using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRegraCatalogoComprovante : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegrasCatalogoComprovante",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    LimiteProdutos = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasCatalogoComprovante", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RegrasCatalogoComprovante",
                columns: new[] { "Id", "LimiteProdutos" },
                values: new object[] { 1, 300 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RegrasCatalogoComprovante");
        }
    }
}
