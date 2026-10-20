namespace OrcaFacil.Core.Dirpf;

/// <summary>Uma DIRPF de um ano-base. Cada perfil é uma linha própria: os custos não se somam.</summary>
public sealed class DirpfAnoBase
{
    public required int Ano { get; init; }

    public IReadOnlyList<LinhaBemDirpf> Bens { get; init; } = [];

    public IReadOnlyList<LinhaRendimentoIsentoDirpf> RendimentosIsentos { get; init; } = [];
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
