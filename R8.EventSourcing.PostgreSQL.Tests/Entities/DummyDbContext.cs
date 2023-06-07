using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace R8.EventSourcing.PostgreSQL.Tests.Entities
{
    public class DummyDbContextFactory : IDesignTimeDbContextFactory<DummyDbContext>
    {
        public const string ConnectionString = "Host=localhost;Database=EventSourcing;Username=postgres;Password=1";

        public DummyDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DummyDbContext>();
            optionsBuilder.UseNpgsql(ConnectionString);
            return new DummyDbContext(optionsBuilder.Options);
        }
    }

    public class DummyDbContext : DbContext
    {
        public DummyDbContext(DbContextOptions<DummyDbContext> options) : base(options)
        {
        }

        public virtual DbSet<FirstEntity> FirstEntities { get; set; }
        public virtual DbSet<SecondEntity> SecondEntities { get; set; }
        public virtual DbSet<ThirdEntity> ThirdEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstEntity>();
            modelBuilder.Entity<SecondEntity>();
            modelBuilder.Entity<ThirdEntity>();

            modelBuilder.Entity<FirstEntity>()
                .HasMany(x => x.SecondEntities)
                .WithOne(x => x.FirstEntity)
                .HasForeignKey(x => x.FirstEntityId)
                .IsRequired(false);
        }
    }
}