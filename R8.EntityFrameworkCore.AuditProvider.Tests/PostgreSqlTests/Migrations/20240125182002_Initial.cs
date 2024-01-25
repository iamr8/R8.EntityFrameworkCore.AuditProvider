using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace R8.EntityFrameworkCore.AuditProvider.Tests.PostgreSqlTests.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MyAuditableEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    LastName = table.Column<string>(type: "text", nullable: true),
                    ListOfIntegers = table.Column<List<int>>(type: "integer[]", nullable: false),
                    ListOfStrings = table.Column<List<string>>(type: "text[]", nullable: false),
                    NullableListOfLongs = table.Column<List<long>>(type: "bigint[]", nullable: true),
                    ArrayOfDoubles = table.Column<double[]>(type: "double precision[]", nullable: false),
                    Double = table.Column<double>(type: "double precision", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DateOffset = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Payload = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    NullableInt = table.Column<int>(type: "integer", nullable: true),
                    MyAuditableEntityId = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Audits = table.Column<JsonElement>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyAuditableEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyAuditableEntities_MyAuditableEntities_MyAuditableEntityId",
                        column: x => x.MyAuditableEntityId,
                        principalTable: "MyAuditableEntities",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MyEntities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    MyAuditableEntityId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MyEntities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MyEntities_MyAuditableEntities_MyAuditableEntityId",
                        column: x => x.MyAuditableEntityId,
                        principalTable: "MyAuditableEntities",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_MyAuditableEntities_MyAuditableEntityId",
                table: "MyAuditableEntities",
                column: "MyAuditableEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_MyEntities_MyAuditableEntityId",
                table: "MyEntities",
                column: "MyAuditableEntityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MyEntities");

            migrationBuilder.DropTable(
                name: "MyAuditableEntities");
        }
    }
}
