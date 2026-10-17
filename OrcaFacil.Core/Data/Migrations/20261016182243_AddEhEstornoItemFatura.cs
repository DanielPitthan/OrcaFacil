using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEhEstornoItemFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default false no ADD COLUMN: linhas antigas continuam iguais, sem UPDATE nem recriação da tabela.
            migrationBuilder.AddColumn<bool>(
                name: "EhEstorno",
                table: "ItensLancamento",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EhEstorno",
                table: "ItensLancamento");
        }
    }
}
