namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Uma queda entre os dois <see cref="File.Move"/> deixa o banco só com o nome de reserva.
/// Na partida seguinte o arquivo volta ao lugar. Se não voltar, a reserva continua no disco
/// e o banco não pode parecer instalação nova.
/// </summary>
internal static class ResiduoTrocaBanco
{
    public static void TentarRecompor(string caminhoBanco)
    {
        if (File.Exists(caminhoBanco) || Directory.Exists(caminhoBanco))
            return;

        var pasta = Path.GetDirectoryName(Path.GetFullPath(caminhoBanco));
        var nome = Path.GetFileName(caminhoBanco);
        if (string.IsNullOrEmpty(pasta) || string.IsNullOrEmpty(nome) || !Directory.Exists(pasta))
            return;

        foreach (var sufixo in ArquivosBancoVivo.Sufixos)
        {
            var destino = caminhoBanco + sufixo;
            if (File.Exists(destino))
                continue;

            var escolhida = ReservaMaisNova(pasta, nome + sufixo + ".reserva-");
            if (escolhida is null)
                continue;

            File.Move(escolhida, destino);
        }
    }

    public static bool HaReserva(string caminhoBanco)
    {
        var pasta = Path.GetDirectoryName(Path.GetFullPath(caminhoBanco));
        var nome = Path.GetFileName(caminhoBanco);
        if (string.IsNullOrEmpty(pasta) || string.IsNullOrEmpty(nome) || !Directory.Exists(pasta))
            return false;

        foreach (var sufixo in ArquivosBancoVivo.Sufixos)
        {
            if (ReservaMaisNova(pasta, nome + sufixo + ".reserva-") is not null)
                return true;
        }

        return false;
    }

    private static string? ReservaMaisNova(string pasta, string prefixo)
    {
        string? escolhida = null;
        var maisNova = DateTime.MinValue;
        foreach (var arquivo in Directory.EnumerateFiles(pasta, prefixo + "*"))
        {
            var nome = Path.GetFileName(arquivo);
            if (!nome.StartsWith(prefixo, StringComparison.Ordinal))
                continue;

            var guid = nome[prefixo.Length..];
            if (guid.Length != 32 || !guid.All(Uri.IsHexDigit))
                continue;

            var quando = File.GetLastWriteTimeUtc(arquivo);
            if (escolhida is null || quando >= maisNova)
            {
                escolhida = arquivo;
                maisNova = quando;
            }
        }

        return escolhida;
    }
}
