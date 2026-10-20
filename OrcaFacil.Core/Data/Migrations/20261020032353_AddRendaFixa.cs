using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRendaFixa : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Coluna nula: lançamentos já gravados ficam sem vínculo de renda fixa.
            migrationBuilder.AddColumn<int>(
                name: "OperacaoRendaFixaId",
                table: "Transacoes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TitulosRendaFixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "NOCASE"),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Emissor = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    AnoVencimento = table.Column<int>(type: "INTEGER", nullable: true),
                    Corretora = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false, collation: "NOCASE"),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TitulosRendaFixa", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacoesRendaFixa",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TituloRendaFixaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantidade = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "TEXT", precision: 18, scale: 8, nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Custos = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    IrRetido = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Origem = table.Column<int>(type: "INTEGER", nullable: false),
                    HashImportacao = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesRendaFixa", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperacoesRendaFixa_TitulosRendaFixa_TituloRendaFixaId",
                        column: x => x.TituloRendaFixaId,
                        principalTable: "TitulosRendaFixa",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_OperacaoRendaFixaId",
                table: "Transacoes",
                column: "OperacaoRendaFixaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesRendaFixa_HashImportacao",
                table: "OperacoesRendaFixa",
                column: "HashImportacao",
                unique: true,
                filter: "\"HashImportacao\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesRendaFixa_TituloRendaFixaId",
                table: "OperacoesRendaFixa",
                column: "TituloRendaFixaId");

            migrationBuilder.CreateIndex(
                name: "IX_TitulosRendaFixa_Codigo_Corretora",
                table: "TitulosRendaFixa",
                columns: new[] { "Codigo", "Corretora" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_OperacoesRendaFixa_OperacaoRendaFixaId",
                table: "Transacoes",
                column: "OperacaoRendaFixaId",
                principalTable: "OperacoesRendaFixa",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No SQLite o DropForeignKey só entra no rebuild do DropColumn,
            // e esse rebuild sai depois do DropTable. Anular o vínculo agora
            // solta a FK antes de apagar OperacoesRendaFixa.
            migrationBuilder.Sql("""UPDATE "Transacoes" SET "OperacaoRendaFixaId" = NULL;""");

            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_OperacoesRendaFixa_OperacaoRendaFixaId",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_OperacaoRendaFixaId",
                table: "Transacoes");

            migrationBuilder.DropTable(
                name: "OperacoesRendaFixa");

            migrationBuilder.DropTable(
                name: "TitulosRendaFixa");

            migrationBuilder.DropColumn(
                name: "OperacaoRendaFixaId",
                table: "Transacoes");
        }
    }
}
