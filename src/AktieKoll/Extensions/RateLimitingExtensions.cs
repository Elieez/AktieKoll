using System.Threading.RateLimiting;

namespace AktieKoll.Extensions;

public static class RateLimitingExtensions
{
    public static IServiceCollection AddCustomRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            var authLimit = configuration.GetValue<int>("RateLimiting:Auth:PermitLimit", 5);
            var authWindow = configuration.GetValue<int>("RateLimiting:Auth:WindowMinutes", 1);

            options.AddPolicy("auth", context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authLimit,
                        Window = TimeSpan.FromMinutes(authWindow),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    });
            });

            var apiLimit = configuration.GetValue<int>("RateLimiting:Api:PermitLimit", 100);
            var apiWindow = configuration.GetValue<int>("RateLimiting:Api:WindowMinutes", 1);

            options.AddPolicy("api", context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ =>
                    new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = apiLimit,
                        Window = TimeSpan.FromMinutes(apiWindow),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 5
                    });
            });

            options.AddPolicy("sensitive", context =>
            {
                var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                return RateLimitPartition.GetSlidingWindowLimiter(ipAddress, _ =>
                    new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 2
                    });
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = retryAfter.TotalSeconds.ToString();

                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        error = "Too many requests",
                        message = "You have exceeded the rate limit. Please try again later.",
                        retryAfter = retryAfter.TotalSeconds
                    }, cancellationToken);

                }
                else
                {
                    await context.HttpContext.Response.WriteAsJsonAsync(new
                    {
                        error = "Too many requests",
                        message = "You have exceeded the rate limit. Please try again later."
                    }, cancellationToken);
                }
            };
        });
        
        return services;
    }
}
