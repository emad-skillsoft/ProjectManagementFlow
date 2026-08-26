using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class SuspendedOrgMember : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrgMember_Status",
                table: "OrgMembers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrgMember_Status",
                table: "OrgMembers",
                sql: "[Status] IN ('pending', 'active', 'suspended')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_OrgMember_Status",
                table: "OrgMembers");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrgMember_Status",
                table: "OrgMembers",
                sql: "[Status] IN ('pending', 'active')");
        }
    }
}
