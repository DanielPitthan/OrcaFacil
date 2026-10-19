using Microsoft.EntityFrameworkCore;
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
/// Uma função e uma lista de tabelas. A abertura semeia apenas em
/// <see cref="SituacaoConteudoBanco.Vazio"/> e nunca semeia por cima. Sem perfil e com
/// dado real, cria só o perfil mínimo. O backup, quando já existe espelho, recusa em
/// vazio e em <see cref="SituacaoConteudoBanco.SoDemonstracao"/> e pede confirmação em
/// <see cref="SituacaoConteudoBanco.DadoReal"/>. Para o backup, demonstração é a linha
/// idêntica à semente, datas incluídas. A data de referência sai das próprias linhas,
/// não do relógio: a abertura do app grava <c>DateTime.Today</c> uma vez.
/// </summary>
public static class PoliticaSemeadura
{
    public const string MensagemPerfilMinimo =
        "SemPerfil com dados do usuário. O banco vivo foi mantido, o espelho não foi restaurado e só um perfil mínimo foi criado. Nenhum dado de demonstração foi semeado.";

    public const string NomePerfilMinimo = "Usuário";

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
        nameof(AppDbContext.ProdutosCesta),
        nameof(AppDbContext.LocaisCompra),
        nameof(AppDbContext.ComprasCesta),
        nameof(AppDbContext.ItensCompraCesta),
        nameof(AppDbContext.Cartoes),
        nameof(AppDbContext.RecargasCartao)
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
    /// A abertura semeia só quando a classificação é <see cref="SituacaoConteudoBanco.Vazio"/>.
    /// </summary>
    public static async Task<bool> HaDadoDoUsuarioAsync(AppDbContext contexto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return await ClassificarConteudoAsync(contexto, cancellationToken) != SituacaoConteudoBanco.Vazio;
    }

    /// <summary>
    /// Categoria padrão do sistema não conta, mesmo com os outros campos diferentes.
    /// No backup, lançamento, orçamento, meta e o perfil "Usuário" só ficam em
    /// <see cref="SituacaoConteudoBanco.SoDemonstracao"/> quando repetem a semente em
    /// todos os campos persistidos, fora o Id e o <c>CriadoEm</c> (o relógio da inserção).
    /// Qualquer diferença, inclusive mês, dia ou uma linha a mais, é
    /// <see cref="SituacaoConteudoBanco.DadoReal"/>. A abertura não muda: semeia só no vazio.
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

        var membros = await contexto.MembrosFamilia.AsNoTracking().ToListAsync(cancellationToken);
        if (!PerfisSoMinimos(membros))
            return SituacaoConteudoBanco.DadoReal;

        var nomesCategoria = await contexto.Categorias.AsNoTracking()
            .ToDictionaryAsync(categoria => categoria.Id, categoria => categoria.Nome, cancellationToken);

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
    {
        var semente = CatalogoDemonstracao.CriarPerfil();
        return membro.Nome == semente.Nome
            && membro.Avatar == semente.Avatar
            && membro.Ativo == semente.Ativo
            && membro.Role == semente.Role
            && membro.Apelido == semente.Apelido
            && membro.FotoPath == semente.FotoPath;
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

        foreach (var hoje in ReferenciasPossiveis(transacoes, orcamentos, metas, nomesCategoria, perfilId))
        {
            if (EncaixaNaSemente(hoje, transacoes, orcamentos, metas, nomesCategoria, perfilId))
                return true;
        }

        return false;
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
        if (mes is < 1 or > 12 || orcamentos.Any(orcamento => orcamento.Mes != mes || orcamento.Ano != ano))
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
        var esperados = CatalogoDemonstracao.LancamentosSemeados(hoje);
        var usados = new bool[esperados.Count];
        foreach (var transacao in transacoes)
        {
            if (perfilId is null || !CabeNoLancamento(transacao, perfilId.Value, nomesCategoria, out var nomeCategoria))
                return false;

            var achou = false;
            for (var i = 0; i < esperados.Count; i++)
            {
                if (usados[i])
                    continue;

                var esperado = esperados[i];
                if (esperado.Descricao == transacao.Descricao
                    && esperado.Valor == transacao.Valor
                    && MesmaData(transacao.Data, esperado.Data)
                    && esperado.Tipo == transacao.Tipo
                    && esperado.Recorrencia == transacao.Recorrencia
                    && esperado.Categoria == nomeCategoria)
                {
                    usados[i] = true;
                    achou = true;
                    break;
                }
            }

            if (!achou)
                return false;
        }

        return true;
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
        var esperados = CatalogoDemonstracao.OrcamentosSemeados(hoje);
        var usados = new bool[esperados.Count];
        foreach (var orcamento in orcamentos)
        {
            if (orcamento.MetaReducaoPercentual is not null
                || !nomesCategoria.TryGetValue(orcamento.CategoriaId, out var nomeCategoria)
                || string.IsNullOrEmpty(nomeCategoria))
                return false;

            var achou = false;
            for (var i = 0; i < esperados.Count; i++)
            {
                if (usados[i])
                    continue;

                var esperado = esperados[i];
                if (esperado.Categoria == nomeCategoria
                    && esperado.ValorLimite == orcamento.ValorLimite
                    && esperado.Mes == orcamento.Mes
                    && esperado.Ano == orcamento.Ano)
                {
                    usados[i] = true;
                    achou = true;
                    break;
                }
            }

            if (!achou)
                return false;
        }

        return true;
    }

    private static bool EncaixaMetas(DateTime hoje, IReadOnlyList<Meta> metas, int? perfilId)
    {
        var esperados = CatalogoDemonstracao.MetasSemeadas(hoje);
        var usados = new bool[esperados.Count];
        foreach (var meta in metas)
        {
            if (perfilId is null
                || meta.MembroFamiliaId != perfilId
                || meta.Concluida
                || meta.Prazo is null)
                return false;

            var achou = false;
            for (var i = 0; i < esperados.Count; i++)
            {
                if (usados[i])
                    continue;

                var esperado = esperados[i];
                if (esperado.Nome == meta.Nome
                    && esperado.ValorAlvo == meta.ValorAlvo
                    && esperado.ValorAtual == meta.ValorAtual
                    && esperado.Icone == meta.Icone
                    && MesmaData(meta.Prazo.Value, esperado.Prazo))
                {
                    usados[i] = true;
                    achou = true;
                    break;
                }
            }

            if (!achou)
                return false;
        }

        return true;
    }

    private static bool MesmaData(DateTime gravada, DateTime esperada)
        => gravada.Year == esperada.Year
            && gravada.Month == esperada.Month
            && gravada.Day == esperada.Day
            && gravada.Hour == esperada.Hour
            && gravada.Minute == esperada.Minute
            && gravada.Second == esperada.Second;
}
