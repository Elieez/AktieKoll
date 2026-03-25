using System.Threading.RateLimiting;
using AktieKoll.Data;
using AktieKoll.Interfaces;
using AktieKoll.Tests.Fixture;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace AktieKoll.Tests.Fixture;

public class WebApplicationFactoryFixture : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgresConnection"] = null,
                // Provide dummy Google credentials so the app starts in tests
                ["Google:ClientId"]     = "test-google-client-id",
                ["Google:ClientSecret"] = "test-google-client-secret",
                ["Frontend:Url"]        = "http://localhost:3000",
            }!);
        });

        builder.ConfigureServices(services =>
        {
            // Remove ALL DbContext-related registrations
            var descriptorsToRemove = services
                .Where(d =>
                    d.ServiceType == typeof(ApplicationDbContext) ||
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
                services.Remove(descriptor);

            // In-memory database — same name across all scopes
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(_databaseName);
                options.EnableSensitiveDataLogging();
            });

            // Replace rate limiters with no-op partitions
            var rateLimiterDescriptors = services
                .Where(d => d.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>))
                .ToList();
            foreach (var d in rateLimiterDescriptors)
                services.Remove(d);

            services.AddRateLimiter(options =>
            {
                options.AddPolicy("auth",       _ => RateLimitPartition.GetNoLimiter("no-limit"));
                options.AddPolicy("api",        _ => RateLimitPartition.GetNoLimiter("no-limit"));
                options.AddPolicy("sensitive",  _ => RateLimitPartition.GetNoLimiter("no-limit"));
                options.AddPolicy("public-api", _ => RateLimitPartition.GetNoLimiter("no-limit"));
            });

            // Replace real email service with a no-op mock so tests never hit SMTP
            var emailDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null) services.Remove(emailDescriptor);

            var mockEmail = new Mock<IEmailService>();
            mockEmail.Setup(s => s.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
            mockEmail.Setup(s => s.SendPasswordResetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
            mockEmail.Setup(s => s.SendAccountDeletionRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);
            mockEmail.Setup(s => s.SendAccountDeletedConfirmationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                     .Returns(Task.CompletedTask);

            services.AddSingleton(mockEmail.Object);
        });
    }

    public void ResetDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        db.Database.EnsureCreated();

        db.RefreshTokens.RemoveRange(db.RefreshTokens);
        db.Users.RemoveRange(db.Users);
        db.InsiderTrades.RemoveRange(db.InsiderTrades);
        db.Companies.RemoveRange(db.Companies);
        db.SaveChanges();
    }
}
