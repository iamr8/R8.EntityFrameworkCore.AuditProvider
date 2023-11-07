using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Npgsql;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.Entities
{
    public class DummyDbContextFactory : IDesignTimeDbContextFactory<DummyDbContext>
    {
        public static string ConnectionString
        {
            get
            {
                var csb = new NpgsqlConnectionStringBuilder
                {
                    Host = "localhost",
                    Port = 54322,
                    Database = "r8-audit-test",
                    Username = "postgres",
                    Password = "1"
                };
                return csb.ConnectionString;
            }
        }

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
                .HasMany(x => x.MyEntities)
                .WithOne(x => x.MyAuditableEntity)
                .HasForeignKey(x => x.MyAuditableEntityId)
                .IsRequired(false);
            modelBuilder.Entity<MyAuditableEntity>()
                .HasMany(x => x.Children)
                .WithOne(x => x.Parent)
                .HasForeignKey(x => x.MyAuditableEntityId)
                .IsRequired(false);
            modelBuilder.Entity<MyAuditableEntity>()
                .Property(x => x.Payload)
                .IsRequired(false);
            modelBuilder.Entity<MyEntity>();
        }
    }
}