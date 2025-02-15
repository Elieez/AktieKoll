using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<InsiderTrade> InsiderTrades { get; set; }
}
