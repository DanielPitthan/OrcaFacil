using System.Text;

namespace OrcaFacil.Core.Cesta;

public static class ProdutoCestaCsv
{
    public sealed record Linha(int Numero, string Nome, string? Categoria);

    public sealed record Leitura(IReadOnlyList<Linha> Linhas, IReadOnlyList<string> Erros);

    public static string Decodificar(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);

        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(1252).GetString(bytes);
        }
    }

    public static Leitura Ler(string conteudo)
    {
        var linhas = new List<Linha>();
        var erros = new List<string>();
        var texto = conteudo.Replace("\r\n", "\n").Replace('\r', '\n');
        var partes = texto.Split('\n');
        var separador = DetectarSeparador(partes);
        var cabecalho = true;

        for (var i = 0; i < partes.Length; i++)
        {
            var bruta = partes[i];
            if (string.IsNullOrWhiteSpace(bruta))
                continue;

            var numero = i + 1;
            var campos = Separar(bruta, separador);
            if (cabecalho && EhCabecalho(campos))
            {
                cabecalho = false;
                continue;
            }

            cabecalho = false;
            var nome = campos.Count > 0 ? campos[0].Trim() : "";
            var categoria = campos.Count > 1 ? campos[1].Trim() : "";

            if (string.IsNullOrWhiteSpace(nome))
            {
                erros.Add($"Linha {numero}: informe o nome do produto.");
                continue;
            }

            if (nome.Length > 80)
            {
                erros.Add($"Linha {numero}: o nome pode ter no máximo 80 caracteres.");
                continue;
            }

            linhas.Add(new Linha(numero, nome, string.IsNullOrWhiteSpace(categoria) ? null : categoria));
        }

        if (linhas.Count == 0 && erros.Count == 0)
            erros.Add("O arquivo não tem produtos.");

        return new Leitura(linhas, erros);
    }

    private static bool EhCabecalho(IReadOnlyList<string> campos)
    {
        var primeiro = campos.Count > 0 ? campos[0].Trim() : "";
        return primeiro.Equals("produto", StringComparison.OrdinalIgnoreCase)
            || primeiro.Equals("nome", StringComparison.OrdinalIgnoreCase)
            || primeiro.Equals("descrição", StringComparison.OrdinalIgnoreCase)
            || primeiro.Equals("descricao", StringComparison.OrdinalIgnoreCase);
    }

    private static char DetectarSeparador(IReadOnlyList<string> partes)
    {
        foreach (var parte in partes)
        {
            if (string.IsNullOrWhiteSpace(parte))
                continue;

            var pontoEVirgula = parte.Count(c => c == ';');
            var virgula = parte.Count(c => c == ',');
            if (pontoEVirgula == 0 && virgula == 0)
                return ';';

            return pontoEVirgula >= virgula ? ';' : ',';
        }

        return ';';
    }

    private static List<string> Separar(string linha, char separador)
    {
        var campos = new List<string>();
        var atual = new StringBuilder();
        var aspas = false;

        for (var i = 0; i < linha.Length; i++)
        {
            var c = linha[i];
            if (c == '"')
            {
                if (aspas && i + 1 < linha.Length && linha[i + 1] == '"')
                {
                    atual.Append('"');
                    i++;
                }
                else
                {
                    aspas = !aspas;
                }
            }
            else if (c == separador && !aspas)
            {
                campos.Add(atual.ToString());
                atual.Clear();
            }
            else
            {
                atual.Append(c);
            }
        }

        campos.Add(atual.ToString());
        return campos;
    }
}
