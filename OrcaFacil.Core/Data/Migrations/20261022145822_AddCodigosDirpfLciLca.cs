using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigosDirpfLciLca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CodigosDirpf",
                columns: new[] { "Id", "Codigo", "Descricao", "EhSistema", "Ficha", "Grupo", "TipoAtivo", "Variante", "VigenciaFim", "VigenciaInicio" },
                values: new object[,]
                {
                    { 12, "12", "Rendimentos de cadernetas de poupança, letras hipotecárias, LCA, LCI, CRA e CRI", true, 1, "", 0, "RendaFixaIsenta", null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 13, "03", "Títulos isentos de tributação", true, 0, "04", 0, "RendaFixaIsenta", null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 13);
        }
    }
}
