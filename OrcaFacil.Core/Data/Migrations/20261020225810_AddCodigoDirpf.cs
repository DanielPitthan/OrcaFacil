using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodigoDirpf : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CodigosDirpf",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ficha = table.Column<int>(type: "INTEGER", nullable: false),
                    TipoAtivo = table.Column<int>(type: "INTEGER", nullable: false),
                    Grupo = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Codigo = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    VigenciaInicio = table.Column<DateTime>(type: "TEXT", nullable: false),
                    VigenciaFim = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EhSistema = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosDirpf", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CodigosDirpf",
                columns: new[] { "Id", "Codigo", "Descricao", "EhSistema", "Ficha", "Grupo", "TipoAtivo", "VigenciaFim", "VigenciaInicio" },
                values: new object[,]
                {
                    { 1, "01", "Ações (inclusive as listadas em bolsa)", true, 0, "03", 0, null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 2, "03", "Fundos de Investimento Imobiliário (FII)", true, 0, "07", 1, null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 3, "09", "Demais fundos de índice de mercado (ETF)", true, 0, "07", 2, null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 4, "04", "Ativos negociados em bolsa no Brasil (BDRs, opções e outros, exceto ações e fundos)", true, 0, "04", 3, null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 5, "09", "Lucros e dividendos recebidos", true, 1, "", 0, null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) },
                    { 6, "99", "Outros", true, 1, "", 1, null, new DateTime(2021, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodigosDirpf_Ficha_TipoAtivo_VigenciaInicio",
                table: "CodigosDirpf",
                columns: new[] { "Ficha", "TipoAtivo", "VigenciaInicio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosDirpf");
        }
    }
}
