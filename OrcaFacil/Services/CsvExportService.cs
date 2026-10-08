using System.Globalization;
using System.Text;
using OrcaFacil.Core.Entities;
using OrcaFacil.Services.Interfaces;

namespace OrcaFacil.Services;

public class CsvExportService : ICsvExportService
{
    private static readonly CultureInfo CulturaPtBr = new("pt-BR");

    public async Task<string> GerarCsvAsync(IEnumerable<Transacao> transacoes)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Descrição;Valor;Data;Tipo;Categoria;Membro;Recorrência;Observação");

        foreach (var t in transacoes)
        {
            sb.AppendLine(string.Join(';',
                EscaparCsv(t.Descricao),
                t.Valor.ToString("F2", CulturaPtBr),
                t.Data.ToString("dd/MM/yyyy"),
                t.Tipo.ToString(),
                EscaparCsv(t.Categoria?.Nome ?? string.Empty),
                EscaparCsv(t.MembroFamilia?.Nome ?? string.Empty),
                t.Recorrencia.ToString(),
                EscaparCsv(t.Observacao ?? string.Empty)));
        }

        var nomeArquivo = $"orcafacil-relatorio-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        var caminho = Path.Combine(FileSystem.CacheDirectory, nomeArquivo);
        await File.WriteAllTextAsync(caminho, sb.ToString(), new UTF8Encoding(true));
        return caminho;
    }

    public async Task CompartilharAsync(string caminhoArquivo)
    {
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Exportar Relatório - Pro Orçamento",
            File = new ShareFile(caminhoArquivo)
        });
    }

    private static string EscaparCsv(string valor)
    {
        if (valor.Contains(';') || valor.Contains('"') || valor.Contains('\n'))
            return $"\"{valor.Replace("\"", "\"\"")}\"";
        return valor;
    }
}
