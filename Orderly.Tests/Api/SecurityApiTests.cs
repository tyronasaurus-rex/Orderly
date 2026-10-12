using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;

namespace Orderly.Tests.Api;

public class SecurityApiTests
{
    [Fact]
    public async Task Http_requests_redirect_to_https()
    {
        await using var factory = new OrderlyApiFactory();

        var context = await factory.Server.SendAsync(http =>
        {
            http.Request.Method = HttpMethods.Get;
            http.Request.Scheme = "http";
            http.Request.Host = new HostString("localhost");
            http.Request.Path = "/api/orders";
        });

        Assert.Equal(StatusCodes.Status307TemporaryRedirect, context.Response.StatusCode);
        Assert.Equal("https://localhost:7222/api/orders", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Development_does_not_send_hsts()
    {
        await using var factory = new OrderlyApiFactory();
        var context = await SendHttps(factory, "orderly.example");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Production_sends_hsts()
    {
        await using var factory = new OrderlyApiFactory("Production");
        var context = await SendHttps(factory, "orderly.example");

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Contains("max-age=", context.Response.Headers.StrictTransportSecurity.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cors_allows_only_the_configured_angular_origin()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();

        using var allowed = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        allowed.Headers.Add("Origin", "http://localhost:4200");
        var allowedResponse = await client.SendAsync(allowed);
        Assert.Equal("http://localhost:4200", allowedResponse.Headers.GetValues("Access-Control-Allow-Origin").Single());

        using var rejected = new HttpRequestMessage(HttpMethod.Get, "/api/orders");
        rejected.Headers.Add("Origin", "https://evil.example");
        var rejectedResponse = await client.SendAsync(rejected);
        Assert.False(rejectedResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Responses_send_nosniff_and_no_referrer()
    {
        await using var factory = new OrderlyApiFactory();
        using var client = factory.CreateHttpsClient();

        var response = await client.GetAsync("/api/orders");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
    }

    private static Task<HttpContext> SendHttps(OrderlyApiFactory factory, string host) =>
        factory.Server.SendAsync(http =>
        {
            http.Request.Method = HttpMethods.Get;
            http.Request.Scheme = "https";
            http.Request.Host = new HostString(host);
            http.Request.Path = "/api/orders";
        });
}
