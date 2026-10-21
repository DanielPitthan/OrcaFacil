using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVarianteFiagroDirpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CodigosDirpf_Ficha_TipoAtivo_VigenciaInicio",
                table: "CodigosDirpf");

            migrationBuilder.AddColumn<string>(
                name: "Variante",
                table: "CodigosDirpf",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 1,
                column: "Variante",
                value: "");

            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 2,
                column: "Variante",
                value: "");

            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 3,
                column: "Variante",
                value: "");

            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 4,
                column: "Variante",
                value: "");

            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 5,
                column: "Variante",
                value: "");

            migrationBuilder.UpdateData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 6,
                column: "Variante",
                value: "");

            migrationBuilder.InsertData(
                table: "CodigosDirpf",
                columns: new[] { "Id", "Codigo", "Descricao", "EhSistema", "Ficha", "Grupo", "TipoAtivo", "Variante", "VigenciaFim", "VigenciaInicio" },
                values: new object[] { 7, "02", "Fundos de Investimento nas Cadeias Produtivas Agroindustriais (Fiagro)", true, 0, "07", 1, "Fiagro", null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) });

            migrationBuilder.CreateIndex(
                name: "IX_CodigosDirpf_Ficha_TipoAtivo_Variante_VigenciaInicio",
                table: "CodigosDirpf",
                columns: new[] { "Ficha", "TipoAtivo", "Variante", "VigenciaInicio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CodigosDirpf_Ficha_TipoAtivo_Variante_VigenciaInicio",
                table: "CodigosDirpf");

            migrationBuilder.DeleteData(
                table: "CodigosDirpf",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DropColumn(
                name: "Variante",
                table: "CodigosDirpf");

            migrationBuilder.CreateIndex(
                name: "IX_CodigosDirpf_Ficha_TipoAtivo_VigenciaInicio",
                table: "CodigosDirpf",
                columns: new[] { "Ficha", "TipoAtivo", "VigenciaInicio" },
                unique: true);
        }
    }
}
