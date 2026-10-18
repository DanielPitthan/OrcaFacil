namespace OrcaFacil.Core.Armazenamento.Espelho.Pasta;

/// <summary>
/// O que a pasta do usuário pode sofrer nesta troca. Criar e apagar só valem para o
/// temporário desta publicação. A reserva — o espelho anterior renomeado — só sai
/// depois que o nome definitivo já tem o arquivo novo.
/// </summary>
public static class RegrasPastaEspelho
{
    public static CodigoFalhaPastaEspelho? ImpedirCriacao(string nomeFinal, string nome)
    {
        if (!NomeTemporarioEspelho.EhTemporarioDesteApp(nome, nomeFinal))
            return CodigoFalhaPastaEspelho.NomeRecusado;

        return null;
    }

    public static CodigoFalhaPastaEspelho? ImpedirDescarteTemporario(string nomeFinal, string nome)
    {
        if (!NomeTemporarioEspelho.EhTemporarioDesteApp(nome, nomeFinal))
            return CodigoFalhaPastaEspelho.NomeRecusado;

        return null;
    }

    public static CodigoFalhaPastaEspelho? ImpedirDescarteReserva(string nomeFinal, string nomeReserva, bool arquivoFinalPresente)
    {
        if (!NomeTemporarioEspelho.EhReservaDesteApp(nomeReserva, nomeFinal))
            return CodigoFalhaPastaEspelho.NomeRecusado;

        if (!arquivoFinalPresente)
            return CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome;

        return null;
    }

    public static bool PodeRenomear(string nomeFinal, string nomeAtual, string nomeNovo)
        => NomeTemporarioEspelho.EhNomeDesteApp(nomeAtual, nomeFinal)
            && NomeTemporarioEspelho.EhNomeDesteApp(nomeNovo, nomeFinal)
            && !string.Equals(nomeAtual, nomeNovo, StringComparison.Ordinal);

    /// <summary>
    /// <c>DocumentsContract.Document.FLAG_SUPPORTS_RENAME</c> (1 &lt;&lt; 6).
    /// </summary>
    public const int FlagSupportsRename = 1 << 6;

    /// <summary>
    /// <c>ColumnFlags</c> nula ou sem o bit de rename recusa a troca antes de
    /// <c>RenameDocument</c>.
    /// </summary>
    public static CodigoFalhaPastaEspelho? ImpedirRenameSemFlag(int? columnFlags)
    {
        if (columnFlags is null || (columnFlags.Value & FlagSupportsRename) == 0)
            return CodigoFalhaPastaEspelho.ProvedorSemTroca;

        return null;
    }
}
