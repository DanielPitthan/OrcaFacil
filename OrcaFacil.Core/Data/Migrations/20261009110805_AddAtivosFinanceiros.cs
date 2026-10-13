using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAtivosFinanceiros : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AtivosFinanceiros",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticker = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false, collation: "NOCASE"),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtivosFinanceiros", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OperacoesAtivo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    AtivoId = table.Column<int>(type: "INTEGER", nullable: false),
                    MembroFamiliaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Quantidade = table.Column<decimal>(type: "TEXT", nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "TEXT", nullable: false),
                    Custos = table.Column<decimal>(type: "TEXT", nullable: false),
                    Corretora = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesAtivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OperacoesAtivo_AtivosFinanceiros_AtivoId",
                        column: x => x.AtivoId,
                        principalTable: "AtivosFinanceiros",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesAtivo_MembrosFamilia_MembroFamiliaId",
                        column: x => x.MembroFamiliaId,
                        principalTable: "MembrosFamilia",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AtivosFinanceiros_Ticker",
                table: "AtivosFinanceiros",
                column: "Ticker",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesAtivo_AtivoId_MembroFamiliaId_Data",
                table: "OperacoesAtivo",
                columns: new[] { "AtivoId", "MembroFamiliaId", "Data" });

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesAtivo_MembroFamiliaId",
                table: "OperacoesAtivo",
                column: "MembroFamiliaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperacoesAtivo");

            migrationBuilder.DropTable(
                name: "AtivosFinanceiros");
        }
    }
}
