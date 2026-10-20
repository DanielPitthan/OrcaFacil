using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProvento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProventoId",
                table: "Transacoes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Proventos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtivoId = table.Column<int>(type: "INTEGER", nullable: false),
                    MembroFamiliaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    DataPagamento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ValorLiquido = table.Column<decimal>(type: "TEXT", precision: 18, scale: 2, nullable: false),
                    Origem = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 0),
                    HashImportacao = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proventos_AtivosFinanceiros_AtivoId",
                        column: x => x.AtivoId,
                        principalTable: "AtivosFinanceiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Proventos_MembrosFamilia_MembroFamiliaId",
                        column: x => x.MembroFamiliaId,
                        principalTable: "MembrosFamilia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_ProventoId",
                table: "Transacoes",
                column: "ProventoId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Proventos_AtivoId",
                table: "Proventos",
                column: "AtivoId");

            migrationBuilder.CreateIndex(
                name: "IX_Proventos_HashImportacao",
                table: "Proventos",
                column: "HashImportacao",
                unique: true,
                filter: "\"HashImportacao\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Proventos_MembroFamiliaId",
                table: "Proventos",
                column: "MembroFamiliaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_Proventos_ProventoId",
                table: "Transacoes",
                column: "ProventoId",
                principalTable: "Proventos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O SQLite só reconstrói Transacoes (e tira a FK) no fim do Down.
            // Sem desligar a checagem, o DROP TABLE de Proventos falha enquanto a FK existe.
            migrationBuilder.Sql("PRAGMA foreign_keys = OFF;", suppressTransaction: true);

            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_Proventos_ProventoId",
                table: "Transacoes");

            migrationBuilder.DropTable(
                name: "Proventos");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_ProventoId",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "ProventoId",
                table: "Transacoes");

            migrationBuilder.Sql("PRAGMA foreign_keys = ON;", suppressTransaction: true);
        }
    }
}
