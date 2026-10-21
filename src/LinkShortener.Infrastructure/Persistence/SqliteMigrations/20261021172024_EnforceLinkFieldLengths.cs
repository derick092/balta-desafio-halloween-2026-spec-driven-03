using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LinkShortener.Infrastructure.Persistence.SqliteMigrations
{
    /// <inheritdoc />
    public partial class EnforceLinkFieldLengths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Links_Code_Length",
                table: "Links",
                sql: "length(\"Code\") BETWEEN 4 AND 20");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Links_DestinationUrl_Length",
                table: "Links",
                sql: "length(\"DestinationUrl\") <= 2048");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Links_Code_Length",
                table: "Links");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Links_DestinationUrl_Length",
                table: "Links");
        }
    }
}
