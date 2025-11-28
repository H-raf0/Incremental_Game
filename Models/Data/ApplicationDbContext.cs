using Microsoft.EntityFrameworkCore;
namespace GameServerApi.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite("Data Source=ProjectDB.db");
    }

    // All DbSets in one place
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Progression> Progressions { get; set; } = null!;
    public DbSet<Item> Items { get; set; } = null!;
    public DbSet<InventoryEntry> InventoryEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Progression>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}