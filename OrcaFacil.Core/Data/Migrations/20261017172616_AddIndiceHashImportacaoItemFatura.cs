using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIndiceHashImportacaoItemFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItensLancamento_TransacaoId",
                table: "ItensLancamento");

            migrationBuilder.CreateIndex(
                name: "IX_ItensLancamento_TransacaoId_HashImportacao",
                table: "ItensLancamento",
                columns: new[] { "TransacaoId", "HashImportacao" },
                unique: true,
                filter: "\"HashImportacao\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ItensLancamento_TransacaoId_HashImportacao",
                table: "ItensLancamento");

            migrationBuilder.CreateIndex(
                name: "IX_ItensLancamento_TransacaoId",
                table: "ItensLancamento",
                column: "TransacaoId");
        }
    }
}
