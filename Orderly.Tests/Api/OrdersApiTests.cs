using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Orderly.Application;
using Orderly.Infrastructure;
using Orderly.Models;

namespace Orderly.Tests.Api;

public class OrderlyApiFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;

    public OrderlyApiFactory(string environment = "Development")
    {
        _environment = environment;
    }

    public string RelativeOrdersFile { get; } = $"data/tests/{Guid.NewGuid():N}/orders.json";

    public string? StorePath { get; private set; }

    public HttpClient CreateHttpsClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
        });

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:OrdersFile"] = RelativeOrdersFile,
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        var environment = host.Services.GetRequiredService<IHostEnvironment>();
        StorePath = OrdersFileLocation.Resolve(environment.ContentRootPath, RelativeOrdersFile);
        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing || StorePath is null)
            return;

        var directory = Path.GetDirectoryName(StorePath);
        if (directory is not null && Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }
}

public class OrdersApiTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Create_ignores_client_totals_and_stores_the_server_total()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsync("/api/orders", JsonBody(new
        {
            externalReference = "PO-1001",
            customerName = "Northwind",
            total = 999,
            lines = new[]
            {
                new { description = "Binder", quantity = 2, unitPrice = 3.5m, lineTotal = 123 },
            },
        }));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains("/api/orders/", response.Headers.Location?.ToString(), StringComparison.Ordinal);
        using var created = await Read(response);
        Assert.Equal(7.00m, created.RootElement.GetProperty("total").GetDecimal());
        Assert.Equal(7.00m, created.RootElement.GetProperty("lines")[0].GetProperty("lineTotal").GetDecimal());
        Assert.Equal("Submitted", created.RootElement.GetProperty("status").GetString());

        var id = created.RootElement.GetProperty("id").GetGuid();
        var fetched = await client.GetAsync($"/api/orders/{id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        using var stored = await Read(fetched);
        Assert.Equal(7.00m, stored.RootElement.GetProperty("total").GetDecimal());
    }

    [Fact]
    public async Task Repeat_submission_does_not_create_a_duplicate()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();
        var body = Order("PO-1001", "Northwind", "Binder", 2, 3.5m);

        var first = await client.PostAsync("/api/orders", body);
        var second = await client.PostAsync("/api/orders", Order("PO-1001", "Northwind", "Binder", 2, 3.5m));
        var conflict = await client.PostAsync("/api/orders", Order("PO-1001", "Contoso", "Binder", 2, 3.5m));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        using var created = await Read(first);
        using var existing = await Read(second);
        Assert.Equal(created.RootElement.GetProperty("id").GetGuid(), existing.RootElement.GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using var problem = await Read(conflict);
        Assert.Equal("duplicate_reference", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(SubmitOrder.ReferenceInUseDetail, problem.RootElement.GetProperty("detail").GetString());
        AssertNoLeak(await conflict.Content.ReadAsStringAsync(), factory);

        var listed = await client.GetFromJsonAsync<List<OrderBody>>("/api/orders", Json);
        var only = Assert.Single(listed!);
        Assert.Equal("Northwind", only.CustomerName);
        Assert.Equal(7.00m, only.Total);
    }

    [Fact]
    public async Task List_returns_newest_first_and_get_reports_a_missing_order()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();
        var first = await PostOrder(client, "PO-1");
        await Task.Delay(20);
        var second = await PostOrder(client, "PO-2");

        var listed = await client.GetFromJsonAsync<List<OrderBody>>("/api/orders", Json);
        var expected = new[] { first, second }
            .OrderByDescending(order => order.CreatedAtUtc)
            .ThenByDescending(order => order.Id)
            .Select(order => order.Id);
        Assert.Equal(expected, listed!.Select(order => order.Id));

        var missing = Guid.NewGuid();
        var response = await client.GetAsync($"/api/orders/{missing}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = await Read(response);
        Assert.Equal("order_not_found", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(OrderText.NotFound(missing), problem.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Illegal_status_change_explains_what_is_allowed()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();
        var order = await PostOrder(client, "PO-1");

        var rejected = await client.PatchAsync(
            $"/api/orders/{order.Id}/status",
            JsonBody(new { status = "Fulfilled" }));

        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using var problem = await Read(rejected);
        Assert.Equal("invalid_status_transition", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            $"Order {order.Id:D} is Submitted. You can change it to Confirmed or Cancelled.",
            problem.RootElement.GetProperty("detail").GetString());

        var current = await client.GetFromJsonAsync<OrderBody>($"/api/orders/{order.Id}", Json);
        Assert.Equal("Submitted", current!.Status);

        var confirmed = await client.PatchAsync(
            $"/api/orders/{order.Id}/status",
            JsonBody(new { status = "Confirmed" }));
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        using var updated = await Read(confirmed);
        Assert.Equal("Confirmed", updated.RootElement.GetProperty("status").GetString());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Invalid_quantity_is_a_validation_error_and_stores_nothing(int quantity)
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.PostAsync("/api/orders", Order("PO-1", "Northwind", "Binder", quantity, 3.5m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var problem = await Read(response);
        Assert.Equal("validation_error", problem.RootElement.GetProperty("code").GetString());
        Assert.Contains("positive whole number", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        AssertNoLeak(await response.Content.ReadAsStringAsync(), factory);

        var listed = await client.GetFromJsonAsync<List<OrderBody>>("/api/orders", Json);
        Assert.Empty(listed!);
    }

    [Fact]
    public async Task Fractional_quantity_is_rejected()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();
        var json = """
            {
              "externalReference": "PO-1",
              "customerName": "Northwind",
              "lines": [ { "description": "Binder", "quantity": 1.5, "unitPrice": 3.5 } ]
            }
            """;

        var response = await client.PostAsync("/api/orders", new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("validation_error", problem.RootElement.GetProperty("code").GetString());
        Assert.Contains("positive whole number", problem.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        AssertNoLeak(body, factory);
    }

    [Fact]
    public async Task Failed_request_hides_stack_traces_and_file_paths()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();

        var invalid = await client.PostAsync("/api/orders", new StringContent("{", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        AssertNoLeak(await invalid.Content.ReadAsStringAsync(), factory);

        Assert.NotNull(factory.StorePath);
        Directory.CreateDirectory(Path.GetDirectoryName(factory.StorePath)!);
        await File.WriteAllTextAsync(factory.StorePath, "{");
        var broken = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.InternalServerError, broken.StatusCode);
        var body = await broken.Content.ReadAsStringAsync();
        using var problem = JsonDocument.Parse(body);
        Assert.Equal("error", problem.RootElement.GetProperty("code").GetString());
        Assert.Equal("The request could not be completed.", problem.RootElement.GetProperty("detail").GetString());
        AssertNoLeak(body, factory);
    }

    [Fact]
    public void Request_limits_match_the_domain_limits()
    {
        Assert.Equal(Orderly.Domain.OrderFieldLimits.ExternalReferenceMaxLength, CreateOrderRequest.ExternalReferenceMaxLength);
        Assert.Equal(Orderly.Domain.OrderFieldLimits.CustomerNameMaxLength, CreateOrderRequest.CustomerNameMaxLength);
        Assert.Equal(Orderly.Domain.OrderFieldLimits.DescriptionMaxLength, CreateOrderLineRequest.DescriptionMaxLength);
    }

    private static StringContent Order(string reference, string customer, string description, decimal quantity, decimal unitPrice) =>
        JsonBody(new
        {
            externalReference = reference,
            customerName = customer,
            lines = new[]
            {
                new { description, quantity, unitPrice },
            },
        });

    private static StringContent JsonBody(object value) =>
        new(JsonSerializer.Serialize(value, Json), Encoding.UTF8, "application/json");

    private static async Task<JsonDocument> Read(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<OrderBody> PostOrder(HttpClient client, string reference)
    {
        var response = await client.PostAsync("/api/orders", Order(reference, "Northwind", "Binder", 1, 2m));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderBody>(Json))!;
    }

    private static void AssertNoLeak(string body, OrderlyApiFactory factory)
    {
        Assert.DoesNotContain("StackTrace", body, StringComparison.Ordinal);
        Assert.DoesNotContain(".cs", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("at Orderly", body, StringComparison.Ordinal);
        if (factory.StorePath is not null)
            Assert.DoesNotContain(factory.StorePath, body, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record OrderBody(
        Guid Id,
        string ExternalReference,
        string CustomerName,
        decimal Total,
        string Status,
        DateTimeOffset CreatedAtUtc);
}
