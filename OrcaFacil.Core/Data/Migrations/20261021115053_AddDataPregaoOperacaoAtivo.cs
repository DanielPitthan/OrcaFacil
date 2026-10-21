using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrcaFacil.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDataPregaoOperacaoAtivo : Migration
    {
        /// <summary>
        /// Copia a data já gravada. No manual ela já é o pregão.
        /// Na importação B3 ela é a liquidação e o comando seguinte recua D+2.
        /// </summary>
        public const string CopiarDataParaPregao =
            """
            UPDATE "OperacoesAtivo" SET "DataPregao" = "Data";
            """;

        /// <summary>
        /// Pregão da importação B3 (Origem = 1): liquidação menos 2 dias úteis.
        /// O calendário é o mesmo de <c>CalendarioBancarioNacional</c>:
        /// fim de semana, feriados fixos, 20 de novembro a partir de 2024,
        /// Carnaval, Sexta-feira Santa e Corpus Christi.
        /// A #104 altera a regra da #30. A liquidação em Data não é reescrita.
        /// </summary>
        public const string RecuarPregaoImportado =
            """
            WITH RECURSIVE recuo AS (
              SELECT "Id" AS id, date("Data") AS dia, 0 AS uteis
              FROM "OperacoesAtivo"
              WHERE "Origem" = 1
              UNION ALL
              SELECT
                id,
                date(dia, '-1 day'),
                uteis + (
                  SELECT CASE
                    WHEN CAST(strftime('%w', cand) AS INTEGER) IN (0, 6) THEN 0
                    WHEN strftime('%m-%d', cand) IN ('01-01','04-21','05-01','09-07','10-12','11-02','11-15','12-25') THEN 0
                    WHEN CAST(strftime('%Y', cand) AS INTEGER) >= 2024 AND strftime('%m-%d', cand) = '11-20' THEN 0
                    WHEN cand = date(pascoa, '-48 days')
                      OR cand = date(pascoa, '-47 days')
                      OR cand = date(pascoa, '-2 days')
                      OR cand = date(pascoa, '+60 days') THEN 0
                    ELSE 1
                  END
                  FROM (
                    SELECT
                      date(dia, '-1 day') AS cand,
                      (
                        SELECT printf('%04d-%02d-%02d', ano, mes, dia_p)
                        FROM (
                          SELECT ano,
                                 (h + l - 7 * m + 114) / 31 AS mes,
                                 ((h + l - 7 * m + 114) % 31) + 1 AS dia_p
                          FROM (
                            SELECT ano, h, l, (a + 11 * h + 22 * l) / 451 AS m
                            FROM (
                              SELECT ano, a, h, (32 + 2 * e + 2 * i - h - k) % 7 AS l
                              FROM (
                                SELECT ano, a, e, i, k, (19 * a + b - d - g + 15) % 30 AS h
                                FROM (
                                  SELECT ano, a, b, d, e, i, k, (b - f + 1) / 3 AS g
                                  FROM (
                                    SELECT ano,
                                           ano % 19 AS a,
                                           ano / 100 AS b,
                                           (ano / 100) / 4 AS d,
                                           (ano / 100) % 4 AS e,
                                           ((ano / 100) + 8) / 25 AS f,
                                           (ano % 100) / 4 AS i,
                                           (ano % 100) % 4 AS k
                                    FROM (SELECT CAST(strftime('%Y', date(dia, '-1 day')) AS INTEGER) AS ano)
                                  )
                                )
                              )
                            )
                          )
                        )
                      ) AS pascoa
                  )
                )
              FROM recuo
              WHERE uteis < 2
                AND date(dia, '-1 day') > '1990-01-01'
            )
            UPDATE "OperacoesAtivo"
            SET "DataPregao" = (
              SELECT dia || ' 00:00:00'
              FROM recuo
              WHERE recuo.id = "OperacoesAtivo"."Id" AND recuo.uteis = 2
            )
            WHERE "Origem" = 1;
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataPregao",
                table: "OperacoesAtivo",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql(CopiarDataParaPregao);
            migrationBuilder.Sql(RecuarPregaoImportado);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataPregao",
                table: "OperacoesAtivo");
        }
    }
}
