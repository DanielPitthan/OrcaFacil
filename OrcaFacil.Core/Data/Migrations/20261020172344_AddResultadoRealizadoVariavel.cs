using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddResultadoRealizadoVariavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OperacaoAtivoId",
                table: "Transacoes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HashImportacao",
                table: "OperacoesAtivo",
                type: "TEXT",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Origem",
                table: "OperacoesAtivo",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "RegrasFiscais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    VigenciaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VigenciaFim = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EhSistema = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegrasFiscais", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RegrasFiscais",
                columns: new[] { "Id", "Codigo", "EhSistema", "Valor", "VigenciaFim", "VigenciaInicio" },
                values: new object[,]
                {
                    { 1, "RV.CustosReduzemResultado", true, 1m, null, new DateTime(1995, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, "RV.CasasDecimaisResultado", true, 2m, null, new DateTime(1995, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, "RV.Aliquota.Acao", true, 0.15m, null, new DateTime(1995, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 4, "RV.Aliquota.Fii", true, 0.20m, null, new DateTime(1995, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 5, "RV.Aliquota.Etf", true, 0.15m, null, new DateTime(1995, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 6, "RV.Aliquota.Bdr", true, 0.15m, null, new DateTime(1995, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_OperacaoAtivoId",
                table: "Transacoes",
                column: "OperacaoAtivoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesAtivo_HashImportacao",
                table: "OperacoesAtivo",
                column: "HashImportacao",
                unique: true,
                filter: "\"HashImportacao\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RegrasFiscais_Codigo_VigenciaInicio",
                table: "RegrasFiscais",
                columns: new[] { "Codigo", "VigenciaInicio" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_OperacoesAtivo_OperacaoAtivoId",
                table: "Transacoes",
                column: "OperacaoAtivoId",
                principalTable: "OperacoesAtivo",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_OperacoesAtivo_OperacaoAtivoId",
                table: "Transacoes");

            migrationBuilder.DropTable(
                name: "RegrasFiscais");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_OperacaoAtivoId",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_OperacoesAtivo_HashImportacao",
                table: "OperacoesAtivo");

            migrationBuilder.DropColumn(
                name: "OperacaoAtivoId",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "HashImportacao",
                table: "OperacoesAtivo");

            migrationBuilder.DropColumn(
                name: "Origem",
                table: "OperacoesAtivo");
        }
    }
}
