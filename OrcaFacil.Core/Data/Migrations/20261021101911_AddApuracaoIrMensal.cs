using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApuracaoIrMensal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "CodigosDirpf",
                columns: new[] { "Id", "Codigo", "Descricao", "EhSistema", "Ficha", "Grupo", "TipoAtivo", "Variante", "VigenciaFim", "VigenciaInicio" },
                values: new object[] { 8, "20", "Ganhos líquidos em operações no mercado à vista de ações negociadas em bolsas de valores nas alienações realizadas até R$ 20.000,00 em cada mês, para o conjunto de ações", true, 1, "", 0, "GanhoIsento", null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.InsertData(
                table: "RegrasFiscais",
                columns: new[] { "Id", "Codigo", "EhSistema", "Valor", "VigenciaFim", "VigenciaInicio" },
                values: new object[,]
                {
                    { 7, "RV.Aliquota.DayTrade", true, 0.20m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 8, "RV.Isencao.VendasAcoes", true, 20000m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 9, "RV.Darf.Codigo", true, 6015m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 10, "RV.Darf.ValorMinimo", true, 10m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 11, "RV.Darf.AcumularAbaixoDoMinimo", true, 1m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 12, "RV.Prejuizo.CompensaEntreAnos", true, 0m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 13, "RV.Prejuizo.CompensaNoMesIsento", true, 0m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 14, "RV.Irrf.Estimar", true, 0m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 15, "RV.Irrf.AliquotaComum", true, 0.00005m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 16, "RV.Irrf.AliquotaDayTrade", true, 0.01m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 17, "RV.Irrf.ValorMinimoRetencao", true, 1m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 18, "RV.Irrf.CompensaMesesSeguintes", true, 0m, null, new DateTime(2005, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "RegrasFiscais",
                keyColumn: "Id",
                keyValue: 18);
        }
    }
}
