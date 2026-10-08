using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPagoToTransacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Lançamentos já existentes (antes desta feature) são tratados como resolvidos/pagos;
            // só novos lançamentos de despesa nascem com Pago = false por padrão (ver TransacaoFormModel).
            migrationBuilder.AddColumn<bool>(
                name: "Pago",
                table: "Transacoes",
                type: "INTEGER",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Pago",
                table: "Transacoes");
        }
    }
}
