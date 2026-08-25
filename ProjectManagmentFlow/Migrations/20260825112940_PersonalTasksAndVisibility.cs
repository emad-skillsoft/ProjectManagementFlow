using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class PersonalTasksAndVisibility : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // تطبيع ما سبق قبل شدّ القيود: الصفوف القديمة تركت Visibility فارغاً،
            // والقيدان أدناه يرفضان أيّ قيمةٍ خارج المجموعة.
            migrationBuilder.Sql(@"
                UPDATE [Tasks]
                SET [Visibility] = 'project'
                WHERE [ProjectId] IS NOT NULL
                  AND ([Visibility] IS NULL OR [Visibility] NOT IN ('project', 'private'));");

            migrationBuilder.Sql(@"
                UPDATE [Tasks]
                SET [Visibility] = 'private'
                WHERE [ProjectId] IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "Visibility",
                table: "Tasks",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_CreatedById_ProjectId_Status",
                table: "Tasks",
                columns: new[] { "CreatedById", "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "UX_Task_PersonalCode",
                table: "Tasks",
                columns: new[] { "CreatedById", "Code" },
                unique: true,
                filter: "[ProjectId] IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_PersonalIsPrivate",
                table: "Tasks",
                sql: "[ProjectId] IS NOT NULL OR [Visibility] = 'private'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Task_Visibility",
                table: "Tasks",
                sql: "[Visibility] IS NULL OR [Visibility] IN ('project', 'private')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_CreatedById_ProjectId_Status",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "UX_Task_PersonalCode",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_PersonalIsPrivate",
                table: "Tasks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Task_Visibility",
                table: "Tasks");

            migrationBuilder.AlterColumn<string>(
                name: "Visibility",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32,
                oldNullable: true);
        }
    }
}
