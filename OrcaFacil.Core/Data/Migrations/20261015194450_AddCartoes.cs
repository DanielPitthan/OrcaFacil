using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCartoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CartaoId",
                table: "Transacoes",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CartaoBeneficioId",
                table: "ComprasCesta",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CartaoCreditoId",
                table: "ComprasCesta",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ItemFaturaId",
                table: "ComprasCesta",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Cartoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "NOCASE"),
                    Bandeira = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    UltimosDigitos = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoBeneficio = table.Column<int>(type: "INTEGER", nullable: true),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cartoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecargasCartao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CartaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecargasCartao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecargasCartao_Cartoes_CartaoId",
                        column: x => x.CartaoId,
                        principalTable: "Cartoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Transacoes_CartaoId",
                table: "Transacoes",
                column: "CartaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasCesta_CartaoBeneficioId",
                table: "ComprasCesta",
                column: "CartaoBeneficioId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasCesta_CartaoCreditoId",
                table: "ComprasCesta",
                column: "CartaoCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasCesta_ItemFaturaId",
                table: "ComprasCesta",
                column: "ItemFaturaId");

            migrationBuilder.CreateIndex(
                name: "IX_Cartoes_Nome",
                table: "Cartoes",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecargasCartao_CartaoId_Data",
                table: "RecargasCartao",
                columns: new[] { "CartaoId", "Data" });

            migrationBuilder.AddForeignKey(
                name: "FK_ComprasCesta_Cartoes_CartaoBeneficioId",
                table: "ComprasCesta",
                column: "CartaoBeneficioId",
                principalTable: "Cartoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ComprasCesta_Cartoes_CartaoCreditoId",
                table: "ComprasCesta",
                column: "CartaoCreditoId",
                principalTable: "Cartoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ComprasCesta_ItensLancamento_ItemFaturaId",
                table: "ComprasCesta",
                column: "ItemFaturaId",
                principalTable: "ItensLancamento",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Transacoes_Cartoes_CartaoId",
                table: "Transacoes",
                column: "CartaoId",
                principalTable: "Cartoes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ComprasCesta_Cartoes_CartaoBeneficioId",
                table: "ComprasCesta");

            migrationBuilder.DropForeignKey(
                name: "FK_ComprasCesta_Cartoes_CartaoCreditoId",
                table: "ComprasCesta");

            migrationBuilder.DropForeignKey(
                name: "FK_ComprasCesta_ItensLancamento_ItemFaturaId",
                table: "ComprasCesta");

            migrationBuilder.DropForeignKey(
                name: "FK_Transacoes_Cartoes_CartaoId",
                table: "Transacoes");

            migrationBuilder.DropTable(
                name: "RecargasCartao");

            migrationBuilder.DropTable(
                name: "Cartoes");

            migrationBuilder.DropIndex(
                name: "IX_Transacoes_CartaoId",
                table: "Transacoes");

            migrationBuilder.DropIndex(
                name: "IX_ComprasCesta_CartaoBeneficioId",
                table: "ComprasCesta");

            migrationBuilder.DropIndex(
                name: "IX_ComprasCesta_CartaoCreditoId",
                table: "ComprasCesta");

            migrationBuilder.DropIndex(
                name: "IX_ComprasCesta_ItemFaturaId",
                table: "ComprasCesta");

            migrationBuilder.DropColumn(
                name: "CartaoId",
                table: "Transacoes");

            migrationBuilder.DropColumn(
                name: "CartaoBeneficioId",
                table: "ComprasCesta");

            migrationBuilder.DropColumn(
                name: "CartaoCreditoId",
                table: "ComprasCesta");

            migrationBuilder.DropColumn(
                name: "ItemFaturaId",
                table: "ComprasCesta");
        }
    }
}
