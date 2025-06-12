using AktieKoll.Models;
using Microsoft.EntityFrameworkCore;

namespace AktieKoll.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<InsiderTrade> InsiderTrades { get; set; }
}
