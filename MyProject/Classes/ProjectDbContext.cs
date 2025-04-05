using Microsoft.EntityFrameworkCore;
using MyProject.Domains;

namespace MyProject.Classes
{
    public class ProjectDbContext : DbContext
    {
        public ProjectDbContext(DbContextOptions<ProjectDbContext> options) : base(options)
        {
        }

        public DbSet<Player> Players { get; set; } = null!;
        
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Player>()
                .Property(p => p.SteamId)
                .HasColumnType("bigint");
        }
    }
}
