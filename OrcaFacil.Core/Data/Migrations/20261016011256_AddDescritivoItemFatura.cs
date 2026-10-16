using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDescritivoItemFatura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Descritivo",
                table: "ItensLancamento",
                type: "TEXT",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Descritivo",
                table: "ItensLancamento");
        }
    }
}
