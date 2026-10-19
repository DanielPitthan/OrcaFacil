using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.Planilha;
using OrcaFacil.Core.Tema;

namespace OrcaFacil.Core.Exportacao;

/// <summary>
/// Lê o banco com <c>AsNoTracking</c> e grava um xlsx. Cada conjunto do contexto
/// tem decisão explícita: vira aba ou fica de fora. A configuração chega pronta,
/// sem chave, sem URI e sem senha.
/// </summary>
public static class ExportadorPlanilha
{
    public static IReadOnlyList<string> NomesDeAba { get; } =
    [
        "Perfis",
        "Categorias",
        "Lançamentos",
        "Itens da fatura",
        "Orçamentos",
        "Metas",
        "Fechamentos",
        "Ativos",
        "Operações",
        "Produtos da cesta",
        "Locais de compra",
        "Compras da cesta",
        "Itens da compra",
        "Cartões",
        "Recargas",
        "Configurações"
    ];

    /// <summary>Nome do <see cref="DbSet{TEntity}"/> para o nome da aba. Não inclui os excluídos.</summary>
    public static IReadOnlyDictionary<string, string> AbasPorConjunto { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(AppDbContext.MembrosFamilia)] = "Perfis",
            [nameof(AppDbContext.Categorias)] = "Categorias",
            [nameof(AppDbContext.Transacoes)] = "Lançamentos",
            [nameof(AppDbContext.ItensLancamento)] = "Itens da fatura",
            [nameof(AppDbContext.Orcamentos)] = "Orçamentos",
            [nameof(AppDbContext.Metas)] = "Metas",
            [nameof(AppDbContext.FechamentosPeriodo)] = "Fechamentos",
            [nameof(AppDbContext.AtivosFinanceiros)] = "Ativos",
            [nameof(AppDbContext.OperacoesAtivo)] = "Operações",
            [nameof(AppDbContext.ProdutosCesta)] = "Produtos da cesta",
            [nameof(AppDbContext.LocaisCompra)] = "Locais de compra",
            [nameof(AppDbContext.ComprasCesta)] = "Compras da cesta",
            [nameof(AppDbContext.ItensCompraCesta)] = "Itens da compra",
            [nameof(AppDbContext.Cartoes)] = "Cartões",
            [nameof(AppDbContext.RecargasCartao)] = "Recargas"
        };

    /// <summary>Conjuntos que existem no contexto e não entram na planilha.</summary>
    public static IReadOnlySet<string> ConjuntosExcluidos { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(AppDbContext.LogsEvento)
        };

    private static readonly (PrazoAviso Flag, string Texto)[] TextosPrazo =
    [
        (PrazoAviso.TresDiasAntes, "3 dias antes do vencimento"),
        (PrazoAviso.UmDiaAntes, "1 dia antes do vencimento"),
        (PrazoAviso.NoDia, "No dia do vencimento"),
        (PrazoAviso.UmDiaDepois, "1 dia depois do vencimento"),
        (PrazoAviso.Vencidas, "Diariamente enquanto estiver vencida (até 7 dias)")
    ];

    public static void Exportar(AppDbContext contexto, ConfiguracaoExportavel configuracao, Stream destino)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(configuracao);
        ArgumentNullException.ThrowIfNull(destino);

        using var pasta = new XLWorkbook();
        GravarPerfis(contexto, pasta);
        GravarCategorias(contexto, pasta);
        GravarLancamentos(contexto, pasta);
        GravarItensFatura(contexto, pasta);
        GravarOrcamentos(contexto, pasta);
        GravarMetas(contexto, pasta);
        GravarFechamentos(contexto, pasta);
        GravarAtivos(contexto, pasta);
        GravarOperacoes(contexto, pasta);
        GravarProdutos(contexto, pasta);
        GravarLocais(contexto, pasta);
        GravarCompras(contexto, pasta);
        GravarItensCompra(contexto, pasta);
        GravarCartoes(contexto, pasta);
        GravarRecargas(contexto, pasta);
        GravarConfiguracoes(configuracao, pasta);
        pasta.SaveAs(destino);
        if (destino.CanSeek)
            destino.Position = 0;
    }

    private static void GravarPerfis(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Perfis");
        PlanilhaPtBr.GravarCabecalho(aba, ["Nome", "Apelido", "Papel", "Ativo", "Foto", "Criado em"]);
        EmLotes(contexto.MembrosFamilia.AsNoTracking().OrderBy(m => m.Id), "Perfis", (membro, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), membro.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), membro.Apelido);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 3), TextoPapel(membro.Role));
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 4), membro.Ativo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 5), NomeDeFoto(membro.FotoPath));
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 6), InstanteUtc(membro.CriadoEm));
        });
    }

    private static void GravarCategorias(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Categorias");
        PlanilhaPtBr.GravarCabecalho(aba, ["Nome", "Ícone", "Cor", "Tipo padrão", "Padrão do sistema", "Permite itens de fatura", "Ativa"]);
        EmLotes(contexto.Categorias.AsNoTracking().OrderBy(c => c.Id), "Categorias", (categoria, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), categoria.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), categoria.Icone);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 3), categoria.CorHex);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 4), TextoTipo(categoria.TipoPadrao));
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 5), categoria.EhPadraoDoSistema);
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 6), categoria.PermiteItensFatura);
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 7), categoria.Ativo);
        });
    }

    private static void GravarLancamentos(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Lançamentos");
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Descrição", "Valor", "Data", "Tipo", "Recorrência", "Pago", "Observação",
            "Categoria", "Perfil", "Lançamento de origem", "Cartão", "Criado em"
        ]);
        EmLotes(contexto.Transacoes.AsNoTracking()
            .Include(t => t.Categoria)
            .Include(t => t.MembroFamilia)
            .Include(t => t.Cartao)
            .Include(t => t.TransacaoOrigem)
            .OrderBy(t => t.Id), "Lançamentos", (transacao, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), transacao.Descricao);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 2), transacao.Valor);
            PlanilhaPtBr.GravarData(PlanilhaPtBr.Celula(aba, linha, 3), transacao.Data);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 4), TextoTipo(transacao.Tipo));
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 5), TextoRecorrencia(transacao.Recorrencia));
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 6), transacao.Pago);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 7), transacao.Observacao);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 8), transacao.Categoria?.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 9), TextoPerfil(transacao.MembroFamilia));
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 10), TextoOrigem(transacao.TransacaoOrigem));
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 11), transacao.Cartao?.Rotulo);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 12), InstanteUtc(transacao.CriadoEm));
        });
    }

    private static void GravarItensFatura(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Itens da fatura");
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Descrição", "Descritivo", "Valor", "Estorno", "Natureza", "Parcela",
            "Total de parcelas", "Ordem", "Lançamento", "Categoria"
        ]);
        EmLotes(contexto.ItensLancamento.AsNoTracking()
            .Include(i => i.Transacao)
            .Include(i => i.Categoria)
            .OrderBy(i => i.Id), "Itens da fatura", (item, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), item.Descricao);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), item.Descritivo);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 3), item.Valor);
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 4), item.EhEstorno);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 5), TextoNatureza(item.Natureza));
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 6), item.ParcelaAtual);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 7), item.TotalParcelas);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 8), item.Ordem);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 9), item.Transacao?.Descricao);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 10), item.Categoria?.Nome);
        });
    }

    private static void GravarOrcamentos(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Orçamentos");
        PlanilhaPtBr.GravarCabecalho(aba, ["Categoria", "Limite", "Meta de redução", "Mês", "Ano", "Criado em"]);
        EmLotes(contexto.Orcamentos.AsNoTracking()
            .Include(o => o.Categoria)
            .OrderBy(o => o.Id), "Orçamentos", (orcamento, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), orcamento.Categoria?.Nome);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 2), orcamento.ValorLimite);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 3), orcamento.MetaReducaoPercentual);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 4), orcamento.Mes);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 5), orcamento.Ano);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 6), InstanteUtc(orcamento.CriadoEm));
        });
    }

    private static void GravarMetas(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Metas");
        PlanilhaPtBr.GravarCabecalho(aba,
            ["Nome", "Valor alvo", "Valor atual", "Prazo", "Ícone", "Concluída", "Perfil", "Percentual", "Criado em"]);
        EmLotes(contexto.Metas.AsNoTracking()
            .Include(m => m.MembroFamilia)
            .OrderBy(m => m.Id), "Metas", (meta, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), meta.Nome);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 2), meta.ValorAlvo);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 3), meta.ValorAtual);
            PlanilhaPtBr.GravarData(PlanilhaPtBr.Celula(aba, linha, 4), meta.Prazo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 5), meta.Icone);
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 6), meta.Concluida);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 7), TextoPerfil(meta.MembroFamilia));
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 8), meta.PercentualConcluido);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 9), InstanteUtc(meta.CriadoEm));
        });
    }

    private static void GravarFechamentos(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Fechamentos");
        PlanilhaPtBr.GravarCabecalho(aba, ["Mês", "Ano", "Fechado", "Atualizado em"]);
        EmLotes(contexto.FechamentosPeriodo.AsNoTracking().OrderBy(f => f.Id), "Fechamentos", (fechamento, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 1), fechamento.Mes);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 2), fechamento.Ano);
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 3), fechamento.Fechado);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 4), InstanteUtc(fechamento.AtualizadoEm));
        });
    }

    private static void GravarAtivos(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Ativos");
        PlanilhaPtBr.GravarCabecalho(aba, ["Ticker", "Nome", "Tipo", "Ativo", "Criado em"]);
        EmLotes(contexto.AtivosFinanceiros.AsNoTracking().OrderBy(a => a.Id), "Ativos", (ativo, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), ativo.Ticker);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), ativo.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 3), TextoAtivo(ativo.Tipo));
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 4), ativo.Ativo);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 5), InstanteUtc(ativo.CriadoEm));
        });
    }

    private static void GravarOperacoes(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Operações");
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Ticker", "Nome do ativo", "Perfil", "Operação", "Data", "Quantidade",
            "Preço unitário", "Custos", "Corretora", "Criado em", "Atualizado em"
        ]);
        EmLotes(contexto.OperacoesAtivo.AsNoTracking()
            .Include(o => o.Ativo)
            .Include(o => o.MembroFamilia)
            .OrderBy(o => o.Id), "Operações", (operacao, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), operacao.Ativo?.Ticker);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), operacao.Ativo?.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 3), TextoPerfil(operacao.MembroFamilia));
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 4), TextoOperacao(operacao.Tipo));
            PlanilhaPtBr.GravarData(PlanilhaPtBr.Celula(aba, linha, 5), operacao.Data);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 6), operacao.Quantidade);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 7), operacao.PrecoUnitario);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 8), operacao.Custos);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 9), operacao.Corretora);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 10), InstanteUtc(operacao.CriadoEm));
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 11), InstanteUtc(operacao.AtualizadoEm));
        });
    }

    private static void GravarProdutos(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Produtos da cesta");
        PlanilhaPtBr.GravarCabecalho(aba, ["Nome", "Categoria", "Ativo", "Criado em"]);
        EmLotes(contexto.ProdutosCesta.AsNoTracking()
            .Include(p => p.Categoria)
            .OrderBy(p => p.Id), "Produtos da cesta", (produto, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), produto.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), produto.Categoria?.Nome);
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 3), produto.Ativo);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 4), InstanteUtc(produto.CriadoEm));
        });
    }

    private static void GravarLocais(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Locais de compra");
        PlanilhaPtBr.GravarCabecalho(aba, ["Nome", "Ativo", "Criado em"]);
        EmLotes(contexto.LocaisCompra.AsNoTracking().OrderBy(l => l.Id), "Locais de compra", (local, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), local.Nome);
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 2), local.Ativo);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 3), InstanteUtc(local.CriadoEm));
        });
    }

    private static void GravarCompras(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Compras da cesta");
        PlanilhaPtBr.GravarCabecalho(aba,
        [
            "Mês", "Ano", "Data", "Local", "Benefício", "Tipo do benefício", "Cartão de benefício",
            "Dinheiro", "Valor no crédito", "Cartão de crédito", "Item da fatura", "Lançamento", "Criado em"
        ]);
        EmLotes(contexto.ComprasCesta.AsNoTracking()
            .Include(c => c.LocalCompra)
            .Include(c => c.CartaoBeneficio)
            .Include(c => c.CartaoCredito)
            .Include(c => c.ItemFatura)
            .Include(c => c.Transacao)
            .OrderBy(c => c.Id), "Compras da cesta", (compra, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 1), compra.Mes);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 2), compra.Ano);
            PlanilhaPtBr.GravarData(PlanilhaPtBr.Celula(aba, linha, 3), compra.Data);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 4), compra.LocalCompra?.Nome);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 5), compra.ValorBeneficio);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 6), TextoBeneficio(compra.TipoCartaoBeneficio));
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 7), compra.CartaoBeneficio?.Rotulo);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 8), compra.ValorDinheiro);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 9), compra.ValorCartaoCredito);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 10), compra.CartaoCredito?.Rotulo);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 11), TextoItemFatura(compra.ItemFatura));
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 12), compra.Transacao?.Descricao);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 13), InstanteUtc(compra.CriadoEm));
        });
    }

    private static void GravarItensCompra(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Itens da compra");
        PlanilhaPtBr.GravarCabecalho(aba, ["Data da compra", "Local", "Produto", "Preço", "Ordem"]);
        EmLotes(contexto.ItensCompraCesta.AsNoTracking()
            .Include(i => i.CompraCesta!).ThenInclude(c => c.LocalCompra)
            .Include(i => i.Produto)
            .OrderBy(i => i.Id), "Itens da compra", (item, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarData(PlanilhaPtBr.Celula(aba, linha, 1), item.CompraCesta?.Data);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), item.CompraCesta?.LocalCompra?.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 3), item.Produto?.Nome);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 4), item.Preco);
            PlanilhaPtBr.GravarNumero(PlanilhaPtBr.Celula(aba, linha, 5), item.Ordem);
        });
    }

    private static void GravarCartoes(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Cartões");
        PlanilhaPtBr.GravarCabecalho(aba, ["Nome", "Bandeira", "Últimos dígitos", "Tipo", "Tipo do benefício", "Ativo", "Criado em"]);
        EmLotes(contexto.Cartoes.AsNoTracking().OrderBy(c => c.Id), "Cartões", (cartao, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), cartao.Nome);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), cartao.Bandeira);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 3), cartao.UltimosDigitos);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 4), TextoCartao(cartao.Tipo));
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 5), TextoBeneficio(cartao.TipoBeneficio));
            PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 6), cartao.Ativo);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 7), InstanteUtc(cartao.CriadoEm));
        });
    }

    private static void GravarRecargas(AppDbContext contexto, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Recargas");
        PlanilhaPtBr.GravarCabecalho(aba, ["Cartão", "Data", "Valor", "Criado em"]);
        EmLotes(contexto.RecargasCartao.AsNoTracking()
            .Include(r => r.Cartao)
            .OrderBy(r => r.Id), "Recargas", (recarga, indice) =>
        {
            var linha = LinhaDeDados(indice);
            PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), recarga.Cartao?.Rotulo);
            PlanilhaPtBr.GravarData(PlanilhaPtBr.Celula(aba, linha, 2), recarga.Data);
            PlanilhaPtBr.GravarMoeda(PlanilhaPtBr.Celula(aba, linha, 3), recarga.Valor);
            PlanilhaPtBr.GravarDataHora(PlanilhaPtBr.Celula(aba, linha, 4), InstanteUtc(recarga.CriadoEm));
        });
    }

    private static void GravarConfiguracoes(ConfiguracaoExportavel configuracao, XLWorkbook pasta)
    {
        var aba = Abrir(pasta, "Configurações");
        PlanilhaPtBr.GravarCabecalho(aba, ["Item", "Valor"]);
        EscreverItem(aba, 2, "Tema", TextoTema(configuracao.Tema));
        EscreverSimNao(aba, 3, "Notificações", configuracao.NotificacoesAtivas);
        EscreverSimNao(aba, 4, "Som", configuracao.SomAtivo);
        EscreverSimNao(aba, 5, "Gráfico de lançamentos expandido", configuracao.GraficoLancamentosExpandido);
        EscreverItem(aba, 6, "Prazos de aviso", TextoPrazos(configuracao.PrazosAviso));
        EscreverItem(aba, 7, "Provedor", configuracao.Provedor);
        EscreverItem(aba, 8, "URL", UrlSemSegredo(configuracao.Url));
        EscreverItem(aba, 9, "Modelo", configuracao.Modelo);
    }

    private static void EscreverItem(IXLWorksheet aba, int linha, string item, string? valor)
    {
        PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), item);
        PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 2), valor);
    }

    private static void EscreverSimNao(IXLWorksheet aba, int linha, string item, bool valor)
    {
        PlanilhaPtBr.GravarTexto(PlanilhaPtBr.Celula(aba, linha, 1), item);
        PlanilhaPtBr.GravarSimNao(PlanilhaPtBr.Celula(aba, linha, 2), valor);
    }

    private static IXLWorksheet Abrir(XLWorkbook pasta, string nome) => PlanilhaPtBr.CriarAba(pasta, nome);

    /// <summary>Lote da leitura paginada. O teste passa disso para provar a segunda página.</summary>
    internal const int TamanhoLote = 200;

    private static void EmLotes<T>(IQueryable<T> consulta, string aba, Action<T, int> gravar)
    {
        var total = consulta.Count();
        ExigirLimiteDeDados(aba, total);

        var pulados = 0;
        while (pulados < total)
        {
            var lote = consulta.Skip(pulados).Take(TamanhoLote).ToList();
            if (lote.Count == 0)
                break;

            for (var i = 0; i < lote.Count; i++)
                gravar(lote[i], pulados + i);

            pulados += lote.Count;
        }
    }

    /// <summary>A primeira linha da aba é o cabeçalho. O Excel aceita 1.048.575 linhas de dados.</summary>
    internal static void ExigirLimiteDeDados(string aba, int linhasDeDados)
    {
        var teto = PlanilhaPtBr.MaximoLinhas - 1;
        if (linhasDeDados > teto)
            throw new InvalidOperationException(
                $"A aba {aba} tem {linhasDeDados} linhas de dados. O Excel aceita no máximo {teto} além do cabeçalho.");
    }

    /// <summary>
    /// CriadoEm e AtualizadoEm são gravados com <see cref="DateTime.UtcNow"/>.
    /// O SQLite devolve o mesmo relógio com <see cref="DateTimeKind.Unspecified"/>.
    /// Kind Local vira UTC antes de São Paulo.
    /// </summary>
    internal static DateTime InstanteUtc(DateTime instante) => instante.Kind switch
    {
        DateTimeKind.Utc => instante,
        DateTimeKind.Local => instante.ToUniversalTime(),
        _ => DateTime.SpecifyKind(instante, DateTimeKind.Utc)
    };

    private static DateTime? InstanteUtc(DateTime? instante)
        => instante is null ? null : InstanteUtc(instante.Value);

    private static int LinhaDeDados(int indice)
    {
        var linha = indice + 2;
        if (linha > PlanilhaPtBr.MaximoLinhas)
            throw new InvalidOperationException(
                $"A exportação parou: o Excel aceita no máximo {PlanilhaPtBr.MaximoLinhas} linhas e a primeira é o cabeçalho.");

        return linha;
    }

    private static string TextoPerfil(MembroFamilia? membro)
    {
        if (membro is null)
            return "";

        if (string.IsNullOrWhiteSpace(membro.Apelido))
            return membro.Nome;

        return $"{membro.Nome} ({membro.Apelido})";
    }

    private static string TextoOrigem(Transacao? origem)
    {
        if (origem is null)
            return "";

        var data = origem.Data.Kind == DateTimeKind.Utc
            ? TimeZoneInfo.ConvertTimeFromUtc(origem.Data, PlanilhaPtBr.FusoBrasil)
            : origem.Data;
        return origem.Descricao + " — " + data.ToString("dd'/'MM'/'yyyy", PlanilhaPtBr.Cultura);
    }

    /// <summary>
    /// Caminho relativo sai como está. Caminho absoluto, URI ou letra de unidade
    /// vira só o nome do arquivo, sem pasta e sem consulta.
    /// </summary>
    internal static string NomeDeFoto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return "";

        if (!EhCaminhoAbsoluto(valor))
            return valor;

        var texto = valor.Trim();
        var corte = Math.Max(texto.LastIndexOf('/'), texto.LastIndexOf('\\'));
        var nome = corte < 0 ? texto : texto[(corte + 1)..];
        var parametro = nome.IndexOfAny(['?', '#', ';']);
        return parametro < 0 ? nome : nome[..parametro];
    }

    private static bool EhCaminhoAbsoluto(string valor)
    {
        if (valor.Contains("://", StringComparison.Ordinal))
            return true;

        if (valor.StartsWith("//", StringComparison.Ordinal) || valor.StartsWith("\\\\", StringComparison.Ordinal))
            return true;

        if (valor.Length >= 3 && char.IsLetter(valor[0]) && valor[1] == ':' && valor[2] is '\\' or '/')
            return true;

        return Path.IsPathRooted(valor);
    }

    private static string TextoItemFatura(ItemLancamento? item)
    {
        if (item is null)
            return "";

        return string.IsNullOrWhiteSpace(item.Descritivo) ? item.Descricao : item.Descritivo;
    }

    /// <summary>
    /// Esquema, host, porta e caminho. Sem userinfo. Corta em <c>;</c>, <c>?</c> e <c>#</c>,
    /// inclusive o parâmetro de matriz <c>;token=</c>.
    /// </summary>
    internal static string? UrlSemSegredo(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (Uri.TryCreate(url, UriKind.Absolute, out var absoluta) && absoluta.IsAbsoluteUri && !string.IsNullOrEmpty(absoluta.Host))
        {
            var autoridade = absoluta.Authority;
            var arroba = autoridade.LastIndexOf('@');
            if (arroba >= 0)
                autoridade = autoridade[(arroba + 1)..];

            var caminho = absoluta.AbsolutePath;
            var corte = caminho.IndexOfAny([';', '?', '#']);
            if (corte >= 0)
                caminho = caminho[..corte];

            return absoluta.Scheme + "://" + autoridade + caminho;
        }

        var indice = url.IndexOfAny([';', '?', '#']);
        return indice < 0 ? url : url[..indice];
    }

    internal static string TextoPrazos(PrazoAviso prazos)
    {
        const PrazoAviso conhecidos =
            PrazoAviso.TresDiasAntes | PrazoAviso.UmDiaAntes | PrazoAviso.NoDia
            | PrazoAviso.UmDiaDepois | PrazoAviso.Vencidas;
        if ((prazos & ~conhecidos) != 0)
            throw new InvalidOperationException($"Prazo de aviso desconhecido: {prazos}.");

        var partes = new List<string>();
        foreach (var (flag, texto) in TextosPrazo)
        {
            if (prazos.HasFlag(flag) && flag != PrazoAviso.Nenhum)
                partes.Add(texto);
        }

        return string.Join(", ", partes);
    }

    internal static string TextoTema(TemaPreferido tema) => tema switch
    {
        TemaPreferido.Automatico => "Automático",
        TemaPreferido.Claro => "Claro",
        TemaPreferido.Escuro => "Escuro",
        _ => throw new InvalidOperationException($"Tema desconhecido: {tema}.")
    };

    internal static string TextoPapel(PerfilRole papel) => papel switch
    {
        PerfilRole.Admin => "Administrador",
        PerfilRole.Visualizador => "Visualizador",
        _ => throw new InvalidOperationException($"Papel desconhecido: {papel}.")
    };

    internal static string TextoTipo(TipoTransacao tipo) => tipo switch
    {
        TipoTransacao.Receita => "Receita",
        TipoTransacao.Despesa => "Despesa",
        _ => throw new InvalidOperationException($"Tipo de lançamento desconhecido: {tipo}.")
    };

    internal static string TextoRecorrencia(TipoRecorrencia recorrencia) => recorrencia switch
    {
        TipoRecorrencia.Unica => "Única",
        TipoRecorrencia.Semanal => "Semanal",
        TipoRecorrencia.Mensal => "Mensal",
        TipoRecorrencia.Anual => "Anual",
        _ => throw new InvalidOperationException($"Recorrência desconhecida: {recorrencia}.")
    };

    internal static string TextoNatureza(NaturezaItemLancamento natureza) => natureza switch
    {
        NaturezaItemLancamento.Avulsa => "Avulsa",
        NaturezaItemLancamento.Parcelada => "Parcelada",
        NaturezaItemLancamento.Assinatura => "Assinatura",
        _ => throw new InvalidOperationException($"Natureza desconhecida: {natureza}.")
    };

    internal static string TextoAtivo(TipoAtivo tipo) => tipo switch
    {
        TipoAtivo.Acao => "Ação",
        TipoAtivo.Fii => "FII",
        TipoAtivo.Etf => "ETF",
        TipoAtivo.Bdr => "BDR",
        _ => throw new InvalidOperationException($"Tipo de ativo desconhecido: {tipo}.")
    };

    internal static string TextoOperacao(TipoOperacao tipo) => tipo switch
    {
        TipoOperacao.Compra => "Compra",
        TipoOperacao.Venda => "Venda",
        _ => throw new InvalidOperationException($"Operação desconhecida: {tipo}.")
    };

    internal static string TextoCartao(TipoCartao tipo) => tipo switch
    {
        TipoCartao.Credito => "Crédito",
        TipoCartao.Beneficio => "Benefício",
        _ => throw new InvalidOperationException($"Tipo de cartão desconhecido: {tipo}.")
    };

    internal static string TextoBeneficio(TipoCartaoBeneficio? tipo) => tipo switch
    {
        null => "",
        TipoCartaoBeneficio.ValeAlimentacao => "Vale alimentação",
        TipoCartaoBeneficio.ValeRefeicao => "Vale refeição",
        TipoCartaoBeneficio.Outro => "Outro",
        _ => throw new InvalidOperationException($"Tipo de benefício desconhecido: {tipo}.")
    };
}
