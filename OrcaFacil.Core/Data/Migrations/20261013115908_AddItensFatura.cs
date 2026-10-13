using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddItensFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PermiteItensFatura",
                table: "Categorias",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ItensLancamento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    Natureza = table.Column<int>(type: "INTEGER", nullable: false),
                    ParcelaAtual = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalParcelas = table.Column<int>(type: "INTEGER", nullable: true),
                    Ordem = table.Column<int>(type: "INTEGER", nullable: false),
                    TransacaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    CategoriaId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensLancamento", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensLancamento_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensLancamento_Transacoes_TransacaoId",
                        column: x => x.TransacaoId,
                        principalTable: "Transacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensLancamento_CategoriaId",
                table: "ItensLancamento",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensLancamento_TransacaoId",
                table: "ItensLancamento",
                column: "TransacaoId");

            migrationBuilder.Sql(
                "UPDATE Categorias SET PermiteItensFatura = 1 WHERE Nome = 'Cartão de Crédito';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensLancamento");

            migrationBuilder.DropColumn(
                name: "PermiteItensFatura",
                table: "Categorias");
        }
    }
}
