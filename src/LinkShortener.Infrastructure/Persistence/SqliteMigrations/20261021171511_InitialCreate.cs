using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkShortener.Infrastructure.Persistence.SqliteMigrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Links",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    DestinationUrl = table.Column<string>(type: "TEXT", maxLength: 2048, nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ClickCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Links", x => x.Id);
                    table.CheckConstraint("CK_Links_ClickCount_NonNegative", "\"ClickCount\" >= 0");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Links_Code",
                table: "Links",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Links");
        }
    }
}
