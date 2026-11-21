using System.Text.Json.Serialization;
using BlastRadius.DemoApp.Services;
using BlastRadius.Sdk.Extensions;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Register BlastRadius SDK with real-time in-memory cache and Redis telemetry
builder.Services.AddBlastRadiusSdk(options =>
{
    options.ControlPlaneUrl = builder.Configuration["BlastRadius:ControlPlaneUrl"] ?? "http://localhost:5188";
    options.RedisConnectionString = builder.Configuration.GetConnectionString("Redis") 
                                  ?? builder.Configuration["BlastRadius:RedisConnectionString"] 
                                  ?? string.Empty;
    options.Environment = builder.Configuration["BlastRadius:Environment"] ?? "production";
    options.PubSubChannel = builder.Configuration["BlastRadius:PubSubChannel"] ?? "flag-updates";
    options.EnableTelemetry = true;
    options.TelemetryFlushIntervalMs = 1000;
});

// 2. Register Simulation & Chaos Domain Services
builder.Services.AddSingleton<IChaosManager, ChaosManager>();
builder.Services.AddScoped<ICheckoutService, CheckoutService>();

// 3. Web API Controllers & JSON Formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 4. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 5. OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BlastRadius DemoApp — Checkout Simulation API",
        Version = "v1",
        Description = "E-Commerce checkout API wrapped with BlastRadius.Sdk. Demonstrates canary rollouts, live MurmurHash3 evaluation, and automated 50ms circuit breaking."
    });
});

var app = builder.Build();

// 6. Serve static files & Developer UI at root URL (http://localhost:5288/)
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DemoApp API v1");
        c.RoutePrefix = "swagger"; // Swagger at /swagger, UI at /
    });
}

app.UseCors("AllowAll");
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose Program for test harness
public partial class Program { }
