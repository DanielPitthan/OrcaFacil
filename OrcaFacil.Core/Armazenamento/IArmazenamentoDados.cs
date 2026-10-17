namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// Coordenação do banco vivo e do espelho. Não desenha tela e não fala com o Android.
/// A limpeza seletiva que a tela de Configurações usa hoje continua no serviço do app.
/// </summary>
public interface IArmazenamentoDados
{
    /// <summary>
    /// Observa os dois lados, decide pela matriz e executa só o passo seguro.
    /// Falha de dados volta em <see cref="ResultadoArmazenamento"/>; não apaga arquivo e não semeia no lugar.
    /// </summary>
    Task<ResultadoArmazenamento> PrepararAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Pedido explícito de zerar. Remove o banco vivo e o espelho. Não é caminho de recuperação.
    /// </summary>
    Task<ResultadoLimpezaArmazenamento> LimparTudoAsync(PedidoLimparTudo pedido, CancellationToken cancellationToken = default);
}

/// <summary>
/// Prova de que a limpeza foi pedida de propósito. Não há conversão a partir de bool.
/// </summary>
public sealed class PedidoLimparTudo
{
    private PedidoLimparTudo()
    {
    }

    public static PedidoLimparTudo Explicito { get; } = new();
}

public sealed record ResultadoArmazenamento(
    AcaoArmazenamento Acao,
    AcaoArmazenamento AcaoPlanejada,
    EstadoObservadoArmazenamento? Estado,
    string Motivo,
    Exception? Falha);

public sealed record ResultadoLimpezaArmazenamento(bool BancoVivoRemovido, bool EspelhoRemovido);

/// <summary>Limpar tudo tentou os dois lados. A falha de um não impede a tentativa do outro.</summary>
public sealed class LimpezaTotalException : Exception
{
    public LimpezaTotalException(Exception? falhaBancoVivo, Exception? falhaEspelho)
        : base(MontarMensagem(falhaBancoVivo, falhaEspelho), Combinar(falhaBancoVivo, falhaEspelho))
    {
        FalhaBancoVivo = falhaBancoVivo;
        FalhaEspelho = falhaEspelho;
    }

    public Exception? FalhaBancoVivo { get; }

    public Exception? FalhaEspelho { get; }

    private static string MontarMensagem(Exception? falhaBancoVivo, Exception? falhaEspelho)
    {
        if (falhaBancoVivo is not null && falhaEspelho is not null)
            return "Limpar tudo não removeu o banco vivo nem o espelho.";
        if (falhaBancoVivo is not null)
            return "Limpar tudo não removeu o banco vivo. A remoção do espelho foi tentada.";
        return "Limpar tudo não removeu o espelho.";
    }

    private static Exception? Combinar(Exception? falhaBancoVivo, Exception? falhaEspelho)
    {
        if (falhaBancoVivo is not null && falhaEspelho is not null)
            return new AggregateException(falhaBancoVivo, falhaEspelho);
        return falhaBancoVivo ?? falhaEspelho;
    }
}
