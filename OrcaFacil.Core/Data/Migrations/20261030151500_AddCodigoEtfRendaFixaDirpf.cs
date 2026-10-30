using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OrcaFacil.Core.Data;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AppDbContext))]
    [Migration("20261030151500_AddCodigoEtfRendaFixaDirpf")]
    public class AddCodigoEtfRendaFixaDirpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                INSERT INTO CodigosDirpf (Id, Codigo, Descricao, EhSistema, Ficha, Grupo, TipoAtivo, Variante, VigenciaFim, VigenciaInicio)
                VALUES
                    (14, '08', 'Fundos de Índice de Renda Fixa', 1, 0, '07', 2, 'EtfRendaFixa', NULL, '2021-01-01 00:00:00'),
                    (15, '06', 'Rendimentos de aplicações financeiras', 1, 2, '', 2, 'EtfRendaFixa', NULL, '2021-01-01 00:00:00');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM CodigosDirpf WHERE Id IN (14, 15);");
        }
    }
}
