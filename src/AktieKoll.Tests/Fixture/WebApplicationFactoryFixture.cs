using AktieKoll.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AktieKoll.Tests.Fixture;

public class WebApplicationFactoryFixture : WebApplicationFactory<Program>
{
    private const string DatabaseName = "IntegrationTestDatabase";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgresConnection"] = null
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
            {
                services.Remove(descriptor);
            }

            // Add InMemory database - SAME NAME for all scopes
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseInMemoryDatabase(DatabaseName);
                options.EnableSensitiveDataLogging();
            });
        });
    }

    public void ResetDatabase()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Ensure database exists
        db.Database.EnsureCreated();

        // Clear all data
        db.RefreshTokens.RemoveRange(db.RefreshTokens);
        db.Users.RemoveRange(db.Users);
        db.InsiderTrades.RemoveRange(db.InsiderTrades);
        db.SaveChanges();
    }
}
