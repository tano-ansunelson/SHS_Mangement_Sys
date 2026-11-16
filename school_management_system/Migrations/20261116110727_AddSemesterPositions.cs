using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace school_management_system.Migrations
{
    /// <inheritdoc />
    public partial class AddSemesterPositions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SemesterPositions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrollmentId = table.Column<int>(type: "int", nullable: false),
                    SemesterId = table.Column<int>(type: "int", nullable: false),
                    TotalScore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    OutOf = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SemesterPositions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SemesterPositions_Enrollments_EnrollmentId",
                        column: x => x.EnrollmentId,
                        principalTable: "Enrollments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SemesterPositions_Semesters_SemesterId",
                        column: x => x.SemesterId,
                        principalTable: "Semesters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SemesterPositions_EnrollmentId_SemesterId",
                table: "SemesterPositions",
                columns: new[] { "EnrollmentId", "SemesterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SemesterPositions_SemesterId",
                table: "SemesterPositions",
                column: "SemesterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SemesterPositions");
        }
    }
}
