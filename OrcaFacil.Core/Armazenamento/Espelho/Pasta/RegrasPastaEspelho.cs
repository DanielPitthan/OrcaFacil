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
}
