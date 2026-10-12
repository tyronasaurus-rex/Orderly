using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using Orderly.Application;
using Orderly.Infrastructure;
using Orderly.Models;
using Orderly.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = OrderRequestLimits.MaxBodyBytes;
});

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip;
    });
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = OrderProblemMapper.InvalidModelState;
});
builder.Services.AddOpenApi();
builder.Services.AddHttpsRedirection(options =>
{
    options.HttpsPort = 7222;
});

const string AngularCorsPolicy = "Angular";
var allowedOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
allowedOrigins = allowedOrigins
    .Where(origin => !string.IsNullOrWhiteSpace(origin))
    .Distinct(StringComparer.Ordinal)
    .ToArray();
builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .WithMethods(HttpMethods.Get, HttpMethods.Post, HttpMethods.Patch);
    });
});

builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IOrderRepository>(services =>
{
    var environment = services.GetRequiredService<IHostEnvironment>();
    var configuration = services.GetRequiredService<IConfiguration>();
    var path = OrdersFileLocation.Resolve(environment.ContentRootPath, configuration["Storage:OrdersFile"]);
    return new JsonFileOrderRepository(path);
});
builder.Services.AddScoped<SubmitOrder>();
builder.Services.AddScoped<GetOrder>();
builder.Services.AddScoped<ListOrders>();
builder.Services.AddScoped<ChangeOrderStatus>();

var app = builder.Build();

app.UseExceptionHandler(OrderProblemMapper.HandleUnexpected);

if (!app.Environment.IsDevelopment())
    app.UseHsts();

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseCors(AngularCorsPolicy);

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
