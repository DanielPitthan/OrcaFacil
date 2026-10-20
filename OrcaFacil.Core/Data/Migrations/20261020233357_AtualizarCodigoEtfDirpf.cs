using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AtualizarCodigoEtfDirpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Codigo", "Descricao" },
                values: new object[] { "06", "ETF - Entidade de investimento" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "Codigo", "Descricao" },
                values: new object[] { "09", "Demais fundos de índice de mercado (ETF)" });
        }
    }
}
