using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class ProjectDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // طبّع الصفوف القديمة قبل تضييق الأعمدة وإضافة القيود والفهارس الفريدة.
            migrationBuilder.Sql(@"
UPDATE [Tasks]
SET [Status] = 'todo'
WHERE [Status] IS NULL OR [Status] NOT IN ('todo', 'in_progress', 'in_review', 'done', 'cancelled');

UPDATE [Tasks]
SET [Priority] = 'normal'
WHERE [Priority] IS NULL OR [Priority] NOT IN ('low', 'normal', 'high', 'urgent');

UPDATE [TeamMembers] SET [Role] = 'lead' WHERE [Role] = 'leader';
UPDATE [TeamMembers]
SET [Role] = 'member'
WHERE [Role] IS NULL OR [Role] NOT IN ('lead', 'deputy', 'member');

;WITH [DuplicateRoles] AS
(
    SELECT [Id], ROW_NUMBER() OVER
        (PARTITION BY [TeamId], [Role] ORDER BY [JoinedAt], [Id]) AS [RowNumber]
    FROM [TeamMembers]
    WHERE [TeamId] IS NOT NULL AND [Role] IN ('lead', 'deputy')
)
UPDATE [TeamMembers]
SET [Role] = 'member'
WHERE [Id] IN (SELECT [Id] FROM [DuplicateRoles] WHERE [RowNumber] > 1);");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ProjectId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_TeamId",
                table: "TeamMembers");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "TeamMembers",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tasks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Tasks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "ActivityLog",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrganizationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActorId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLog", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLog_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ProjectId",
                table: "Teams",
                column: "ProjectId",
                unique: true,
                filter: "[ProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId_UserId",
                table: "TeamMembers",
                columns: new[] { "TeamId", "UserId" },
                unique: true,
                filter: "[TeamId] IS NOT NULL AND [UserId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_TeamMember_Deputy",
                table: "TeamMembers",
                column: "TeamId",
                unique: true,
                filter: "[Role] = 'deputy'");

            migrationBuilder.CreateIndex(
                name: "UX_TeamMember_Lead",
                table: "TeamMembers",
                column: "TeamId",
                unique: true,
                filter: "[Role] = 'lead'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TeamMember_Role",
                table: "TeamMembers",
                sql: "[Role] IN ('lead', 'deputy', 'member')");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_AssigneeId_Status",
                table: "Tasks",
                columns: new[] { "AssigneeId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Status",
                table: "Tasks",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_Priority",
                table: "Tasks",
                sql: "[Priority] IN ('low', 'normal', 'high', 'urgent')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_Status",
                table: "Tasks",
                sql: "[Status] IN ('todo', 'in_progress', 'in_review', 'done', 'cancelled')");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_OrganizationId_CreatedAt",
                table: "ActivityLog",
                columns: new[] { "OrganizationId", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLog_ProjectId_CreatedAt",
                table: "ActivityLog",
                columns: new[] { "ProjectId", "CreatedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLog");

            migrationBuilder.DropIndex(
                name: "IX_Teams_ProjectId",
                table: "Teams");

            migrationBuilder.DropIndex(
                name: "IX_TeamMembers_TeamId_UserId",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "UX_TeamMember_Deputy",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "UX_TeamMember_Lead",
                table: "TeamMembers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TeamMember_Role",
                table: "TeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_AssigneeId_Status",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Status",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_Priority",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_Status",
                table: "Tasks");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Teams",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Role",
                table: "TeamMembers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ProjectId",
                table: "Teams",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId",
                table: "TeamMembers",
                column: "TeamId");
        }
    }
}
