using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDirpfRendaFixaJcp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CodigosDirpf",
                columns: new[] { "Id", "Codigo", "Descricao", "EhSistema", "Ficha", "Grupo", "TipoAtivo", "Variante", "VigenciaFim", "VigenciaInicio" },
                values: new object[,]
                {
                    { 9, "10", "Juros sobre capital próprio", true, 2, "", 0, "Jcp", null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 10, "06", "Rendimentos de aplicações financeiras", true, 2, "", 0, "RendaFixa", null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 11, "02", "Títulos públicos e privados sujeitos à tributação", true, 0, "04", 0, "RendaFixa", null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.InsertData(
                table: "RegrasFiscais",
                columns: new[] { "Id", "Codigo", "EhSistema", "Valor", "VigenciaFim", "VigenciaInicio" },
                values: new object[,]
                {
                    { 19, "RF.IrRegressivo.Automatico", true, 0m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 20, "RF.Jcp.GrossUp", true, 0m, null, new DateTime(1996, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 20);
        }
    }
}
