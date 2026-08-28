using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationCodeUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "UX_Organization_Code",
                table: "Organizations",
                columns: new[] { "RootId", "Code" },
                unique: true,
                filter: "[Code] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Organization_Code",
                table: "Organizations");
        }
    }
}
