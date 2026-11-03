using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace school_management_system.Migrations
{
    /// <inheritdoc />
    public partial class MidtermFinalScores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Grade",
                table: "Results");

            migrationBuilder.DropColumn(
                name: "Total",
                table: "Results");

            migrationBuilder.RenameColumn(
                name: "HallMasterAssignmentId",
                table: "HallMasterAssignments",
                newName: "Id");

            migrationBuilder.AddColumn<bool>(
                name: "IsCurrent",
                table: "Semesters",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsCurrent",
                table: "Semesters");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "HallMasterAssignments",
                newName: "HallMasterAssignmentId");

            migrationBuilder.AddColumn<string>(
                name: "Grade",
                table: "Results",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Total",
                table: "Results",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);
        }
    }
}
