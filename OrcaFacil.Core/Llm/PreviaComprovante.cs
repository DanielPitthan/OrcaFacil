using System.Globalization;
using OrcaFacil.Core.Cesta;
using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Llm;

/// <summary>
/// Prévia editável do comprovante. Confirmar o cadastro não grava o produto:
/// ele fica nesta prévia até o confirmar da compra. Cancelar a prévia o descarta.
/// O que segue para a compra é <see cref="LinhaPreviaComprovante.Nome"/>, nunca o rótulo.
/// </summary>
public sealed class PreviaComprovante
{
    public const string EtiquetaCasadoPelaDescricao = "casado pela descrição";

    public const string EtiquetaNovoProduto = "novo produto";

    public const string EtiquetaAmbiguo = "ambíguo";

    public const string EtiquetaNovoSeraCriado = "novo, será criado ao confirmar";

    public const string EtiquetaSeraSomado = "será somado a outro item igual";

    public const string MensagemSemProdutoOuCategoria =
        "Classifique os itens sem produto ou categoria antes de confirmar.";

    public const string MensagemPreco = "O preço precisa ser maior que zero.";

    public const string MensagemSemItem = "Inclua ao menos um item.";

    private readonly List<LinhaPreviaComprovante> _linhas;
    private readonly List<ProdutoCesta> _catalogo;
    private readonly List<ProdutoPendente> _pendentes = [];
    private Guid? _cadastroLinhaId;
    private int _sequenciaPendente;

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
        _catalogo = produtos.Where(produto => produto is not null).ToList();
        _linhas = comprovante.Linhas.Select(linha => LinhaPreviaComprovante.DaLeitura(
            linha,
            linha.ProdutoCestaId is int id && categorias.TryGetValue(id, out var nome) ? nome : null)).ToList();
        RecalcularSoma();
        AtualizarSomas();
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

            if (_linhas.Any(linha => linha.ProdutoCestaId is > 0 && !linha.Marcado))
                return CasamentoComprovante.MensagemConfirmacaoObrigatoria;

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
        AtualizarSomas();
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
        AtualizarSomas();
    }

    /// <summary>
    /// Abre o cadastro com a descrição lida, sem o prefixo e cortada em 80.
    /// Um segundo início, com outro ainda em andamento, é ignorado e não cria produto.
    /// </summary>
    public ProdutoCestaFormModel? IniciarCadastro(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ExigirLinha(linha);
        if (_cadastroLinhaId is not null)
            return null;

        _cadastroLinhaId = linha.Id;
        return new ProdutoCestaFormModel
        {
            Nome = NomeParaCadastro(linha),
            CategoriaId = linha.CategoriaId is > 0 ? linha.CategoriaId.Value : 0,
            Ativo = true
        };
    }

    /// <summary>Desiste do cadastro em andamento. A linha continua como estava e nenhum produto é criado.</summary>
    public void CancelarCadastro() => _cadastroLinhaId = null;

    public bool CadastroPendente => _cadastroLinhaId is not null;

    /// <summary>Há pelo menos duas linhas "novo produto". Ambíguas e já pendentes não contam.</summary>
    public bool PodeCadastrarTudo => _linhas.Count(EhNovoProduto) >= 2;

    /// <summary>
    /// Guarda o produto na prévia e casa a linha. Não grava no banco.
    /// Nome igual a um produto já existente reaproveita esse produto.
    /// </summary>
    public void ConfirmarCadastro(ProdutoCestaFormModel modelo, decimal preco, string? categoriaNome = null)
    {
        ArgumentNullException.ThrowIfNull(modelo);
        if (_cadastroLinhaId is not Guid id)
            throw new InvalidOperationException("Não há cadastro em andamento.");

        var nome = (modelo.Nome ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(nome))
            throw new InvalidOperationException("Informe o nome do produto.");

        if (nome.Length > NormalizadorComprovante.TamanhoMaximoNome)
            throw new InvalidOperationException("O nome pode ter no máximo 80 caracteres.");

        if (modelo.CategoriaId <= 0)
            throw new InvalidOperationException("Selecione a categoria do produto.");

        var linha = _linhas.First(item => item.Id == id);
        if (AplicarNome(linha, nome, modelo.CategoriaId, categoriaNome, preco) == ResultadoNome.Ambiguo)
        {
            _cadastroLinhaId = null;
            throw new InvalidOperationException(NormalizadorComprovante.MotivoAmbiguo);
        }

        _cadastroLinhaId = null;
        AtualizarSomas();
    }

    /// <summary>
    /// Marca de uma vez cada linha "novo produto". O nome sai da descrição lida,
    /// com o mesmo corte e a mesma retirada de prefixo do cadastro de uma linha.
    /// Não grava. Um segundo chamado, sem linhas novas, não cria outro produto.
    /// </summary>
    public void CadastrarTudo(int categoriaId, string? categoriaNome = null)
    {
        if (_cadastroLinhaId is not null || !PodeCadastrarTudo)
            return;

        if (categoriaId <= 0)
            throw new InvalidOperationException("Selecione a categoria do produto.");

        var nomeCategoria = string.IsNullOrWhiteSpace(categoriaNome) ? null : categoriaNome.Trim();
        foreach (var linha in _linhas.Where(EhNovoProduto).ToList())
        {
            var nome = NomeParaCadastro(linha);
            if (string.IsNullOrWhiteSpace(nome))
                continue;

            if (AplicarNome(linha, nome, categoriaId, nomeCategoria, null) == ResultadoNome.Ambiguo)
                continue;

            linha.PodeDesfazerNovo = true;
        }

        AtualizarSomas();
    }

    /// <summary>A linha volta a "novo produto", sem produto pendente.</summary>
    public void DesfazerNovo(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ExigirLinha(linha);
        if (!linha.PodeDesfazerNovo)
            return;

        if (_cadastroLinhaId == linha.Id)
            _cadastroLinhaId = null;

        var chave = linha.ProdutoNovoChave;
        linha.Nome = NormalizadorComprovante.Cortar(linha.NomeLido ?? string.Empty);
        linha.ProdutoCestaId = null;
        linha.ProdutoNovoChave = null;
        linha.CategoriaId = null;
        linha.CategoriaNome = null;
        linha.CategoriaFaltando = true;
        linha.ProdutoInativo = false;
        linha.NaoIdentificado = false;
        linha.NomeNovo = true;
        linha.Ambiguo = false;
        linha.ItemAusente = false;
        linha.CasadoPelaDescricao = false;
        linha.NovoSeraCriado = false;
        linha.PodeDesfazerNovo = false;
        linha.CasamentoConfirmado = false;
        linha.Marcado = false;

        if (chave is not null && _linhas.All(item => item.ProdutoNovoChave != chave))
            _pendentes.RemoveAll(produto => produto.Chave == chave);

        AtualizarSomas();
    }

    public void Marcar(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        ExigirLinha(linha);
        if (linha.ProdutoCestaId is > 0)
            linha.Marcado = true;
    }

    /// <summary>Marca só linhas com produto que não estão pendentes. Não confirma.</summary>
    public void MarcarNaoPendentes()
    {
        foreach (var linha in _linhas)
        {
            if (linha.ProdutoCestaId is > 0 && !linha.Pendente)
                linha.Marcado = true;
        }
    }

    public void ConfirmarMarcados()
    {
        foreach (var linha in _linhas)
        {
            if (linha.ProdutoCestaId is > 0 && linha.Marcado)
                linha.CasamentoConfirmado = true;
        }
    }

    public void ConfirmarCasamentos() => ConfirmarMarcados();

    public IReadOnlyList<ItemSaidaComprovante> ItensComProduto()
    {
        var itens = new List<ItemSaidaComprovante>();
        foreach (var linha in _linhas)
        {
            if (linha.ProdutoCestaId is not int produtoId || produtoId <= 0)
                continue;

            var existente = linha.Saidas.FirstOrDefault(item => item.ProdutoId == produtoId);
            var saida = (existente ?? new ItemSaidaComprovante
            {
                DescricaoLida = linha.DescricaoLida,
                ProdutoId = produtoId,
                Confianca = linha.Saidas.Count == 0 ? 0m : linha.Saidas.Min(item => item.Confianca),
                Pendente = linha.Pendente
            }) with
            {
                ProdutoId = produtoId,
                Confirmado = false
            };
            itens.Add(linha.CasamentoConfirmado ? saida.Confirmar() : saida);
        }

        return itens;
    }

    public CompraCestaFormModel ParaCompra()
    {
        if (!PodeConfirmar)
            throw new InvalidOperationException(MensagemBloqueio ?? MensagemSemProdutoOuCategoria);

        var confirmacoes = ItensComProduto();
        if (confirmacoes.Count > 0)
            CasamentoComprovante.GarantirConfirmacao(confirmacoes);

        var novos = new List<ProdutoNovoCompra>();
        var itens = new List<ItemCompraCestaFormModel>();
        foreach (var linha in _linhas)
        {
            if (linha.NovoSeraCriado)
            {
                var pendente = _pendentes.First(produto => produto.Chave == linha.ProdutoNovoChave);
                if (novos.All(produto => produto.Chave != pendente.Chave))
                {
                    novos.Add(new ProdutoNovoCompra
                    {
                        Chave = pendente.Chave,
                        Nome = pendente.Nome,
                        CategoriaId = pendente.CategoriaId,
                        CategoriaNome = pendente.CategoriaNome
                    });
                }

                itens.Add(new ItemCompraCestaFormModel
                {
                    ProdutoNovoChave = pendente.Chave,
                    NomePendente = pendente.Nome,
                    CategoriaPendente = pendente.CategoriaNome,
                    Preco = linha.Preco
                });
                continue;
            }

            itens.Add(new ItemCompraCestaFormModel
            {
                ProdutoCestaId = linha.ProdutoCestaId!.Value,
                Preco = linha.Preco
            });
        }

        return new CompraCestaFormModel
        {
            LocalCompraId = LocalCompraId ?? 0,
            Data = Data.Date,
            ProdutosNovos = novos,
            Itens = itens,
            ItensSaida = confirmacoes.ToList()
        };
    }

    public static string NomeParaCadastro(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        return SemPrefixoECortado(linha.DescricaoLida);
    }

    public static string SemPrefixoECortado(string? texto)
    {
        var nome = texto ?? string.Empty;
        var prefixo = NormalizadorComprovante.PrefixoNaoIdentificado;
        if (nome.StartsWith(prefixo, StringComparison.Ordinal))
            nome = nome[prefixo.Length..];

        return NormalizadorComprovante.Cortar(nome.Trim());
    }

    public override string ToString() => "PreviaComprovante";

    private void AplicarProduto(LinhaPreviaComprovante linha, ProdutoCesta produto)
    {
        int? novoId = produto.Id > 0 ? produto.Id : null;
        var mudouProduto = linha.ProdutoCestaId != novoId;
        linha.Nome = (produto.Nome ?? string.Empty).Trim();
        linha.ProdutoCestaId = novoId;
        linha.CategoriaId = produto.CategoriaId > 0 ? produto.CategoriaId : null;
        linha.CategoriaNome = string.IsNullOrWhiteSpace(produto.Categoria?.Nome) ? null : produto.Categoria.Nome.Trim();
        linha.CategoriaFaltando = produto.CategoriaId <= 0;
        linha.ProdutoInativo = !produto.Ativo;
        linha.NaoIdentificado = false;
        linha.NomeNovo = false;
        linha.Ambiguo = false;
        linha.ItemAusente = false;
        linha.CasadoPelaDescricao = false;
        linha.NovoSeraCriado = false;
        linha.ProdutoNovoChave = null;
        linha.PodeDesfazerNovo = false;
        if (mudouProduto)
        {
            linha.CasamentoConfirmado = false;
            linha.Marcado = false;
        }
    }

    private static void AplicarPendente(LinhaPreviaComprovante linha, ProdutoPendente pendente)
    {
        linha.Nome = pendente.Nome;
        linha.ProdutoCestaId = pendente.Id;
        linha.ProdutoNovoChave = pendente.Chave;
        linha.CategoriaId = pendente.CategoriaId;
        linha.CategoriaNome = pendente.CategoriaNome;
        linha.CategoriaFaltando = false;
        linha.ProdutoInativo = false;
        linha.NaoIdentificado = false;
        linha.NomeNovo = false;
        linha.Ambiguo = false;
        linha.ItemAusente = false;
        linha.CasadoPelaDescricao = false;
        linha.NovoSeraCriado = true;
        linha.PodeDesfazerNovo = false;
        linha.CasamentoConfirmado = false;
        linha.Marcado = false;
    }

    private ResultadoNome AplicarNome(
        LinhaPreviaComprovante linha,
        string nome,
        int categoriaId,
        string? categoriaNome,
        decimal? preco)
    {
        var chave = NormalizadorComprovante.TextoComparacao(nome);
        var existentes = _catalogo
            .Where(produto => produto.Id > 0 && NormalizadorComprovante.TextoComparacao(produto.Nome) == chave)
            .Take(2)
            .ToList();

        if (existentes.Count > 1)
            return ResultadoNome.Ambiguo;

        if (existentes.Count == 1)
        {
            AplicarProduto(linha, existentes[0]);
            if (preco is decimal valor)
                AlterarPreco(linha, valor);
            return ResultadoNome.Casado;
        }

        var pendente = _pendentes.FirstOrDefault(produto => produto.Chave == chave);
        if (pendente is null)
        {
            pendente = new ProdutoPendente
            {
                Id = --_sequenciaPendente,
                Chave = chave,
                Nome = nome,
                CategoriaId = categoriaId,
                CategoriaNome = string.IsNullOrWhiteSpace(categoriaNome) ? null : categoriaNome.Trim()
            };
            _pendentes.Add(pendente);
        }

        AplicarPendente(linha, pendente);
        if (preco is decimal precoLinha)
            AlterarPreco(linha, precoLinha);
        return ResultadoNome.Pendente;
    }

    private static bool EhNovoProduto(LinhaPreviaComprovante linha) =>
        linha.NomeNovo && !linha.Ambiguo && !linha.NaoIdentificado && !linha.NovoSeraCriado;

    private void ExigirLinha(LinhaPreviaComprovante linha)
    {
        if (!_linhas.Contains(linha))
            throw new InvalidOperationException("A linha não está na prévia.");
    }

    private void AtualizarSomas()
    {
        static string Chave(LinhaPreviaComprovante linha)
        {
            if (!string.IsNullOrEmpty(linha.ProdutoNovoChave))
                return "n:" + linha.ProdutoNovoChave;

            if (linha.ProdutoCestaId is > 0)
                return "p:" + linha.ProdutoCestaId.Value;

            return "u:" + linha.Id;
        }

        foreach (var linha in _linhas)
            linha.SeraSomado = false;

        foreach (var grupo in _linhas.GroupBy(Chave))
        {
            if (grupo.Key.StartsWith("u:", StringComparison.Ordinal) || grupo.Count() < 2)
                continue;

            foreach (var linha in grupo)
                linha.SeraSomado = true;
        }
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

    private enum ResultadoNome
    {
        Casado,
        Pendente,
        Ambiguo
    }

    private sealed class ProdutoPendente
    {
        public required int Id { get; init; }
        public required string Chave { get; init; }
        public required string Nome { get; init; }
        public required int CategoriaId { get; init; }
        public string? CategoriaNome { get; init; }
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

    public bool NovoSeraCriado { get; set; }

    public bool PodeDesfazerNovo { get; set; }

    public bool SeraSomado { get; set; }

    public string? ProdutoNovoChave { get; set; }

    public bool ProdutoInativo { get; set; }

    public bool ItemAusente { get; set; }

    public bool CategoriaFaltando { get; set; }

    public int? ProdutoCestaId { get; set; }

    public bool CasamentoConfirmado { get; set; }

    public bool Marcado { get; set; }

    public bool Revisar { get; set; }

    public List<ItemSaidaComprovante> Saidas { get; init; } = [];

    public bool Pendente =>
        ProdutoCestaId is > 0
        && (Revisar || Saidas.Count == 0 || Saidas.Any(item => item.Pendente));

    public string? MarcadorCasamento
    {
        get
        {
            if (ProdutoCestaId is not > 0)
                return null;

            var estado = Marcado || CasamentoConfirmado
                ? "confirmado"
                : Pendente
                    ? "pendente"
                    : "não confirmado";
            if (Saidas.Count == 0)
                return estado;

            var confianca = Saidas.Min(item => item.Confianca).ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"));
            return $"{estado} · confiança {confianca}";
        }
    }

    public int? CategoriaId { get; set; }

    public string? CategoriaNome { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string NomeLido { get; private set; } = string.Empty;

    public IReadOnlyList<int> CandidatosIds { get; private set; } = [];

    public IReadOnlyList<string> Avisos { get; private set; } = [];

    public string? Motivo => Avisos.Count == 0 ? null : string.Join(" ", Avisos);

    /// <summary>O que a prévia grava. Nunca o <see cref="Rotulo"/>.</summary>
    public string NomeGravado => Nome;

    public string Rotulo =>
        NaoIdentificado ? NormalizadorComprovante.PrefixoNaoIdentificado + DescricaoLida : Nome;

    public bool TemProdutoECategoria =>
        (ProdutoCestaId is > 0 || NovoSeraCriado)
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

    private string? AvisoSoma => SeraSomado ? PreviaComprovante.EtiquetaSeraSomado : null;

    public string Titulo
    {
        get
        {
            if (NaoIdentificado)
                return Rotulo;

            if (NovoSeraCriado)
                return Juntar(Nome, PreviaComprovante.EtiquetaNovoSeraCriado, AvisoSoma, DescricaoLida);

            if (CasadoPelaDescricao)
                return Juntar(Nome, PreviaComprovante.EtiquetaCasadoPelaDescricao, AvisoSoma, DescricaoLida);

            if (Ambiguo)
                return Juntar(Nome, PreviaComprovante.EtiquetaAmbiguo, AvisoSoma, DescricaoLida);

            if (NomeNovo)
                return Juntar(Nome, PreviaComprovante.EtiquetaNovoProduto, AvisoSoma, DescricaoLida);

            return Juntar(Nome, AvisoSoma);
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

    public bool PodeCadastrar => ProdutoCestaId is not > 0 && !NovoSeraCriado;

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
            Revisar = linha.Revisar,
            CategoriaId = linha.CategoriaId,
            CategoriaNome = string.IsNullOrWhiteSpace(categoriaNome) ? null : categoriaNome.Trim(),
            Nome = linha.Nome,
            NomeLido = linha.NomeLido,
            CandidatosIds = linha.CandidatosIds,
            Avisos = linha.Avisos,
            Saidas = linha.Saidas.ToList()
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

            var texto = parte.Trim();
            if (vistos.Contains(texto, StringComparer.Ordinal))
                continue;

            vistos.Add(texto);
        }

        return string.Join(" · ", vistos);
    }
}
