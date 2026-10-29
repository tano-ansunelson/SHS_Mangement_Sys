using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace school_management_system.Migrations
{
    /// <inheritdoc />
    public partial class FixHallId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "HallId",
                table: "Halls",
                newName: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Halls",
                newName: "HallId");
        }
    }
}
