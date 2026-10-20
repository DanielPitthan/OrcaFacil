using System.Reflection;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>O que o banco vivo contém, para a abertura e para o backup.</summary>
public enum SituacaoConteudoBanco
{
    /// <summary>Vazio, ou só com categoria padrão do sistema. A abertura semeia.</summary>
    Vazio,

    /// <summary>Só a demonstração semeada e/ou o perfil mínimo "Usuário". A abertura não semeia por cima.</summary>
    SoDemonstracao,

    /// <summary>Qualquer outro dado, inclusive log. Sem perfil, a abertura cria só o "Usuário".</summary>
    DadoReal
}

/// <summary>
/// A abertura usa <see cref="HaDadoDoUsuarioAsync"/>: semeia só no vazio e nunca por cima.
/// Categoria com <c>EhPadraoDoSistema</c> não conta para ela, mesmo alterada.
/// O backup usa <see cref="ClassificarConteudoAsync"/>. Recusa no vazio e na demonstração
/// idêntica à semente; qualquer diferença pede confirmação. O lançamento guarda
/// <c>mesRef.AddDays(dia)</c>. O dia de "hoje" só entra no prazo das metas.
/// </summary>
public static class PoliticaSemeadura
{
    public const string MensagemPerfilMinimo =
        "SemPerfil com dados do usuário. O banco vivo foi mantido, o espelho não foi restaurado e só um perfil mínimo foi criado. Nenhum dado de demonstração foi semeado.";

    public const string NomePerfilMinimo = "Usuário";

    // contexto.TabelaQueNaoExiste fica só neste comentário para o teste da política ignorar texto comentado.

    /// <summary>Cada <see cref="DbSet{TEntity}"/> de <see cref="AppDbContext"/> entra nesta lista.</summary>
    public static readonly string[] ConjuntosObservados =
    [
        nameof(AppDbContext.MembrosFamilia),
        nameof(AppDbContext.Categorias),
        nameof(AppDbContext.Transacoes),
        nameof(AppDbContext.ItensLancamento),
        nameof(AppDbContext.Orcamentos),
        nameof(AppDbContext.Metas),
        nameof(AppDbContext.FechamentosPeriodo),
        nameof(AppDbContext.LogsEvento),
        nameof(AppDbContext.AtivosFinanceiros),
        nameof(AppDbContext.OperacoesAtivo),
        nameof(AppDbContext.Proventos),
        nameof(AppDbContext.ProdutosCesta),
        nameof(AppDbContext.LocaisCompra),
        nameof(AppDbContext.ComprasCesta),
        nameof(AppDbContext.ItensCompraCesta),
        nameof(AppDbContext.Cartoes),
        nameof(AppDbContext.RecargasCartao),
        nameof(AppDbContext.TitulosRendaFixa),
        nameof(AppDbContext.OperacoesRendaFixa),
        nameof(AppDbContext.RegrasCatalogoComprovante),
        nameof(AppDbContext.RegrasFiscais)
    ];

    /// <summary>Colunas de <see cref="Transacao"/> que o backup compara com a semente. Fora Id e CriadoEm.</summary>
    public static readonly string[] CamposTransacaoComparados =
    [
        nameof(Transacao.Descricao),
        nameof(Transacao.Valor),
        nameof(Transacao.Data),
        nameof(Transacao.Tipo),
        nameof(Transacao.Recorrencia),
        nameof(Transacao.Pago),
        nameof(Transacao.Observacao),
        nameof(Transacao.CategoriaId),
        nameof(Transacao.MembroFamiliaId),
        nameof(Transacao.TransacaoOrigemId),
        nameof(Transacao.CartaoId),
        nameof(Transacao.OperacaoRendaFixaId),
        nameof(Transacao.OperacaoAtivoId),
        nameof(Transacao.ProventoId),
        nameof(Transacao.AtualizadoEm)
    ];

    /// <summary>Colunas de <see cref="Orcamento"/> que o backup compara com a semente. Fora Id e CriadoEm.</summary>
    public static readonly string[] CamposOrcamentoComparados =
    [
        nameof(Orcamento.CategoriaId),
        nameof(Orcamento.ValorLimite),
        nameof(Orcamento.MetaReducaoPercentual),
        nameof(Orcamento.Mes),
        nameof(Orcamento.Ano)
    ];

    /// <summary>Colunas de <see cref="Meta"/> que o backup compara com a semente. Fora Id e CriadoEm.</summary>
    public static readonly string[] CamposMetaComparados =
    [
        nameof(Meta.Nome),
        nameof(Meta.ValorAlvo),
        nameof(Meta.ValorAtual),
        nameof(Meta.Prazo),
        nameof(Meta.Icone),
        nameof(Meta.Concluida),
        nameof(Meta.MembroFamiliaId)
    ];

    /// <summary>Colunas de <see cref="MembroFamilia"/> que o backup compara com o perfil mínimo. Fora Id e CriadoEm.</summary>
    public static readonly string[] CamposPerfilComparados =
    [
        nameof(MembroFamilia.Nome),
        nameof(MembroFamilia.Apelido),
        nameof(MembroFamilia.FotoPath),
        nameof(MembroFamilia.Avatar),
        nameof(MembroFamilia.Role),
        nameof(MembroFamilia.Ativo)
    ];

    /// <summary>Colunas de <see cref="Categoria"/> que o backup compara com a semente. Fora Id.</summary>
    public static readonly string[] CamposCategoriaComparados =
    [
        nameof(Categoria.Nome),
        nameof(Categoria.Icone),
        nameof(Categoria.CorHex),
        nameof(Categoria.TipoPadrao),
        nameof(Categoria.EhPadraoDoSistema),
        nameof(Categoria.PermiteItensFatura),
        nameof(Categoria.Ativo)
    ];

    /// <summary>
    /// Devolve true quando a semeadura de demonstração não deve rodar.
    /// </summary>
    public static async Task<bool> CriarPerfilMinimoSeHaDadoDoUsuarioAsync(
        AppDbContext contexto,
        IRegistroLog? log,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        if (!await HaDadoDoUsuarioAsync(contexto, cancellationToken))
            return false;

        log?.Informacao(MensagemPerfilMinimo, []);

        if (await contexto.MembrosFamilia.AnyAsync(cancellationToken))
            return true;

        contexto.MembrosFamilia.Add(new MembroFamilia
        {
            Nome = NomePerfilMinimo,
            Avatar = "👤",
            Ativo = true,
            CriadoEm = DateTime.UtcNow
        });
        await contexto.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Critério da abertura, inalterado. Categoria com <c>EhPadraoDoSistema</c> não conta,
    /// mesmo renomeada ou incompleta. Qualquer outra linha impede a semeadura.
    /// </summary>
    public static async Task<bool> HaDadoDoUsuarioAsync(AppDbContext contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (await contexto.MembrosFamilia.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Categorias.AnyAsync(categoria => !categoria.EhPadraoDoSistema, cancellationToken))
            return true;

        if (await contexto.Transacoes.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ItensLancamento.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Orcamentos.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Metas.AnyAsync(cancellationToken))
            return true;

        if (await contexto.FechamentosPeriodo.AnyAsync(cancellationToken))
            return true;

        if (await contexto.LogsEvento.AnyAsync(cancellationToken))
            return true;

        if (await contexto.AtivosFinanceiros.AnyAsync(cancellationToken))
            return true;

        if (await contexto.OperacoesAtivo.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Proventos.AnyAsync(cancellationToken))
            return true;

        if (await contexto.TitulosRendaFixa.AnyAsync(cancellationToken))
            return true;

        if (await contexto.OperacoesRendaFixa.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ProdutosCesta.AnyAsync(cancellationToken))
            return true;

        if (await contexto.LocaisCompra.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ComprasCesta.AnyAsync(cancellationToken))
            return true;

        if (await contexto.ItensCompraCesta.AnyAsync(cancellationToken))
            return true;

        if (await contexto.Cartoes.AnyAsync(cancellationToken))
            return true;

        if (await contexto.RecargasCartao.AnyAsync(cancellationToken))
            return true;

        if (await contexto.RegrasCatalogoComprovante.AnyAsync(
                regra => regra.LimiteProdutos != RegraCatalogoComprovante.LimitePadrao,
                cancellationToken))
            return true;

        return await RegrasFiscaisDiferemDaSementeAsync(contexto, cancellationToken);
    }

    /// <summary>
    /// Critério do backup. A semente inteira tem de estar presente, campo a campo.
    /// Linha a mais, linha a menos, data diferente ou categoria padrão diferente é
    /// <see cref="SituacaoConteudoBanco.DadoReal"/>. O perfil mínimo sozinho continua
    /// <see cref="SituacaoConteudoBanco.SoDemonstracao"/>.
    /// </summary>
    public static async Task<SituacaoConteudoBanco> ClassificarConteudoAsync(
        AppDbContext contexto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (await contexto.Categorias.AnyAsync(categoria => !categoria.EhPadraoDoSistema, cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ItensLancamento.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.FechamentosPeriodo.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.LogsEvento.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.AtivosFinanceiros.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.OperacoesAtivo.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.Proventos.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.TitulosRendaFixa.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.OperacoesRendaFixa.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ProdutosCesta.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.LocaisCompra.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ComprasCesta.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.ItensCompraCesta.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.Cartoes.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.RecargasCartao.AnyAsync(cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await contexto.RegrasCatalogoComprovante.AnyAsync(
                regra => regra.LimiteProdutos != RegraCatalogoComprovante.LimitePadrao,
                cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        if (await RegrasFiscaisDiferemDaSementeAsync(contexto, cancellationToken))
            return SituacaoConteudoBanco.DadoReal;

        var membros = await contexto.MembrosFamilia.AsNoTracking().ToListAsync(cancellationToken);
        if (!PerfisSoMinimos(membros))
            return SituacaoConteudoBanco.DadoReal;

        var categorias = await contexto.Categorias.AsNoTracking().ToListAsync(cancellationToken);
        if (categorias.Count > 0 && !CategoriasIdenticasASemente(categorias))
            return SituacaoConteudoBanco.DadoReal;

        var nomesCategoria = new Dictionary<int, string>();
        foreach (var categoria in categorias)
            nomesCategoria[categoria.Id] = categoria.Nome;

        var transacoes = await contexto.Transacoes.AsNoTracking().ToListAsync(cancellationToken);
        var orcamentos = await contexto.Orcamentos.AsNoTracking().ToListAsync(cancellationToken);
        var metas = await contexto.Metas.AsNoTracking().ToListAsync(cancellationToken);
        if (!LinhasIdenticasASemente(membros, transacoes, orcamentos, metas, nomesCategoria))
            return SituacaoConteudoBanco.DadoReal;

        if (membros.Count == 0 && transacoes.Count == 0 && orcamentos.Count == 0 && metas.Count == 0)
            return SituacaoConteudoBanco.Vazio;

        return SituacaoConteudoBanco.SoDemonstracao;
    }

    private static bool PerfisSoMinimos(IReadOnlyList<MembroFamilia> membros)
    {
        if (membros.Count > 1)
            return false;

        return membros.Count == 0 || EhPerfilMinimo(membros[0]);
    }

    private static bool EhPerfilMinimo(MembroFamilia membro)
        => CamposIguais(membro, CatalogoDemonstracao.CriarPerfil(), CamposPerfilComparados);

    private static bool CategoriasIdenticasASemente(IReadOnlyList<Categoria> gravadas)
    {
        var esperadas = CatalogoDemonstracao.CriarCategorias();
        var usados = new bool[esperadas.Count];
        foreach (var gravada in gravadas)
        {
            var achou = false;
            for (var i = 0; i < esperadas.Count; i++)
            {
                if (usados[i])
                    continue;

                if (CamposIguais(gravada, esperadas[i], CamposCategoriaComparados))
                {
                    usados[i] = true;
                    achou = true;
                    break;
                }
            }

            if (!achou)
                return false;
        }

        return VagasPreenchidas(usados);
    }

    private static bool LinhasIdenticasASemente(
        IReadOnlyList<MembroFamilia> membros,
        IReadOnlyList<Transacao> transacoes,
        IReadOnlyList<Orcamento> orcamentos,
        IReadOnlyList<Meta> metas,
        IReadOnlyDictionary<int, string> nomesCategoria)
    {
        if (transacoes.Count == 0 && orcamentos.Count == 0 && metas.Count == 0)
            return true;

        int? perfilId = membros.Count == 1 ? membros[0].Id : null;
        if ((transacoes.Count > 0 || metas.Count > 0) && perfilId is null)
            return false;

        var referencias = ReferenciasPossiveis(transacoes, orcamentos, metas, nomesCategoria, perfilId);
        if (referencias.Count != 1)
            return false;

        return EncaixaNaSemente(referencias[0], transacoes, orcamentos, metas, nomesCategoria, perfilId);
    }

    private static List<DateTime> ReferenciasPossiveis(
        IReadOnlyList<Transacao> transacoes,
        IReadOnlyList<Orcamento> orcamentos,
        IReadOnlyList<Meta> metas,
        IReadOnlyDictionary<int, string> nomesCategoria,
        int? perfilId)
    {
        if (metas.Count > 0)
            return ReferenciasDasMetas(metas, perfilId);

        if (orcamentos.Count > 0)
            return ReferenciasDosOrcamentos(orcamentos);

        return ReferenciasDosLancamentos(transacoes, nomesCategoria, perfilId);
    }

    private static List<DateTime> ReferenciasDasMetas(IReadOnlyList<Meta> metas, int? perfilId)
    {
        if (perfilId is null)
            return [];

        List<DateTime>? comuns = null;
        foreach (var meta in metas)
        {
            var datas = DatasQueExplicamMeta(meta, perfilId.Value);
            if (datas.Count == 0)
                return [];

            comuns = comuns is null ? datas : comuns.Where(datas.Contains).Distinct().ToList();
            if (comuns.Count == 0)
                return [];
        }

        return comuns ?? [];
    }

    private static List<DateTime> DatasQueExplicamMeta(Meta meta, int perfilId)
    {
        var datas = new List<DateTime>();
        if (meta.Concluida || meta.Prazo is null || meta.MembroFamiliaId != perfilId)
            return datas;

        foreach (var item in CatalogoDemonstracao.Metas)
        {
            if (item.Nome != meta.Nome
                || item.ValorAlvo != meta.ValorAlvo
                || item.ValorAtual != meta.ValorAtual
                || item.Icone != meta.Icone)
                continue;

            datas.AddRange(DiasQueGeramPrazo(meta.Prazo.Value, item.PrazoMeses));
        }

        return datas;
    }

    /// <summary>
    /// <c>AddMonths</c> no fim do mês não volta pelo caminho inverso.
    /// Por isso o dia candidato é o que, somado de novo, reproduz o prazo gravado.
    /// </summary>
    private static List<DateTime> DiasQueGeramPrazo(DateTime prazo, int meses)
    {
        var alvo = prazo.Date;
        var aprox = alvo.AddMonths(-meses);
        var inicio = new DateTime(aprox.Year, aprox.Month, 1).AddMonths(-1);
        var fim = inicio.AddMonths(3);
        var datas = new List<DateTime>();
        for (var dia = inicio; dia < fim; dia = dia.AddDays(1))
        {
            if (dia.AddMonths(meses).Date == alvo)
                datas.Add(dia);
        }

        return datas;
    }

    private static List<DateTime> ReferenciasDosOrcamentos(IReadOnlyList<Orcamento> orcamentos)
    {
        var mes = orcamentos[0].Mes;
        var ano = orcamentos[0].Ano;
        if (ano is < 1 or > 9999 || mes is < 1 or > 12 || orcamentos.Any(orcamento => orcamento.Mes != mes || orcamento.Ano != ano))
            return [];

        return [new DateTime(ano, mes, 1)];
    }

    private static List<DateTime> ReferenciasDosLancamentos(
        IReadOnlyList<Transacao> transacoes,
        IReadOnlyDictionary<int, string> nomesCategoria,
        int? perfilId)
    {
        if (perfilId is null)
            return [];

        List<DateTime>? comuns = null;
        foreach (var transacao in transacoes)
        {
            var meses = MesesQueExplicamLancamento(transacao, nomesCategoria, perfilId.Value);
            if (meses.Count == 0)
                return [];

            comuns = comuns is null ? meses : comuns.Where(meses.Contains).Distinct().ToList();
            if (comuns.Count == 0)
                return [];
        }

        return comuns ?? [];
    }

    private static List<DateTime> MesesQueExplicamLancamento(
        Transacao transacao,
        IReadOnlyDictionary<int, string> nomesCategoria,
        int perfilId)
    {
        var meses = new List<DateTime>();
        if (!CabeNoLancamento(transacao, perfilId, nomesCategoria, out var nomeCategoria))
            return meses;

        foreach (var item in CatalogoDemonstracao.Lancamentos)
        {
            if (item.Descricao != transacao.Descricao
                || item.Valor != transacao.Valor
                || item.Tipo != transacao.Tipo
                || item.Recorrencia != transacao.Recorrencia
                || item.Categoria != nomeCategoria)
                continue;

            var mesRef = new DateTime(transacao.Data.Year, transacao.Data.Month, 1);
            if (!MesmaData(transacao.Data, mesRef.AddDays(item.Dia)))
                continue;

            for (var deslocamento = 0; deslocamento < CatalogoDemonstracao.RepeticoesMensais; deslocamento++)
                meses.Add(mesRef.AddMonths(deslocamento));
        }

        return meses;
    }

    private static bool EncaixaNaSemente(
        DateTime hoje,
        IReadOnlyList<Transacao> transacoes,
        IReadOnlyList<Orcamento> orcamentos,
        IReadOnlyList<Meta> metas,
        IReadOnlyDictionary<int, string> nomesCategoria,
        int? perfilId)
    {
        if (!EncaixaLancamentos(hoje, transacoes, nomesCategoria, perfilId))
            return false;

        if (!EncaixaOrcamentos(hoje, orcamentos, nomesCategoria))
            return false;

        return EncaixaMetas(hoje, metas, perfilId);
    }

    private static bool EncaixaLancamentos(
        DateTime hoje,
        IReadOnlyList<Transacao> transacoes,
        IReadOnlyDictionary<int, string> nomesCategoria,
        int? perfilId)
    {
        if (!TryIdsPorNome(nomesCategoria, out var idsPorNome))
            return false;

        var esperados = CatalogoDemonstracao.LancamentosSemeados(hoje);
        var usados = new bool[esperados.Count];
        foreach (var transacao in transacoes)
        {
            var achou = false;
            for (var i = 0; i < esperados.Count && perfilId is not null; i++)
            {
                if (usados[i] || !idsPorNome.TryGetValue(esperados[i].Categoria, out var categoriaId))
                    continue;

                var modelo = new Transacao
                {
                    Descricao = esperados[i].Descricao,
                    Valor = esperados[i].Valor,
                    Data = esperados[i].Data,
                    Tipo = esperados[i].Tipo,
                    Recorrencia = esperados[i].Recorrencia,
                    Pago = true,
                    CategoriaId = categoriaId,
                    MembroFamiliaId = perfilId.Value
                };
                if (CamposIguais(transacao, modelo, CamposTransacaoComparados))
                {
                    usados[i] = true;
                    achou = true;
                    break;
                }
            }

            if (!achou)
                return false;
        }

        return VagasPreenchidas(usados);
    }

    private static bool CabeNoLancamento(
        Transacao transacao,
        int perfilId,
        IReadOnlyDictionary<int, string> nomesCategoria,
        out string nomeCategoria)
    {
        nomeCategoria = "";
        if (transacao.MembroFamiliaId != perfilId
            || !transacao.Pago
            || transacao.Observacao is not null
            || transacao.CartaoId is not null
            || transacao.TransacaoOrigemId is not null
            || transacao.AtualizadoEm is not null)
            return false;

        return nomesCategoria.TryGetValue(transacao.CategoriaId, out nomeCategoria!)
            && !string.IsNullOrEmpty(nomeCategoria);
    }

    private static bool EncaixaOrcamentos(
        DateTime hoje,
        IReadOnlyList<Orcamento> orcamentos,
        IReadOnlyDictionary<int, string> nomesCategoria)
    {
        if (!TryIdsPorNome(nomesCategoria, out var idsPorNome))
            return false;

        var esperados = CatalogoDemonstracao.OrcamentosSemeados(hoje);
        var usados = new bool[esperados.Count];
        foreach (var orcamento in orcamentos)
        {
            var achou = false;
            for (var i = 0; i < esperados.Count; i++)
            {
                if (usados[i] || !idsPorNome.TryGetValue(esperados[i].Categoria, out var categoriaId))
                    continue;

                var modelo = new Orcamento
                {
                    CategoriaId = categoriaId,
                    ValorLimite = esperados[i].ValorLimite,
                    Mes = esperados[i].Mes,
                    Ano = esperados[i].Ano
                };
                if (CamposIguais(orcamento, modelo, CamposOrcamentoComparados))
                {
                    usados[i] = true;
                    achou = true;
                    break;
                }
            }

            if (!achou)
                return false;
        }

        return VagasPreenchidas(usados);
    }

    private static bool EncaixaMetas(DateTime hoje, IReadOnlyList<Meta> metas, int? perfilId)
    {
        var esperados = CatalogoDemonstracao.MetasSemeadas(hoje);
        var usados = new bool[esperados.Count];
        foreach (var meta in metas)
        {
            var achou = false;
            for (var i = 0; i < esperados.Count && perfilId is not null; i++)
            {
                if (usados[i])
                    continue;

                var modelo = new Meta
                {
                    Nome = esperados[i].Nome,
                    ValorAlvo = esperados[i].ValorAlvo,
                    ValorAtual = esperados[i].ValorAtual,
                    Prazo = esperados[i].Prazo,
                    Icone = esperados[i].Icone,
                    Concluida = false,
                    MembroFamiliaId = perfilId.Value
                };
                if (CamposIguais(meta, modelo, CamposMetaComparados))
                {
                    usados[i] = true;
                    achou = true;
                    break;
                }
            }

            if (!achou)
                return false;
        }

        return VagasPreenchidas(usados);
    }

    private static bool TryIdsPorNome(IReadOnlyDictionary<int, string> nomesCategoria, out Dictionary<string, int> idsPorNome)
    {
        idsPorNome = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var par in nomesCategoria)
        {
            if (!idsPorNome.TryAdd(par.Value, par.Key))
                return false;
        }

        return true;
    }

    private static bool VagasPreenchidas(bool[] usados)
    {
        foreach (var usada in usados)
        {
            if (!usada)
                return false;
        }

        return true;
    }

    private static bool CamposIguais<T>(T gravado, T semente, string[] campos)
    {
        foreach (var campo in campos)
        {
            var propriedade = typeof(T).GetProperty(campo, BindingFlags.Instance | BindingFlags.Public);
            if (propriedade is null)
                return false;

            var atual = propriedade.GetValue(gravado);
            var esperado = propriedade.GetValue(semente);
            if (atual is DateTime dataAtual && esperado is DateTime dataEsperada)
            {
                if (!MesmaData(dataAtual, dataEsperada))
                    return false;
                continue;
            }

            if (!Equals(atual, esperado))
                return false;
        }

        return true;
    }

    private static bool MesmaData(DateTime gravada, DateTime esperada)
        => gravada.Date == esperada.Date && gravada.TimeOfDay.Ticks == esperada.TimeOfDay.Ticks;

    /// <summary>A semente da migration não é dado do usuário. Linha a mais ou valor diferente é.</summary>
    private static async Task<bool> RegrasFiscaisDiferemDaSementeAsync(
        AppDbContext contexto,
        CancellationToken cancellationToken)
    {
        var gravadas = await contexto.RegrasFiscais.AsNoTracking().OrderBy(regra => regra.Id).ToListAsync(cancellationToken);
        var esperadas = CatalogoRegrasFiscais.Linhas().OrderBy(regra => regra.Id).ToList();
        if (gravadas.Count != esperadas.Count)
            return true;

        for (var i = 0; i < gravadas.Count; i++)
        {
            var gravada = gravadas[i];
            var esperada = esperadas[i];
            if (gravada.Id != esperada.Id
                || !string.Equals(gravada.Codigo, esperada.Codigo, StringComparison.Ordinal)
                || gravada.Valor != esperada.Valor
                || gravada.VigenciaInicio.Date != esperada.VigenciaInicio.Date
                || gravada.VigenciaFim?.Date != esperada.VigenciaFim?.Date
                || gravada.EhSistema != esperada.EhSistema)
                return true;
        }

        return false;
    }
}
