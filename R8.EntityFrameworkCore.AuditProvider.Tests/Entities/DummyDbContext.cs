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

        public virtual DbSet<MyAuditableEntity> MyAuditableEntities { get; set; }
        public virtual DbSet<MyEntity> MyEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MyAuditableEntity>()
                .HasMany(x => x.RelationalEntities)
                .WithOne(x => x.MyAuditableEntity)
                .HasForeignKey(x => x.MyAuditableEntityId)
                .IsRequired(false);
            modelBuilder.Entity<MyEntity>();
        }
    }
}