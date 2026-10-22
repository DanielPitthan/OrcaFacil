namespace OrcaFacil.Core.Dirpf;

/// <summary>Uma DIRPF de um ano-base. Cada perfil é uma linha própria: os custos não se somam.</summary>
public sealed class DirpfAnoBase
{
    public required int Ano { get; init; }

    public IReadOnlyList<LinhaBemDirpf> Bens { get; init; } = [];

    public IReadOnlyList<LinhaRendimentoIsentoDirpf> RendimentosIsentos { get; init; } = [];

    /// <summary>JCP e rendimento líquido de renda fixa. Prejuízo não entra.</summary>
    public IReadOnlyList<LinhaRendimentoExclusivoDirpf> RendimentosExclusivos { get; init; } = [];

    /// <summary>Posição de renda fixa em 31/12. Quantidade zero fica de fora.</summary>
    public IReadOnlyList<LinhaRendaFixaDirpf> RendaFixa { get; init; } = [];
}

/// <summary>Posição em 31/12. O custo de aquisição é o valor da ficha Bens e Direitos.</summary>
public sealed record LinhaBemDirpf(
    int AnoBase,
    string Perfil,
    string Ticker,
    string Nome,
    string Tipo,
    string Grupo,
    string Codigo,
    string DescricaoCodigo,
    string Cnpj,
    decimal Quantidade,
    decimal CustoAquisicao,
    decimal PrecoMedio,
    string Discriminacao);

/// <summary>Total recebido no ano-base, pela data de pagamento. JCP não entra aqui.</summary>
public sealed record LinhaRendimentoIsentoDirpf(
    int AnoBase,
    string Perfil,
    string Ticker,
    string Nome,
    string Natureza,
    string Codigo,
    string DescricaoCodigo,
    string Cnpj,
    decimal Valor,
    string Descricao);

/// <summary>
/// Rendimento de tributação exclusiva no ano-base.
/// JCP entra pelo ano da data de pagamento e traz <see cref="IrRetido"/> nulo:
/// o provento não guarda o imposto.
/// Renda fixa traz o IR retido já gravado na operação.
/// </summary>
public sealed record LinhaRendimentoExclusivoDirpf(
    int AnoBase,
    string Perfil,
    string CodigoAtivo,
    string Nome,
    string Natureza,
    string Codigo,
    string DescricaoCodigo,
    string Cnpj,
    decimal ValorLiquido,
    decimal? IrRetido,
    string Descricao);

/// <summary>Posição de renda fixa em 31/12 e o movimento do ano, para conferir o IR gravado.</summary>
public sealed record LinhaRendaFixaDirpf(
    int AnoBase,
    string Perfil,
    string Codigo,
    string Nome,
    string Tipo,
    string Corretora,
    string Grupo,
    string CodigoDirpf,
    string DescricaoCodigo,
    string Cnpj,
    decimal Quantidade,
    decimal CustoAquisicao,
    decimal PrecoMedio,
    decimal IrRetidoNoAno,
    decimal RendimentoLiquidoNoAno,
    string Discriminacao);
