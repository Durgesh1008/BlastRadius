using System.Text.Json.Serialization;
using BlastRadius.Api.Data;
using BlastRadius.Api.Services;
using BlastRadius.Api.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration (Neon PostgreSQL with graceful in-memory fallback)
string? rawPgConn = builder.Configuration.GetConnectionString("PostgreSQL");
string? pgConn = !string.IsNullOrWhiteSpace(rawPgConn) ? PostgresConnectionHelper.Normalize(rawPgConn) : null;
bool isPostgreSqlConfigured = !string.IsNullOrWhiteSpace(pgConn) && !pgConn.Contains("YOUR_NEON_POSTGRESQL_CONNECTION_STRING");

builder.Services.AddDbContext<BlastRadiusDbContext>(options =>
{
    if (isPostgreSqlConfigured)
    {
        options.UseNpgsql(pgConn);
    }
    else
    {
        // Graceful fallback: allows the API to run and test immediately before Neon keys are inserted
        options.UseInMemoryDatabase("BlastRadiusDb");
    }
});

// 2. Distributed Messaging & Telemetry Services
builder.Services.AddSingleton<IRedisPublisher, RedisPublisher>();
builder.Services.AddSingleton<ITelemetryReader, TelemetryReader>();

// 3. Core Feature Flag Business Services
builder.Services.AddScoped<IFeatureFlagService, FeatureFlagService>();

// 4. Autonomous 50ms Circuit Breaker Worker
builder.Services.AddHostedService<CircuitBreakerWorker>();

// 5. Web API Controllers & JSON Formatting
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// 6. CORS Policy for Angular Control Plane UI
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 7. OpenAPI / Swagger Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BlastRadius Control Plane API",
        Version = "v1",
        Description = "Self-Healing Feature Flag & Blast-Radius Control Engine API. Provides real-time canary rollouts, audit trails, and automated circuit breaker kill-switches."
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment() || true)
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "BlastRadius API v1");
        c.RoutePrefix = string.Empty; // Swagger UI served at application root (http://localhost:PORT/)
    });
}

app.UseCors("AllowAll");
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Database Migration & Seed Check
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<BlastRadiusDbContext>();

    try
    {
        if (db.Database.IsRelational())
        {
            logger.LogInformation("Ensuring PostgreSQL schema and seed records exist in Neon...");
            db.Database.EnsureCreated();
            logger.LogInformation("PostgreSQL schema initialized successfully.");
        }
        else
        {
            logger.LogInformation("Neon PostgreSQL connection string not configured. Running with in-memory database.");
            db.Database.EnsureCreated();
        }

        // Seed demo flag if not present
        if (!db.FeatureFlags.Any(f => f.Key == "new-upi-checkout" && f.EnvironmentId == "production"))
        {
            var defaultFlag = new BlastRadius.Api.Data.Entities.FeatureFlagEntity
            {
                Key = "new-upi-checkout",
                Name = "1-Click Instant UPI Checkout",
                Description = "Accelerated payment checkout flow using instant UPI intent",
                IsEnabled = true,
                RolloutPercentage = 25,
                EnvironmentId = "production",
                Version = 1,
                BlastRadiusPolicy = new BlastRadius.Api.Data.Entities.BlastRadiusPolicyEntity
                {
                    MaxErrorRatePercent = 2.0,
                    MinRequestsThreshold = 10,
                    SlidingWindowSeconds = 60,
                    AutoKillEnabled = true
                }
            };
            db.FeatureFlags.Add(defaultFlag);
            db.SaveChanges();
            logger.LogInformation("Seeded demo feature flag 'new-upi-checkout' (Rollout: 25%, Threshold: 2.0%).");
        }
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not automatically migrate PostgreSQL database. Please verify connection credentials in appsettings.json.");
    }
}

app.Run();

// Expose Program for test harness
public partial class Program { }
