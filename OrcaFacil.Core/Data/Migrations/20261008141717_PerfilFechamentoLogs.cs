using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PerfilFechamentoLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Apelido",
                table: "MembrosFamilia",
                type: "TEXT",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FotoPath",
                table: "MembrosFamilia",
                type: "TEXT",
                maxLength: 260,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FechamentosPeriodo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Mes = table.Column<int>(type: "INTEGER", nullable: false),
                    Ano = table.Column<int>(type: "INTEGER", nullable: false),
                    Fechado = table.Column<bool>(type: "INTEGER", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FechamentosPeriodo", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LogsEvento",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Mensagem = table.Column<string>(type: "TEXT", maxLength: 300, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LogsEvento", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FechamentosPeriodo_Mes_Ano",
                table: "FechamentosPeriodo",
                columns: new[] { "Mes", "Ano" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LogsEvento_CriadoEm",
                table: "LogsEvento",
                column: "CriadoEm");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FechamentosPeriodo");

            migrationBuilder.DropTable(
                name: "LogsEvento");

            migrationBuilder.DropColumn(
                name: "Apelido",
                table: "MembrosFamilia");

            migrationBuilder.DropColumn(
                name: "FotoPath",
                table: "MembrosFamilia");
        }
    }
}
