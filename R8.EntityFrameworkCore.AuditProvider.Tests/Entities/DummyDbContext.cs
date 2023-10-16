using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.Entities
{
    public class DummyDbContextFactory : IDesignTimeDbContextFactory<DummyDbContext>
    {
        public const string ConnectionString = "Host=localhost;Database=EntityFrameworkAuditProvider;Username=postgres;Password=1";

        public DbContextOptions<DummyDbContext> GetOptions()
        {
            var optionsBuilder = new DbContextOptionsBuilder<DummyDbContext>();
            optionsBuilder.UseNpgsql(ConnectionString);
            return optionsBuilder.Options;
        }
        
        public DummyDbContext CreateDbContext(string[] args)
        {
            var options = GetOptions();
            return new DummyDbContext(options);
        }
    }

    public class DummyDbContext : DbContext
    {
        public DummyDbContext(DbContextOptions<DummyDbContext> options) : base(options)
        {
        }

        public virtual DbSet<FirstAuditableEntity> FirstEntities { get; set; }
        public virtual DbSet<SecondAuditableEntity> SecondEntities { get; set; }
        public virtual DbSet<ThirdEntity> ThirdEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FirstAuditableEntity>();
            modelBuilder.Entity<SecondAuditableEntity>();
            modelBuilder.Entity<ThirdEntity>();

            modelBuilder.Entity<FirstAuditableEntity>()
                .HasMany(x => x.SecondEntities)
                .WithOne(x => x.FirstEntity)
                .HasForeignKey(x => x.FirstEntityId)
                .IsRequired(false);
        }
    }
}