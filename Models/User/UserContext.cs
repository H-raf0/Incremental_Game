using Microsoft.EntityFrameworkCore;
namespace GameServerApi.Models;

public class UserContext : DbContext
{
    public UserContext(DbContextOptions<UserContext> options)
        : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        // Connexion a la base sqlite
        options.UseSqlite("Data Source=User.db");
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Progression> Progressions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure cascade delete: when a User is deleted, its Progressions are deleted too
        modelBuilder.Entity<Progression>()
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
