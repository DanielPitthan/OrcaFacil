using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHashImportacaoItemFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Coluna nula: itens já gravados continuam iguais, sem UPDATE nem recriação da tabela.
            migrationBuilder.AddColumn<string>(
                name: "HashImportacao",
                table: "ItensLancamento",
                type: "TEXT",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HashImportacao",
                table: "ItensLancamento");
        }
    }
}
