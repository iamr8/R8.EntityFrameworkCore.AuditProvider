﻿// <auto-generated />
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests;

#nullable disable

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Migrations
{
    [DbContext(typeof(MsSqlDbContext))]
    [Migration("20240125181739_Initial")]
    partial class Initial
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "7.0.15")
                .HasAnnotation("Relational:MaxIdentifierLength", 128);

            SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

            modelBuilder.Entity("R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.MyAuditableEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<string>("AuditsJson")
                        .HasColumnType("nvarchar(max)")
                        .HasColumnName("Audits");

                    b.Property<DateTime>("Date")
                        .HasColumnType("datetime2");

                    b.Property<DateTimeOffset>("DateOffset")
                        .HasColumnType("datetimeoffset");

                    b.Property<double>("Double")
                        .HasColumnType("float");

                    b.Property<bool>("IsDeleted")
                        .HasColumnType("bit");

                    b.Property<string>("LastName")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("MyAuditableEntityId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.Property<int?>("NullableInt")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("MyAuditableEntityId");

                    b.ToTable("MyAuditableEntities");
                });

            modelBuilder.Entity("R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.MyEntity", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

                    b.Property<int>("MyAuditableEntityId")
                        .HasColumnType("int");

                    b.Property<string>("Name")
                        .HasColumnType("nvarchar(max)");

                    b.HasKey("Id");

                    b.HasIndex("MyAuditableEntityId");

                    b.ToTable("MyEntities");
                });

            modelBuilder.Entity("R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.MyAuditableEntity", b =>
                {
                    b.HasOne("R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.MyAuditableEntity", "Parent")
                        .WithMany("Children")
                        .HasForeignKey("MyAuditableEntityId");

                    b.Navigation("Parent");
                });

            modelBuilder.Entity("R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.MyEntity", b =>
                {
                    b.HasOne("R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.MyAuditableEntity", "MyAuditableEntity")
                        .WithMany("MyEntities")
                        .HasForeignKey("MyAuditableEntityId");

                    b.Navigation("MyAuditableEntity");
                });

            modelBuilder.Entity("R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.MyAuditableEntity", b =>
                {
                    b.Navigation("Children");

                    b.Navigation("MyEntities");
                });
#pragma warning restore 612, 618
        }
    }
}
