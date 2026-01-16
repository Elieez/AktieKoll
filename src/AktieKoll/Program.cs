using System.Threading.RateLimiting;
using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Middleware;
using AktieKoll.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configure CORS with frontend URL from configuration
var frontendUrl = builder.Configuration["FrontendUrl"] ?? "http://localhost:3000";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
          .WithOrigins(frontendUrl)
          .AllowAnyMethod()
          .AllowAnyHeader()
          .WithExposedHeaders("X-RateLimit-Limit", "X-RateLimit-Remaining", "X-RateLimit-Reset");
    });
});

// Configure rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global rate limit: 100 requests per minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ipAddress,
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            await context.HttpContext.Response.WriteAsync(
                $"Too many requests. Please try again after {retryAfter.TotalSeconds} seconds.",
                cancellationToken);
        }
        else
        {
            await context.HttpContext.Response.WriteAsync(
                "Too many requests. Please try again later.",
                cancellationToken);
        }
    };
});

builder.Services.AddControllers();

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Health checks with database connectivity
builder.Services.AddHealthChecks()
    .AddNpgSql(
        connectionString: connectionString ?? throw new InvalidOperationException("PostgresConnection not configured"),
        name: "postgresql",
        tags: new[] { "db", "ready" });

// HTTP clients and services
builder.Services.AddHttpClient<CsvFetchService>();
builder.Services.AddHttpClient<IOpenFigiService, OpenFigiService>(client =>
    client.BaseAddress = new Uri("https://api.openfigi.com/v3/"));
builder.Services.AddTransient<ISymbolService, SymbolService>();

builder.Services.AddScoped<IInsiderTradeService, InsiderTradeService>();

var app = builder.Build();

// Configure HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseHttpsRedirection();

// Health check endpoints
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = healthCheck => healthCheck.Tags.Contains("ready")
});

app.UseCors("AllowFrontend");

// Rate limiting
app.UseRateLimiter();

// API key authentication
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();

app.UseRouting();

app.UseAuthorization();

app.MapControllers();

app.Run();