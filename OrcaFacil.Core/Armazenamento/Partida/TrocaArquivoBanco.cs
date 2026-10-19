using Microsoft.Data.Sqlite;
using OrcaFacil.Core.Armazenamento;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Coloca o SQLite já validado no lugar do banco vivo.
/// Antes de renomear um banco que já existe, copia o arquivo e os laterais para <c>backups</c>
/// e confere tamanho e leitura. Sem essa cópia, a troca não começa.
/// Se a troca não termina, o arquivo anterior volta ao nome de origem.
/// Não apaga backup, espelho nem o banco que já estava no aparelho.
/// O único descarte é o rascunho desta tentativa, que ainda não é o banco vivo,
/// e a cópia que não chegou a ser confirmada.
/// </summary>
internal static class TrocaArquivoBanco
{
    public static ResultadoRestauracao Substituir(
        string destino,
        string origemValidada,
        Func<string, bool>? sonda = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destino);
        ArgumentException.ThrowIfNullOrWhiteSpace(origemValidada);

        string? rascunho = null;
        var afastados = new List<(string Original, string Reserva)>();
        var copias = new List<string>();
        var copiaConfirmada = false;
        var rascunhoVirouBanco = false;
        try
        {
            SqliteConnection.ClearAllPools();
            var pasta = Path.GetDirectoryName(Path.GetFullPath(destino));
            if (string.IsNullOrEmpty(pasta))
                return ResultadoRestauracao.Falha("A restauração não concluiu. Nenhum arquivo foi apagado.");

            Directory.CreateDirectory(pasta);
            rascunho = Path.Combine(pasta, Path.GetFileName(destino) + ".restauracao-" + Guid.NewGuid().ToString("N"));
            File.Copy(origemValidada, rascunho, overwrite: false);

            var inventario = ArquivosBancoVivo.Inventariar(destino, sonda);
            if (inventario.Arquivos.Count > 0)
            {
                var backups = CopiaConfirmada.GarantirPasta(destino, sonda);
                CopiaConfirmada.Copiar(inventario, backups, "orcafacil-antes-troca", copias, CancellationToken.None);
                copiaConfirmada = true;
            }

            foreach (var arquivo in inventario.Arquivos)
            {
                var reserva = arquivo.Caminho + ".reserva-" + Guid.NewGuid().ToString("N");
                File.Move(arquivo.Caminho, reserva);
                afastados.Add((arquivo.Caminho, reserva));
            }

            File.Move(rascunho, destino);
            rascunhoVirouBanco = true;
            rascunho = null;
            GuardarReservasSemApagar(destino, afastados);
            return ResultadoRestauracao.Ok();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!rascunhoVirouBanco)
                Devolver(afastados);

            ApagarRascunho(rascunho);
            if (!copiaConfirmada)
                CopiaConfirmada.ApagarIncompletos(copias);

            return ResultadoRestauracao.Falha("A restauração não concluiu. Nenhum arquivo foi apagado.");
        }
    }

    private static void Devolver(List<(string Original, string Reserva)> afastados)
    {
        for (var i = afastados.Count - 1; i >= 0; i--)
        {
            var (original, reserva) = afastados[i];
            try
            {
                if (!File.Exists(original) && File.Exists(reserva))
                    File.Move(reserva, original);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    /// <summary>
    /// Tira o arquivo antigo do nome que o SQLite abriria como lateral.
    /// Se a pasta de backups não aceitar a cópia, o arquivo fica com o nome de reserva. Não é apagado.
    /// </summary>
    private static void GuardarReservasSemApagar(string destino, List<(string Original, string Reserva)> afastados)
    {
        if (afastados.Count == 0)
            return;

        var pastaBanco = Path.GetDirectoryName(Path.GetFullPath(destino));
        string? backups = null;
        if (!string.IsNullOrEmpty(pastaBanco))
        {
            var candidata = Path.Combine(pastaBanco, "backups");
            if (!ArquivosBancoVivo.ExisteArquivo(candidata))
            {
                try
                {
                    Directory.CreateDirectory(candidata);
                    backups = candidata;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
            else if (Directory.Exists(candidata))
            {
                backups = candidata;
            }
        }

        foreach (var (_, reserva) in afastados)
        {
            if (backups is null || !File.Exists(reserva))
                continue;

            try
            {
                var alvo = Path.Combine(backups, Path.GetFileName(reserva));
                if (!File.Exists(alvo))
                    File.Move(reserva, alvo);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void ApagarRascunho(string? rascunho)
    {
        if (string.IsNullOrEmpty(rascunho))
            return;

        try
        {
            if (File.Exists(rascunho))
                File.Delete(rascunho);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
