using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Prévia editável do comprovante. Nada aqui grava compra nem produto:
/// o cadastro só entra na linha quando <see cref="ConfirmarCadastro"/> recebe
/// o produto que o usuário já confirmou. Cancelar desfaz o pendente.
/// O que segue para a compra é <see cref="LinhaPreviaComprovante.Nome"/>, nunca o rótulo.
/// </summary>
public sealed class PreviaComprovante
{
    public const string EtiquetaCasadoPelaDescricao = "casado pela descrição";

    public const string EtiquetaNovoProduto = "novo produto";

    public const string MensagemSemProdutoOuCategoria =
        "Classifique os itens sem produto ou categoria antes de confirmar.";

    public const string MensagemPreco = "O preço precisa ser maior que zero.";

    public const string MensagemProdutoRepetido = "Cada produto só pode aparecer uma vez na compra.";

    public const string MensagemSemItem = "Inclua ao menos um item.";

    private readonly List<LinhaPreviaComprovante> _linhas;
    private Guid? _cadastroLinhaId;

    private PreviaComprovante(
        ComprovanteNormalizado comprovante,
        IReadOnlyList<LocalCompra> locais,
        IReadOnlyList<ProdutoCesta> produtos,
        DateTime dataPadrao)
    {
        Loja = string.IsNullOrWhiteSpace(comprovante.Loja) ? null : comprovante.Loja.Trim();
        Data = comprovante.Data?.ToDateTime(TimeOnly.MinValue) ?? dataPadrao.Date;
        Legivel = comprovante.Legivel;
        TotalCupom = comprovante.TotalCupom;
        var casado = CasarLocal(Loja, locais);
        LocalCompraId = casado.Id;
        LocalCasadoNome = casado.Nome;
        var categorias = produtos
            .Where(p => p.Id > 0)
            .GroupBy(p => p.Id)
            .ToDictionary(g => g.Key, g => g.First().Categoria?.Nome);
        _linhas = comprovante.Linhas.Select(linha => LinhaPreviaComprovante.DaLeitura(
            linha,
            linha.ProdutoCestaId is int id && categorias.TryGetValue(id, out var nome) ? nome : null)).ToList();
        RecalcularSoma();
    }

    public string? Loja { get; }

    public DateTime Data { get; }

    public bool Legivel { get; }

    public decimal? TotalCupom { get; }

    public decimal SomaLinhas { get; private set; }

    public bool SomaExcedida { get; private set; }

    public int? LocalCompraId { get; }

    public string? LocalCasadoNome { get; }

    public IReadOnlyList<LinhaPreviaComprovante> Linhas => _linhas;

    public int NaoIdentificados => _linhas.Count(linha => linha.NaoIdentificado);

    public bool TotalDivergente => TotalCupom is decimal total && total != SomaLinhas;

    public bool PodeConfirmar => MensagemBloqueio is null;

    public string TextoNaoIdentificados => NaoIdentificados switch
    {
        0 => "0 não identificados",
        1 => "1 não identificado",
        _ => $"{NaoIdentificados} não identificados"
    };

    public string TextoLocal => LocalCasadoNome is not null
        ? $"Local: {LocalCasadoNome}"
        : "O local fica para você escolher na compra.";

    public string? MensagemBloqueio
    {
        get
        {
            if (_linhas.Count == 0)
                return MensagemSemItem;

            if (_linhas.Any(linha => !linha.TemProdutoECategoria))
                return MensagemSemProdutoOuCategoria;

            if (_linhas.Any(linha => linha.PrecoInvalido || linha.Preco <= 0))
                return MensagemPreco;

            if (_linhas.Any(linha => linha.ProdutoInativo))
                return NormalizadorComprovante.MotivoProdutoInativo;

            if (SomaExcedida || _linhas.Any(linha => linha.SomaExcedida))
                return NormalizadorComprovante.MotivoSomaExcedida;

            if (_linhas.GroupBy(linha => linha.ProdutoCestaId).Any(grupo => grupo.Count() > 1))
                return MensagemProdutoRepetido;

            return null;
        }
    }

    public static PreviaComprovante Criar(
        ComprovanteNormalizado comprovante,
        IReadOnlyList<LocalCompra> locais,
        IReadOnlyList<ProdutoCesta> produtos,
        DateTime dataPadrao)
    {
        ArgumentNullException.ThrowIfNull(comprovante);
        ArgumentNullException.ThrowIfNull(locais);
        ArgumentNullException.ThrowIfNull(produtos);
        return new PreviaComprovante(comprovante, locais, produtos, dataPadrao);
    }

    /// <summary>Um único local ativo com o mesmo nome, sem diferenciar maiúsculas. Senão, o usuário escolhe.</summary>
    public static (int? Id, string? Nome) CasarLocal(string? loja, IReadOnlyList<LocalCompra> locais)
    {
        ArgumentNullException.ThrowIfNull(locais);
        if (string.IsNullOrWhiteSpace(loja))
            return (null, null);

        var nome = loja.Trim();
        var casados = locais
            .Where(local => local is not null && local.Ativo && local.Id > 0 && !string.IsNullOrWhiteSpace(local.Nome))
            .Where(local => string.Equals(local.Nome.Trim(), nome, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();

        return casados.Count == 1 ? (casados[0].Id, casados[0].Nome.Trim()) : (null, null);
    }

    public void Remover(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        if (_cadastroLinhaId == linha.Id)
            _cadastroLinhaId = null;

        _linhas.Remove(linha);
        RecalcularSoma();
    }

    public void AlterarPreco(LinhaPreviaComprovante linha, decimal preco)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ExigirLinha(linha);
        linha.Preco = CestaDespesaRegras.Dinheiro(preco);
        linha.PrecoInvalido = linha.Preco <= 0;
        if (!linha.PrecoInvalido)
            linha.SomaExcedida = false;

        RecalcularSoma();
    }

    public void Classificar(LinhaPreviaComprovante linha, ProdutoCesta produto)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ArgumentNullException.ThrowIfNull(produto);
        ExigirLinha(linha);
        if (_cadastroLinhaId == linha.Id)
            _cadastroLinhaId = null;

        AplicarProduto(linha, produto);
    }

    /// <summary>
    /// Abre o cadastro com o nome cortado, sem o prefixo de não identificado.
    /// Não cria produto e não muda a linha.
    /// </summary>
    public ProdutoCestaFormModel IniciarCadastro(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ExigirLinha(linha);
        _cadastroLinhaId = linha.Id;
        return new ProdutoCestaFormModel
        {
            Nome = NomeParaCadastro(linha),
            CategoriaId = linha.CategoriaId is > 0 ? linha.CategoriaId.Value : 0,
            Ativo = true
        };
    }

    /// <summary>Desiste do cadastro. A linha continua como estava e nenhum produto é criado.</summary>
    public void CancelarCadastro() => _cadastroLinhaId = null;

    public bool CadastroPendente => _cadastroLinhaId is not null;

    /// <summary>Casa a linha com o produto que o usuário acabou de confirmar. Não persiste.</summary>
    public void ConfirmarCadastro(ProdutoCesta produto)
    {
        ArgumentNullException.ThrowIfNull(produto);
        if (_cadastroLinhaId is not Guid id)
            throw new InvalidOperationException("Não há cadastro em andamento.");

        var linha = _linhas.First(item => item.Id == id);
        AplicarProduto(linha, produto);
        _cadastroLinhaId = null;
    }

    public CompraCestaFormModel ParaCompra()
    {
        if (!PodeConfirmar)
            throw new InvalidOperationException(MensagemBloqueio ?? MensagemSemProdutoOuCategoria);

        return new CompraCestaFormModel
        {
            LocalCompraId = LocalCompraId ?? 0,
            Data = Data.Date,
            Itens = _linhas.Select(linha => new ItemCompraCestaFormModel
            {
                ProdutoCestaId = linha.ProdutoCestaId!.Value,
                Preco = linha.Preco
            }).ToList()
        };
    }

    public static string NomeParaCadastro(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        if (linha.NaoIdentificado)
            return SemPrefixoECortado(linha.DescricaoLida);

        return NormalizadorComprovante.Cortar(linha.Nome ?? string.Empty);
    }

    public static string SemPrefixoECortado(string? texto)
    {
        var nome = (texto ?? string.Empty).Trim();
        var prefixo = NormalizadorComprovante.PrefixoNaoIdentificado;
        if (nome.StartsWith(prefixo, StringComparison.Ordinal))
            nome = nome[prefixo.Length..].TrimStart();

        return NormalizadorComprovante.Cortar(nome);
    }

    public override string ToString() => "PreviaComprovante";

    private void AplicarProduto(LinhaPreviaComprovante linha, ProdutoCesta produto)
    {
        linha.Nome = (produto.Nome ?? string.Empty).Trim();
        linha.ProdutoCestaId = produto.Id > 0 ? produto.Id : null;
        linha.CategoriaId = produto.CategoriaId > 0 ? produto.CategoriaId : null;
        linha.CategoriaNome = string.IsNullOrWhiteSpace(produto.Categoria?.Nome) ? null : produto.Categoria.Nome.Trim();
        linha.CategoriaFaltando = produto.CategoriaId <= 0;
        linha.ProdutoInativo = !produto.Ativo;
        linha.NaoIdentificado = false;
        linha.NomeNovo = false;
        linha.Ambiguo = false;
        linha.ItemAusente = false;
        linha.CasadoPelaDescricao = false;
    }

    private void ExigirLinha(LinhaPreviaComprovante linha)
    {
        if (!_linhas.Contains(linha))
            throw new InvalidOperationException("A linha não está na prévia.");
    }

    private void RecalcularSoma()
    {
        decimal soma = 0;
        var excedida = false;
        foreach (var linha in _linhas)
        {
            if (linha.PrecoInvalido || linha.SomaExcedida || linha.Preco <= 0)
                continue;

            try
            {
                soma = CestaDespesaRegras.Dinheiro(soma + linha.Preco);
            }
            catch (OverflowException)
            {
                excedida = true;
                break;
            }
        }

        SomaLinhas = soma;
        SomaExcedida = excedida;
    }
}

public sealed class LinhaPreviaComprovante
{
    private LinhaPreviaComprovante()
    {
    }

    public Guid Id { get; } = Guid.NewGuid();

    public string DescricaoLida { get; private set; } = string.Empty;

    public string? Quantidade { get; private set; }

    public string? Unidade { get; private set; }

    public decimal Preco { get; set; }

    public bool PrecoInvalido { get; set; }

    public bool SomaExcedida { get; set; }

    public bool NaoIdentificado { get; set; }

    public bool NomeNovo { get; set; }

    public bool Ambiguo { get; set; }

    public bool CasadoPelaDescricao { get; set; }

    public bool ProdutoInativo { get; set; }

    public bool ItemAusente { get; set; }

    public bool CategoriaFaltando { get; set; }

    public int? ProdutoCestaId { get; set; }

    public int? CategoriaId { get; set; }

    public string? CategoriaNome { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string NomeLido { get; private set; } = string.Empty;

    public IReadOnlyList<string> Avisos { get; private set; } = [];

    public string? Motivo => Avisos.Count == 0 ? null : string.Join(" ", Avisos);

    /// <summary>O que a prévia grava. Nunca o <see cref="Rotulo"/>.</summary>
    public string NomeGravado => Nome;

    public string Rotulo =>
        NaoIdentificado ? NormalizadorComprovante.PrefixoNaoIdentificado + DescricaoLida : Nome;

    public bool TemProdutoECategoria =>
        ProdutoCestaId is > 0
        && !CategoriaFaltando
        && !NaoIdentificado
        && !NomeNovo
        && !Ambiguo
        && !ItemAusente;

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

    public string Titulo
    {
        get
        {
            if (NaoIdentificado)
                return Rotulo;

            if (CasadoPelaDescricao)
                return Juntar(Nome, PreviaComprovante.EtiquetaCasadoPelaDescricao, DescricaoLida);

            if (NomeNovo)
                return Juntar(Nome, PreviaComprovante.EtiquetaNovoProduto, DescricaoLida);

            return Nome;
        }
    }

    public string Subtitulo
    {
        get
        {
            var partes = new List<string>();
            if (!string.IsNullOrWhiteSpace(CategoriaNome))
                partes.Add(CategoriaNome.Trim());
            if (TextoApoio is not null)
                partes.Add(TextoApoio);
            if (Ambiguo && Motivo is not null)
                partes.Add(Motivo);

            return string.Join(" · ", partes);
        }
    }

    public bool PodeCadastrar => ProdutoCestaId is not > 0;

    public static LinhaPreviaComprovante DaLeitura(LinhaComprovanteNormalizada linha, string? categoriaNome)
    {
        ArgumentNullException.ThrowIfNull(linha);
        return new LinhaPreviaComprovante
        {
            DescricaoLida = linha.DescricaoLida,
            Quantidade = linha.Quantidade,
            Unidade = linha.Unidade,
            Preco = linha.Preco,
            PrecoInvalido = linha.PrecoInvalido,
            SomaExcedida = linha.SomaExcedida,
            NaoIdentificado = linha.NaoIdentificado,
            NomeNovo = linha.NomeNovo,
            Ambiguo = linha.Ambiguo,
            CasadoPelaDescricao = linha.CasadoPelaDescricao,
            ProdutoInativo = linha.ProdutoInativo,
            ItemAusente = linha.ItemAusente,
            CategoriaFaltando = linha.CategoriaFaltando,
            ProdutoCestaId = linha.ProdutoCestaId,
            CategoriaId = linha.CategoriaId,
            CategoriaNome = string.IsNullOrWhiteSpace(categoriaNome) ? null : categoriaNome.Trim(),
            Nome = linha.Nome,
            NomeLido = linha.NomeLido,
            Avisos = linha.Avisos
        };
    }

    public override string ToString() => "LinhaPreviaComprovante";

    private static string Juntar(params string?[] partes)
    {
        var vistos = new List<string>();
        foreach (var parte in partes)
        {
            if (string.IsNullOrWhiteSpace(parte))
                continue;

            vistos.Add(parte.Trim());
        }

        return string.Join(" · ", vistos);
    }
}
