using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace R8.EntityFrameworkCore.AuditProvider.Tests.MsSqlTests.Migrations
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Double = table.Column<double>(type: "float", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateOffset = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    NullableInt = table.Column<int>(type: "int", nullable: true),
                    MyAuditableEntityId = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Audits = table.Column<string>(type: "nvarchar(max)", nullable: true)
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
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MyAuditableEntityId = table.Column<int>(type: "int", nullable: false)
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
