using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class TaskBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // الصفوف القديمة لا تستوفي قيود اللوحة؛ طبّعها قبل التضييق.
            migrationBuilder.Sql(@"
UPDATE [Tasks] SET [Title] = N'—' WHERE [Title] IS NULL OR LTRIM(RTRIM([Title])) = '';");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Tasks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Tasks",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Tasks",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            // الرمز فريدٌ لكلّ مشروع، فلا بدّ من ترقيم الصفوف القائمة قبل إنشاء الفهرس.
            migrationBuilder.Sql(@"
WITH [Numbered] AS (
  SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [ProjectId] ORDER BY [CreatedAt], [Id]) AS [N]
  FROM [Tasks])
UPDATE t SET [Code] = 'T-' + CONVERT(varchar(10), n.[N])
FROM [Tasks] t JOIN [Numbered] n ON n.[Id] = t.[Id]
WHERE t.[Code] IS NULL OR t.[Code] = '';

WITH [Ordered] AS (
  SELECT [Id], ROW_NUMBER() OVER (PARTITION BY [ProjectId], [Status] ORDER BY [CreatedAt], [Id]) AS [N]
  FROM [Tasks])
UPDATE t SET [Position] = o.[N] * 1000
FROM [Tasks] t JOIN [Ordered] o ON o.[Id] = t.[Id]
WHERE t.[Position] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Code",
                table: "Tasks",
                columns: new[] { "ProjectId", "Code" },
                unique: true,
                filter: "[ProjectId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_Status_Position",
                table: "Tasks",
                columns: new[] { "ProjectId", "Status", "Position" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Code",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_Status_Position",
                table: "Tasks");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Tasks");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Tasks",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
