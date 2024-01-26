using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using R8.EntityFrameworkCore.AuditProvider.Abstractions;
using R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests
{
    public class MsSqlDbContext : DbContext
    {
        public MsSqlDbContext(DbContextOptions<MsSqlDbContext> options) : base(options)
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
            modelBuilder.Entity<MyEntity>();
        }
    }
}