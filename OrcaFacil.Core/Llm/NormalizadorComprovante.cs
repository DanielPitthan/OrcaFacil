using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Casa a resposta lida com o catálogo por nome, sem diferenciar maiúsculas.
/// Soma o preço dos nomes repetidos numa linha. Não descarta item.
/// Quantidade e unidade ficam só como texto de apoio e não entram no preço.
/// </summary>
public static class NormalizadorComprovante
{
    public const int TamanhoMaximoNome = 80;

    public const string PrefixoNaoIdentificado = "Não identificado: ";

    public static ComprovanteNormalizado Normalizar(
        ComprovanteLido comprovante,
        IReadOnlyList<ProdutoCesta> catalogo)
    {
        ArgumentNullException.ThrowIfNull(comprovante);
        ArgumentNullException.ThrowIfNull(catalogo);

        var indice = Indexar(catalogo);
        var ordem = new List<string>();
        var porChave = new Dictionary<string, LinhaEmEdicao>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in comprovante.Itens ?? [])
        {
            var linha = Classificar(item, indice);
            if (porChave.TryGetValue(linha.Chave, out var existente))
            {
                existente.Preco = CestaDespesaRegras.Dinheiro(existente.Preco + linha.Preco);
                if (linha.NomeExcedeLimite)
                    existente.NomeExcedeLimite = true;
                continue;
            }

            porChave.Add(linha.Chave, linha);
            ordem.Add(linha.Chave);
        }

        var linhas = ordem.Select(chave => porChave[chave].Concluir()).ToList();
        var soma = CestaDespesaRegras.Dinheiro(linhas.Sum(linha => linha.Preco));

        return new ComprovanteNormalizado
        {
            Loja = comprovante.Loja,
            Data = comprovante.Data,
            Legivel = comprovante.Legivel,
            TotalCupom = comprovante.TotalCupom is decimal total
                ? CestaDespesaRegras.Dinheiro(total)
                : null,
            SomaLinhas = soma,
            Linhas = linhas
        };
    }

    private static Dictionary<string, ProdutoCesta> Indexar(IReadOnlyList<ProdutoCesta> catalogo)
    {
        var indice = new Dictionary<string, ProdutoCesta>(StringComparer.OrdinalIgnoreCase);
        foreach (var produto in catalogo)
        {
            if (produto is null || string.IsNullOrWhiteSpace(produto.Nome))
                continue;

            indice.TryAdd(produto.Nome.Trim(), produto);
        }

        return indice;
    }

    private static LinhaEmEdicao Classificar(ItemComprovanteLido item, Dictionary<string, ProdutoCesta> catalogo)
    {
        var descricao = (item.DescricaoLida ?? string.Empty).Trim();
        var produto = (item.Produto ?? string.Empty).Trim();
        // O preço da linha já vem com o desconto aplicado.
        var preco = CestaDespesaRegras.Dinheiro(item.Preco);

        if (!item.Reconhecido || produto.Length == 0)
        {
            if (catalogo.TryGetValue(descricao, out var pelaDescricao))
                return DoCatalogo(item, descricao, descricao, pelaDescricao, preco);

            return NaoIdentificada(item, descricao, preco);
        }

        if (catalogo.TryGetValue(produto, out var peloProduto))
            return DoCatalogo(item, descricao, produto, peloProduto, preco);

        return NomeNovo(item, descricao, produto, preco);
    }

    private static LinhaEmEdicao DoCatalogo(
        ItemComprovanteLido item,
        string descricao,
        string nomeLido,
        ProdutoCesta produto,
        decimal preco)
    {
        var nome = produto.Nome.Trim();
        var semCategoria = produto.CategoriaId <= 0;
        return new LinhaEmEdicao
        {
            Chave = $"p:{produto.Id}",
            DescricaoLida = descricao,
            Quantidade = Apoio(item.Quantidade),
            Unidade = Apoio(item.Unidade),
            Preco = preco,
            Reconhecido = true,
            Nome = nome,
            NomeLido = nomeLido,
            NomeExcedeLimite = nomeLido.Length > TamanhoMaximoNome,
            ProdutoCestaId = produto.Id,
            CategoriaId = semCategoria ? null : produto.CategoriaId,
            CategoriaFaltando = semCategoria
        };
    }

    private static LinhaEmEdicao NomeNovo(
        ItemComprovanteLido item,
        string descricao,
        string produto,
        decimal preco)
    {
        var excede = produto.Length > TamanhoMaximoNome;
        return new LinhaEmEdicao
        {
            Chave = "n:" + Cortar(produto),
            DescricaoLida = descricao,
            Quantidade = Apoio(item.Quantidade),
            Unidade = Apoio(item.Unidade),
            Preco = preco,
            Reconhecido = true,
            NomeNovo = true,
            CategoriaFaltando = true,
            NomeExcedeLimite = excede,
            Nome = Cortar(produto),
            NomeLido = produto
        };
    }

    private static LinhaEmEdicao NaoIdentificada(ItemComprovanteLido item, string descricao, decimal preco)
    {
        return new LinhaEmEdicao
        {
            Chave = "u:" + descricao,
            DescricaoLida = descricao,
            Quantidade = Apoio(item.Quantidade),
            Unidade = Apoio(item.Unidade),
            Preco = preco,
            NaoIdentificado = true,
            CategoriaFaltando = true,
            NomeExcedeLimite = descricao.Length > TamanhoMaximoNome,
            Nome = Cortar(descricao),
            NomeLido = descricao
        };
    }

    private static string? Apoio(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return null;

        return texto.Trim();
    }

    private static string Cortar(string nome)
    {
        if (nome.Length <= TamanhoMaximoNome)
            return nome;

        var tamanho = TamanhoMaximoNome;
        if (char.IsHighSurrogate(nome[tamanho - 1]))
            tamanho--;

        return nome[..tamanho];
    }

    private sealed class LinhaEmEdicao
    {
        public required string Chave { get; init; }
        public required string DescricaoLida { get; init; }
        public string? Quantidade { get; init; }
        public string? Unidade { get; init; }
        public decimal Preco { get; set; }
        public bool Reconhecido { get; init; }
        public bool NaoIdentificado { get; init; }
        public bool NomeNovo { get; init; }
        public bool CategoriaFaltando { get; init; }
        public bool NomeExcedeLimite { get; set; }
        public int? ProdutoCestaId { get; init; }
        public int? CategoriaId { get; init; }
        public required string Nome { get; init; }
        public required string NomeLido { get; init; }

        public LinhaComprovanteNormalizada Concluir() => new()
        {
            DescricaoLida = DescricaoLida,
            Quantidade = Quantidade,
            Unidade = Unidade,
            Preco = Preco,
            Reconhecido = Reconhecido,
            NaoIdentificado = NaoIdentificado,
            NomeNovo = NomeNovo,
            CategoriaFaltando = CategoriaFaltando,
            NomeExcedeLimite = NomeExcedeLimite,
            ProdutoCestaId = ProdutoCestaId,
            CategoriaId = CategoriaId,
            Nome = Nome,
            NomeLido = NomeLido
        };
    }
}

/// <summary>
/// Leitura pronta para a prévia. <see cref="TotalCupom"/> e <see cref="SomaLinhas"/>
/// saem juntos para a tela mostrar a divergência.
/// </summary>
public sealed record ComprovanteNormalizado
{
    public string? Loja { get; init; }
    public DateOnly? Data { get; init; }
    public bool Legivel { get; init; }
    public decimal? TotalCupom { get; init; }
    public decimal SomaLinhas { get; init; }
    public IReadOnlyList<LinhaComprovanteNormalizada> Linhas { get; init; } = [];

    public bool TotalDivergente => TotalCupom is decimal total && total != SomaLinhas;

    /// <summary>Só grava quando toda linha já tem produto, categoria e preço positivo.</summary>
    public bool PodeGravar => Linhas.Count > 0 && Linhas.All(linha => linha.ProntaParaGravar);

    public override string ToString() => "ComprovanteNormalizado";
}

/// <summary>
/// Uma linha do comprovante. Nome novo e não identificado ainda não têm produto para gravar.
/// </summary>
public sealed record LinhaComprovanteNormalizada
{
    public string DescricaoLida { get; init; } = string.Empty;

    public string? Quantidade { get; init; }

    public string? Unidade { get; init; }

    public decimal Preco { get; init; }

    public bool Reconhecido { get; init; }

    public bool NaoIdentificado { get; init; }

    public bool NomeNovo { get; init; }

    public bool CategoriaFaltando { get; init; }

    public bool NomeExcedeLimite { get; init; }

    public int? ProdutoCestaId { get; init; }

    public int? CategoriaId { get; init; }

    /// <summary>Nome do catálogo, nome novo cortado em 80, ou a descrição cortada se não identificada.</summary>
    public string Nome { get; init; } = string.Empty;

    /// <summary>Texto comparado ao catálogo, antes do corte de 80.</summary>
    public string NomeLido { get; init; } = string.Empty;

    public bool ProntaParaGravar =>
        ProdutoCestaId is > 0 && !CategoriaFaltando && Preco > 0;

    public string? TextoApoio
    {
        get
        {
            if (Quantidade is null && Unidade is null)
                return null;

            if (Quantidade is null)
                return Unidade;

            if (Unidade is null)
                return Quantidade;

            return $"{Quantidade} {Unidade}";
        }
    }

    public string Rotulo =>
        NaoIdentificado ? NormalizadorComprovante.PrefixoNaoIdentificado + DescricaoLida : Nome;

    public override string ToString() => "LinhaComprovanteNormalizada";
}
