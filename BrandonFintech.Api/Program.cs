using BrandonFintech.Api.Services;
using BrandonFintech.Infrastructure;
using BrandonFintech.Platform;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Stripe;
using System.Threading.RateLimiting;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
{
    config.WriteTo.Console();
});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<IIdempotencyService, IdempotencyService>();
builder.Services.AddSingleton<InMemoryPlatformEventPublisher>();
builder.Services.AddSingleton<IPlatformEventPublisher>(services =>
    services.GetRequiredService<InMemoryPlatformEventPublisher>());
builder.Services.AddSingleton(services =>
{
    var enabled =
        PlatformIntegrationFeature.IsEnabled(Environment.GetEnvironmentVariable(PlatformIntegrationFeature.EnvironmentVariableName)) ||
        PlatformIntegrationFeature.IsEnabled(builder.Configuration["PlatformIntegration:Enabled"]);

    return new BrandonFintechPlatformAdapter(
        enabled,
        services.GetRequiredService<IPlatformEventPublisher>());
});

var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
var jwtSecret = builder.Configuration["Jwt:Secret"];

if (string.IsNullOrWhiteSpace(jwtIssuer) ||
    string.IsNullOrWhiteSpace(jwtAudience) ||
    string.IsNullOrWhiteSpace(jwtSecret))
{
    throw new InvalidOperationException("JWT configuration is missing.");
}

if (jwtSecret.Length < 32)
{
    throw new InvalidOperationException("JWT secret must be at least 32 characters.");
}

if (builder.Environment.IsProduction() &&
    jwtSecret.Contains("ChangeThis", StringComparison.OrdinalIgnoreCase))
{
    throw new InvalidOperationException("JWT secret must be replaced for production.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];

if (!string.IsNullOrWhiteSpace(stripeSecretKey))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddMemoryCache();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        var configuredOrigins = builder.Configuration["Cors:AllowedOrigins"] ??
            Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS");

        var allowedOrigins = (configuredOrigins ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (allowedOrigins.Length == 0 && builder.Environment.IsDevelopment())
        {
            allowedOrigins =
            [
                "http://localhost:5173",
                "http://localhost:5015",
                "https://localhost:5173"
            ];
        }

        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException("CORS allowed origins must be configured outside Development.");
        }

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("AuthSensitive", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(15),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("Default");

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
    }

    await next();
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "BrandonFintech",
    version = "0.1.0",
    status = "running"
}));

app.MapGet("/health", async (ApplicationDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    return Results.Ok(new
    {
        status = canConnect ? "healthy" : "database_unavailable",
        timestamp = DateTime.UtcNow
    });
});

app.MapGet("/ready", async (ApplicationDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();

    if (!canConnect)
    {
        return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    return Results.Ok(new
    {
        status = "ready",
        database = "available",
        timestamp = DateTime.UtcNow
    });
});

app.MapGet("/api/v1/platform/status", () => Results.Ok(new
{
    success = true,
    data = new
    {
        name = "BrandonFintech",
        version = "0.1.0",
        stripeConfigured = !string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey),
        environment = app.Environment.EnvironmentName
    }
}));

app.MapControllers();

app.Run();
