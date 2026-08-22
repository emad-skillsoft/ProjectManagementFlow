using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class OrganizationTree : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // صفوف سابقة قد تحمل NULL أو قيمًا خارج المجموعة المسموحة،
            // فتُسقِط قيودَ CHECK أدناه. تُسوّى قبلها.
            migrationBuilder.Sql(@"
UPDATE [OrgMembers] SET [Role]   = 'member' WHERE [Role]   IS NULL OR [Role]   NOT IN ('owner','admin','member');
UPDATE [OrgMembers] SET [Status] = 'active' WHERE [Status] IS NULL OR [Status] NOT IN ('pending','active');");

            migrationBuilder.DropForeignKey(
                name: "FK_OrgMembers_Organizations_OrganizationId",
                table: "OrgMembers");

            migrationBuilder.DropIndex(
                name: "IX_OrgMembers_OrganizationId",
                table: "OrgMembers");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "OrgMembers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "OrgMembers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Organizations",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Organizations",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Depth",
                table: "Organizations",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Organizations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "Organizations",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "RootId",
                table: "Organizations",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            // المنظّمات القائمة تصير جذورًا: RootId = Id و Path = "<id>/" و Depth = 0.
            // "N" في C# حروف صغيرة بلا شرطات، فيقابلها LOWER(REPLACE(...)) هنا.
            migrationBuilder.Sql(@"
UPDATE [Organizations]
SET [RootId] = [Id],
    [Path]   = LOWER(REPLACE(CONVERT(varchar(36), [Id]), '-', '')) + '/',
    [Depth]  = 0
WHERE [Path] = '' OR [Path] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_OrgMembers_OrganizationId_UserId",
                table: "OrgMembers",
                columns: new[] { "OrganizationId", "UserId" },
                unique: true,
                filter: "[OrganizationId] IS NOT NULL AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OrgMembers_UserId_Status",
                table: "OrgMembers",
                columns: new[] { "UserId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrgMember_Role",
                table: "OrgMembers",
                sql: "[Role] IN ('owner', 'admin', 'member')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrgMember_Status",
                table: "OrgMembers",
                sql: "[Status] IN ('pending', 'active')");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_ParentId",
                table: "Organizations",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_Path",
                table: "Organizations",
                column: "Path");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_RootId_Depth",
                table: "Organizations",
                columns: new[] { "RootId", "Depth" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Organization_Depth",
                table: "Organizations",
                sql: "[Depth] BETWEEN 0 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Organization_Root",
                table: "Organizations",
                sql: "([ParentId] IS NULL AND [RootId] = [Id] AND [Depth] = 0) OR ([ParentId] IS NOT NULL AND [Depth] > 0)");

            migrationBuilder.AddForeignKey(
                name: "FK_Organizations_Organizations_ParentId",
                table: "Organizations",
                column: "ParentId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrgMembers_Organizations_OrganizationId",
                table: "OrgMembers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Organizations_Organizations_ParentId",
                table: "Organizations");

            migrationBuilder.DropForeignKey(
                name: "FK_OrgMembers_Organizations_OrganizationId",
                table: "OrgMembers");

            migrationBuilder.DropIndex(
                name: "IX_OrgMembers_OrganizationId_UserId",
                table: "OrgMembers");

            migrationBuilder.DropIndex(
                name: "IX_OrgMembers_UserId_Status",
                table: "OrgMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrgMember_Role",
                table: "OrgMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrgMember_Status",
                table: "OrgMembers");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_ParentId",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_Path",
                table: "Organizations");

            migrationBuilder.DropIndex(
                name: "IX_Organizations_RootId_Depth",
                table: "Organizations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Organization_Depth",
                table: "Organizations");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Organization_Root",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Depth",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Path",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "RootId",
                table: "Organizations");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "OrgMembers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "OrgMembers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Organizations",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrgMembers_OrganizationId",
                table: "OrgMembers",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_OrgMembers_Organizations_OrganizationId",
                table: "OrgMembers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");
        }
    }
}
