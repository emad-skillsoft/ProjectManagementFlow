using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class OrgUnitTypeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Organizations",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            // الافتراضي organization — يُعاد ضبطه بالردم بعده، وبقاؤه
            // يضمن أنّ القيمة دائمًا مقبولة قبل قيد CHECK.
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Organizations",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "organization");

            // ردمٌ من العمق للصفوف القائمة: 0→organization · 1→sector · 2→general_admin ·
            // 3→admin · 4→department · ≥5→division.
            migrationBuilder.Sql(@"
UPDATE [Organizations] SET [Type] = CASE
    WHEN [Depth] <= 0 THEN 'organization'
    WHEN [Depth] = 1  THEN 'sector'
    WHEN [Depth] = 2  THEN 'general_admin'
    WHEN [Depth] = 3  THEN 'admin'
    WHEN [Depth] = 4  THEN 'department'
    ELSE 'division'
END;");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Organization_Type",
                table: "Organizations",
                sql: "[Type] IN ('organization', 'sector', 'general_admin', 'admin', 'department', 'division')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Organization_Type",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "Organizations");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Organizations");
        }
    }
}
