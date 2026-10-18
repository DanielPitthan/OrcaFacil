using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEhEstornoItemFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Default false no ADD COLUMN: linhas antigas continuam iguais, sem UPDATE nem recriação da tabela.
            migrationBuilder.AddColumn<bool>(
                name: "EhEstorno",
                table: "ItensLancamento",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // O lançamento já abateu a magnitude em Transacoes.Valor. Sem a coluna, a linha
            // volta a ser compra e o total ficaria baixo. A magnitude do estorno volta antes do drop.
            migrationBuilder.Sql(
                """
                UPDATE Transacoes
                SET Valor = ROUND(CAST(Valor AS REAL) + (
                    SELECT COALESCE(SUM(CAST(i.Valor AS REAL)), 0)
                    FROM ItensLancamento AS i
                    WHERE i.TransacaoId = Transacoes.Id AND i.EhEstorno <> 0
                ), 2)
                WHERE EXISTS (
                    SELECT 1
                    FROM ItensLancamento AS i
                    WHERE i.TransacaoId = Transacoes.Id AND i.EhEstorno <> 0
                );
                """);

            migrationBuilder.DropColumn(
                name: "EhEstorno",
                table: "ItensLancamento");
        }
    }
}
