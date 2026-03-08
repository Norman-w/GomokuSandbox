using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GomokuSandbox.Service.Migrations
{
    /// <inheritdoc />
    public partial class AddWorldRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorldRules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MinMovesBeforeWin = table.Column<int>(type: "INTEGER", nullable: false),
                    BlackAdvantage = table.Column<double>(type: "REAL", nullable: false),
                    Direction = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorldRules", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "WorldRules");
        }
    }
}
