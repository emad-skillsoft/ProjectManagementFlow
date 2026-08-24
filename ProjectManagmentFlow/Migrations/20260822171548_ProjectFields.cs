using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectManagmentFlow.Migrations
{
    /// <inheritdoc />
    public partial class ProjectFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // تصحيح القيم قبل أيّ قيد: القيود الجديدة ترفض الصفوف القديمة إن أُضيفت
            // قبل أن تصير قيمها صالحة — نفس فخّ OrganizationTree.
            migrationBuilder.Sql(@"
UPDATE [Projects] SET [Status]   = 'planning' WHERE [Status]   IS NULL OR [Status]   NOT IN ('planning','active','on_hold','done');
UPDATE [Projects] SET [Priority] = 'normal'   WHERE [Priority] IS NULL OR [Priority] NOT IN ('low','normal','high','urgent');
UPDATE [Projects] SET [Name]     = N'—'       WHERE [Name] IS NULL OR LTRIM(RTRIM([Name])) = '';
UPDATE [Projects] SET [Code]     = LOWER(REPLACE(CONVERT(varchar(36), [Id]), '-', ''))
WHERE [Code] IS NULL OR LTRIM(RTRIM([Code])) = '';");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganizationId",
                table: "Projects");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Projects",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Projects",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Projects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId_ParentTaskId_CompletedAt",
                table: "Tasks",
                columns: new[] { "ProjectId", "ParentTaskId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId_Status",
                table: "Projects",
                columns: new[] { "OrganizationId", "Status" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Project_Dates",
                table: "Projects",
                sql: "[StartDate] IS NULL OR [DueDate] IS NULL OR [DueDate] >= [StartDate]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Project_Priority",
                table: "Projects",
                sql: "[Priority] IN ('low', 'normal', 'high', 'urgent')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Project_Status",
                table: "Projects",
                sql: "[Status] IN ('planning', 'active', 'on_hold', 'done')");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Tasks_ProjectId_ParentTaskId_CompletedAt",
                table: "Tasks");

            migrationBuilder.DropIndex(
                name: "IX_Projects_OrganizationId_Status",
                table: "Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Project_Dates",
                table: "Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Project_Priority",
                table: "Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Project_Status",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Projects");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Priority",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tasks_ProjectId",
                table: "Tasks",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Projects_OrganizationId",
                table: "Projects",
                column: "OrganizationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Organizations_OrganizationId",
                table: "Projects",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id");
        }
    }
}
