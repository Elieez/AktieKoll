using AktieKoll.Data;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Services;

public class RefreshTokenCleanupService(IServiceProvider serviceProvider, ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    private readonly TimeSpan cleanupInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("RefreshToken Cleanup Service started");

        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokens(stoppingToken);
                await Task.Delay(cleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error occurred during Token cleanup");
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        logger.LogInformation("RefreshToken Cleanup Service stopped");
    }

    private async Task CleanupExpiredTokens(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        
        var deletedCount = await db.RefreshTokens
            .Where(rt => rt.ExpiresAt < DateTime.UtcNow ||
                        (rt.IsRevoked && rt.CreatedAt < cutoffDate))
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedCount > 0)
        {
            logger.LogInformation("Deleted {Count} expired/revoked refresh tokens", deletedCount);
        }
        else
        {
            logger.LogDebug("No expired tokens to clean up");
        }
    }
}
