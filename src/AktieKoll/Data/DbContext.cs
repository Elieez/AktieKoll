using AktieKoll.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<InsiderTrade> InsiderTrades { get; set; }

    public DbSet<RefreshToken> RefreshTokens { get; set; }

    public DbSet<Company> Companies { get; set; }

    public DbSet<UserCompanyFollow> UserCompanyFollows { get; set; }

    public DbSet<NotificationPreference> NotificationPreferences { get; set; }

    public DbSet<NotificationLog> NotificationLogs { get; set; }

    public DbSet<VerificationCode> VerificationCodes { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(rt => rt.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(rt => rt.Token);
        });

        builder.Entity<Company>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.Code).IsUnique();
            entity.HasIndex(c => c.Isin);
            entity.HasIndex(c => c.Name);
        });

        builder.Entity<UserCompanyFollow>(entity =>
        {
            entity.HasKey(f => f.Id);

            entity.HasOne(f => f.User)
                  .WithMany()
                  .HasForeignKey(f => f.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(f => f.Company)
                  .WithMany()
                  .HasForeignKey(f => f.CompanyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(f => new { f.UserId, f.CompanyId }).IsUnique();
        });

        builder.Entity<NotificationPreference>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.HasOne(p => p.User)
                  .WithMany()
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(p => p.UserId).IsUnique();
        });

        builder.Entity<VerificationCode>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.Code).IsUnique();
            entity.HasOne<ApplicationUser>()
                  .WithMany()
                  .HasForeignKey(v => v.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<NotificationLog>(entity =>
        {
            entity.HasKey(l => l.Id);

            entity.HasOne(l => l.User)
                  .WithMany()
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.Company)
                  .WithMany()
                  .HasForeignKey(l => l.CompanyId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.Property(l => l.TransactionIds)
                  .HasColumnType("integer[]");

            entity.HasIndex(l => new { l.UserId, l.CompanyId, l.BatchRunId, l.Channel });
        });
    }
}