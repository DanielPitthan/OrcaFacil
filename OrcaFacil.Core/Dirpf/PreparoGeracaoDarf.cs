using OrcaFacil.Core.Ativos;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// O ano anterior a 2005 não abre o diálogo e não gera arquivo.
/// Cancelar também não gera. Confirmar segue para gravar o IRRF e a planilha.
/// </summary>
public static class PreparoGeracaoDarf
{
    public static async Task<DecisaoGeracaoDarf> DecidirAsync(int anoBase, Func<Task<bool>> confirmar)
    {
        ArgumentNullException.ThrowIfNull(confirmar);
        if (anoBase < CatalogoRegrasFiscais.InicioLei11033.Year)
            return new DecisaoGeracaoDarf(false, ExportadorDarf.MensagemAntesDe2005);

        var confirmou = await confirmar();
        return new DecisaoGeracaoDarf(confirmou, null);
    }
}

public sealed record DecisaoGeracaoDarf(bool GerarArquivo, string? Aviso);
