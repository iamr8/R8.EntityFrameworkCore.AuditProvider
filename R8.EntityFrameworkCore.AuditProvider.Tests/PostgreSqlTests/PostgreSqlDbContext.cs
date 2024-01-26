﻿using Microsoft.EntityFrameworkCore;
using R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Entities;

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests
{
    public class PostgreSqlDbContext : DbContext
    {
        public PostgreSqlDbContext(DbContextOptions<PostgreSqlDbContext> options) : base(options)
        {
        }

        public virtual DbSet<MyAuditableEntity> MyAuditableEntities { get; set; }
        public virtual DbSet<MyEntity> MyEntities { get; set; }
        public virtual DbSet<MyAuditableEntityWithoutSoftDelete> MyAuditableEntitiesWithoutSoftDelete { get; set; }

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