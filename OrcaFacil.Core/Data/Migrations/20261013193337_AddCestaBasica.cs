using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCestaBasica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocaisCompra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "NOCASE"),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocaisCompra", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProdutosCesta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false, collation: "NOCASE"),
                    CategoriaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProdutosCesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProdutosCesta_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ComprasCesta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mes = table.Column<int>(type: "INTEGER", nullable: false),
                    Ano = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LocalCompraId = table.Column<int>(type: "INTEGER", nullable: false),
                    ValorBeneficio = table.Column<decimal>(type: "TEXT", nullable: false),
                    TipoCartaoBeneficio = table.Column<int>(type: "INTEGER", nullable: true),
                    TransacaoId = table.Column<int>(type: "INTEGER", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComprasCesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComprasCesta_LocaisCompra_LocalCompraId",
                        column: x => x.LocalCompraId,
                        principalTable: "LocaisCompra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComprasCesta_Transacoes_TransacaoId",
                        column: x => x.TransacaoId,
                        principalTable: "Transacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ItensCompraCesta",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CompraCestaId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProdutoCestaId = table.Column<int>(type: "INTEGER", nullable: false),
                    Preco = table.Column<decimal>(type: "TEXT", nullable: false),
                    Ordem = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensCompraCesta", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensCompraCesta_ComprasCesta_CompraCestaId",
                        column: x => x.CompraCestaId,
                        principalTable: "ComprasCesta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ItensCompraCesta_ProdutosCesta_ProdutoCestaId",
                        column: x => x.ProdutoCestaId,
                        principalTable: "ProdutosCesta",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasCesta_Ano_Mes_LocalCompraId",
                table: "ComprasCesta",
                columns: new[] { "Ano", "Mes", "LocalCompraId" });

            migrationBuilder.CreateIndex(
                name: "IX_ComprasCesta_LocalCompraId",
                table: "ComprasCesta",
                column: "LocalCompraId");

            migrationBuilder.CreateIndex(
                name: "IX_ComprasCesta_TransacaoId",
                table: "ComprasCesta",
                column: "TransacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensCompraCesta_CompraCestaId_ProdutoCestaId",
                table: "ItensCompraCesta",
                columns: new[] { "CompraCestaId", "ProdutoCestaId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensCompraCesta_ProdutoCestaId",
                table: "ItensCompraCesta",
                column: "ProdutoCestaId");

            migrationBuilder.CreateIndex(
                name: "IX_LocaisCompra_Nome",
                table: "LocaisCompra",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosCesta_CategoriaId",
                table: "ProdutosCesta",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ProdutosCesta_Nome",
                table: "ProdutosCesta",
                column: "Nome",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensCompraCesta");

            migrationBuilder.DropTable(
                name: "ComprasCesta");

            migrationBuilder.DropTable(
                name: "ProdutosCesta");

            migrationBuilder.DropTable(
                name: "LocaisCompra");
        }
    }
}
