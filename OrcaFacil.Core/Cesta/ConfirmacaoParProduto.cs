using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Llm;

namespace OrcaFacil.Core.Cesta;

/// <summary>
/// Confirmação do par produto na prévia. Confiança e pendente só aparecem na tela:
/// não liberam a gravação. Item com id de produto cadastrado espera Confirmar ou Trocar.
/// </summary>
public sealed class ConfirmacaoParProduto
{
    private readonly List<ProdutoCesta> _catalogo;
    private readonly List<ItemPar> _linhas;

    private ConfirmacaoParProduto(IReadOnlyList<Entrada> entradas, IReadOnlyList<ProdutoCesta> catalogo)
    {
        _catalogo = [];
        foreach (var produto in catalogo)
        {
            if (produto is not null)
                _catalogo.Add(produto);
        }

        _linhas = [];
        foreach (var entrada in entradas)
        {
            if (entrada is not null)
                _linhas.Add(ItemPar.Criar(entrada, _catalogo));
        }
    }

    public static ConfirmacaoParProduto Criar(
        IReadOnlyList<Entrada> entradas,
        IReadOnlyList<ProdutoCesta> catalogo)
    {
        ArgumentNullException.ThrowIfNull(entradas);
        ArgumentNullException.ThrowIfNull(catalogo);
        return new ConfirmacaoParProduto(entradas, catalogo);
    }

    public IReadOnlyList<ItemPar> Linhas => _linhas;

    public bool TemParNaoConfirmado => _linhas.Any(linha => linha.AguardandoConfirmacao);

    public bool PodeGravar => _linhas.Count == 0 || CasamentoComprovante.PodeSalvar(ParaBarreira());

    public string? MensagemBloqueio => PodeGravar ? null : CasamentoComprovante.MensagemConfirmacaoObrigatoria;

    public IReadOnlyList<ItemSaidaComprovante> ParaBarreira()
    {
        var itens = new List<ItemSaidaComprovante>(_linhas.Count);
        foreach (var linha in _linhas)
            itens.Add(linha.ParaContrato());

        return itens;
    }

    public void Confirmar(int indice)
    {
        var linha = Exigir(indice);
        if (!linha.AguardandoConfirmacao)
            return;

        linha.DefinirSituacao(SituacaoPar.Confirmado);
    }

    /// <summary>Escolhe outro produto ativo do cadastro. A escolha confirma o par.</summary>
    public bool Trocar(int indice, int produtoId)
    {
        var linha = Exigir(indice);
        if (linha.Situacao is SituacaoPar.SemPar or SituacaoPar.Desfeito)
            return false;

        var produto = ProdutoAtivo(produtoId);
        if (produto is null)
            return false;

        linha.DefinirProduto(produto.Id, (produto.Nome ?? string.Empty).Trim());
        linha.DefinirSituacao(SituacaoPar.Trocado);
        return true;
    }

    public void Desfazer(int indice)
    {
        var linha = Exigir(indice);
        if (linha.Situacao is SituacaoPar.SemPar or SituacaoPar.Desfeito)
            return;

        linha.DesfazerPar();
    }

    public static void AplicarNaoIdentificado(LinhaPreviaComprovante linha)
    {
        ArgumentNullException.ThrowIfNull(linha);
        linha.ProdutoCestaId = null;
        linha.CategoriaId = null;
        linha.CategoriaNome = null;
        linha.CategoriaFaltando = true;
        linha.ProdutoInativo = false;
        linha.NaoIdentificado = true;
        linha.NomeNovo = false;
        linha.Ambiguo = false;
        linha.ItemAusente = false;
        linha.CasadoPelaDescricao = false;
        linha.NovoSeraCriado = false;
        linha.ProdutoNovoChave = null;
        linha.PodeDesfazerNovo = false;
        linha.Nome = NormalizadorComprovante.Cortar(linha.DescricaoLida);
    }

    public static Guid?[] Ligar(IReadOnlyList<ItemPar> pares, IReadOnlyList<LinhaPreviaChave> linhas)
    {
        ArgumentNullException.ThrowIfNull(pares);
        ArgumentNullException.ThrowIfNull(linhas);
        var ligacao = new Guid?[pares.Count];
        for (var i = 0; i < pares.Count; i++)
        {
            var par = pares[i];
            if (par is null)
                continue;

            var descricao = (par.DescricaoLida ?? string.Empty).Trim();
            LinhaPreviaChave? porDescricao = null;
            LinhaPreviaChave? porId = null;
            foreach (var linha in linhas)
            {
                if (!string.Equals((linha.DescricaoLida ?? string.Empty).Trim(), descricao, StringComparison.Ordinal))
                    continue;

                porDescricao ??= linha;
                if (par.ProdutoId is int id && linha.ProdutoCestaId == id)
                {
                    porId = linha;
                    break;
                }
            }

            var escolhida = porId ?? porDescricao;
            if (escolhida is not null)
                ligacao[i] = escolhida.Value.Id;
        }

        return ligacao;
    }

    private ItemPar Exigir(int indice)
    {
        if ((uint)indice >= (uint)_linhas.Count)
            throw new ArgumentOutOfRangeException(nameof(indice));

        return _linhas[indice];
    }

    private ProdutoCesta? ProdutoAtivo(int produtoId)
    {
        if (produtoId <= 0)
            return null;

        foreach (var produto in _catalogo)
        {
            if (produto.Id == produtoId && produto.Ativo)
                return produto;
        }

        return null;
    }

    public enum SituacaoPar
    {
        SemPar,
        Aguardando,
        Confirmado,
        Trocado,
        Desfeito
    }

    public readonly record struct LinhaPreviaChave(Guid Id, string DescricaoLida, int? ProdutoCestaId);

    public sealed record Entrada(int? ProdutoId, decimal Confianca, bool Pendente, string? DescricaoLida);

    public sealed class ItemPar
    {
        private ItemPar()
        {
        }

        public string DescricaoLida { get; private set; } = string.Empty;

        public int? ProdutoId { get; private set; }

        public string Nome { get; private set; } = string.Empty;

        public decimal Confianca { get; private set; }

        public bool Pendente { get; private set; }

        public bool NaoIdentificado { get; private set; }

        public SituacaoPar Situacao { get; private set; }

        public bool AguardandoConfirmacao => Situacao == SituacaoPar.Aguardando;

        public bool PodeCadastrar => NaoIdentificado;

        public bool DestacarRevisao => Pendente && AguardandoConfirmacao;

        public string NomeExibido =>
            NaoIdentificado
                ? NormalizadorComprovante.PrefixoNaoIdentificado + DescricaoLida
                : Nome;

        public string? TextoConfianca => AguardandoConfirmacao ? FormatarConfianca(Confianca) : null;

        public static string FormatarConfianca(decimal confianca)
        {
            if (confianca < 0m)
                confianca = 0m;
            else if (confianca > 1m)
                confianca = 1m;

            var percentual = (int)Math.Round(confianca * 100m, MidpointRounding.AwayFromZero);
            return $"Confiança {percentual}%";
        }

        internal void DefinirSituacao(SituacaoPar situacao) => Situacao = situacao;

        internal void DefinirProduto(int id, string nome)
        {
            ProdutoId = id;
            Nome = nome;
            NaoIdentificado = false;
        }

        internal void DesfazerPar()
        {
            ProdutoId = null;
            Nome = DescricaoLida;
            NaoIdentificado = true;
            Situacao = SituacaoPar.Desfeito;
        }

        internal ItemSaidaComprovante ParaContrato()
        {
            var contrato = new ItemSaidaComprovante
            {
                DescricaoLida = DescricaoLida,
                ProdutoId = Situacao is SituacaoPar.SemPar or SituacaoPar.Desfeito ? null : ProdutoId,
                Confianca = Confianca,
                Pendente = Pendente
            };

            return Situacao is SituacaoPar.Confirmado or SituacaoPar.Trocado
                ? contrato.Confirmar()
                : contrato;
        }

        internal static ItemPar Criar(Entrada entrada, IReadOnlyList<ProdutoCesta> catalogo)
        {
            var descricao = (entrada.DescricaoLida ?? string.Empty).Trim();
            var produto = Buscar(catalogo, entrada.ProdutoId);
            if (produto is null)
            {
                return new ItemPar
                {
                    DescricaoLida = descricao,
                    Nome = descricao,
                    Confianca = entrada.Confianca,
                    Pendente = entrada.Pendente,
                    NaoIdentificado = true,
                    Situacao = SituacaoPar.SemPar
                };
            }

            return new ItemPar
            {
                DescricaoLida = descricao,
                ProdutoId = produto.Id,
                Nome = (produto.Nome ?? string.Empty).Trim(),
                Confianca = entrada.Confianca,
                Pendente = entrada.Pendente,
                NaoIdentificado = false,
                Situacao = SituacaoPar.Aguardando
            };
        }

        private static ProdutoCesta? Buscar(IReadOnlyList<ProdutoCesta> catalogo, int? id)
        {
            if (id is not int valor || valor <= 0)
                return null;

            foreach (var produto in catalogo)
            {
                if (produto is not null && produto.Id == valor)
                    return produto;
            }

            return null;
        }
    }
}
