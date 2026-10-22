using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;
using OrcaFacil.Core.RendaFixa;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Monta Bens e Direitos, Rendimentos Isentos, a tributação exclusiva e a posição de renda fixa.
/// A posição usa operações até 31/12, inclusive as de anos anteriores.
/// O rendimento isento usa o ano da data de pagamento. JCP fica de fora dessa ficha
/// e vai para a exclusiva, código da variante Jcp, pelo mesmo ano.
/// O ganho isento de ações no limite mensal entra na ficha de isentos, pelo código vigente.
/// BDR e ETF não têm código de rendimento isento na semente e não entram nessa ficha.
/// Renda fixa usa o IR retido gravado. A tabela regressiva não entra na conta.
/// Lucro de resgate já virou receita e prejuízo já virou despesa; aqui só se declara o líquido positivo.
/// O CNPJ sai vazio: o cadastro não guarda esse número.
/// Posição com quantidade zero fica de fora. Não há piso de valor.
/// Título do tipo Outro continua no grupo 04/02 e no código 06. A linha só ganha o aviso de LCI/LCA.
/// LCI e LCA vão para Rendimentos Isentos, código da variante RendaFixaIsenta, e para o bem 04/03.
/// Não levam o aviso do tipo Outro.
/// </summary>
public static class MontadorDirpf
{
    public const string PerfilNaoIdentificado = "Não identificado";

    public const string ObservacaoTituloOutro =
        "Se for LCI/LCA, é isento: declarar em Rendimentos Isentos (código 12) e no grupo 04/03";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private static readonly StringComparer OrdemTexto = StringComparer.Create(PtBr, ignoreCase: true);

    public static DirpfAnoBase Montar(AppDbContext contexto, int anoBase)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ExigirAno(anoBase);

        var operacoes = contexto.OperacoesAtivo.AsNoTracking()
            .Include(operacao => operacao.Ativo)
            .Include(operacao => operacao.MembroFamilia)
            .ToList();
        var proventos = contexto.Proventos.AsNoTracking()
            .Include(provento => provento.Ativo)
            .Include(provento => provento.MembroFamilia)
            .ToList();
        var codigos = contexto.CodigosDirpf.AsNoTracking().ToList();
        var regras = contexto.RegrasFiscais.AsNoTracking().ToList();
        var titulos = contexto.TitulosRendaFixa.AsNoTracking().ToList();
        var operacoesRendaFixa = contexto.OperacoesRendaFixa.AsNoTracking().ToList();
        var lancamentosRendaFixa = contexto.Transacoes.AsNoTracking()
            .Include(transacao => transacao.MembroFamilia)
            .Where(transacao => transacao.OperacaoRendaFixaId != null)
            .ToList();
        var perfis = contexto.MembrosFamilia.AsNoTracking().ToList();

        return Montar(
            anoBase,
            operacoes,
            proventos,
            codigos,
            regras,
            titulos,
            operacoesRendaFixa,
            lancamentosRendaFixa,
            perfis);
    }

    public static DirpfAnoBase Montar(
        int anoBase,
        IEnumerable<OperacaoAtivo> operacoes,
        IEnumerable<Provento> proventos,
        IEnumerable<CodigoDirpf> codigos,
        IEnumerable<RegraFiscal> regras)
        => Montar(anoBase, operacoes, proventos, codigos, regras, [], [], [], []);

    public static DirpfAnoBase Montar(
        int anoBase,
        IEnumerable<OperacaoAtivo> operacoes,
        IEnumerable<Provento> proventos,
        IEnumerable<CodigoDirpf> codigos,
        IEnumerable<RegraFiscal> regras,
        IEnumerable<TituloRendaFixa> titulos,
        IEnumerable<OperacaoRendaFixa> operacoesRendaFixa,
        IEnumerable<Transacao> lancamentosRendaFixa,
        IEnumerable<MembroFamilia> perfis)
    {
        ArgumentNullException.ThrowIfNull(operacoes);
        ArgumentNullException.ThrowIfNull(proventos);
        ArgumentNullException.ThrowIfNull(codigos);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(titulos);
        ArgumentNullException.ThrowIfNull(operacoesRendaFixa);
        ArgumentNullException.ThrowIfNull(lancamentosRendaFixa);
        ArgumentNullException.ThrowIfNull(perfis);
        ExigirAno(anoBase);

        var listaOperacoes = operacoes.ToList();
        var listaProventos = proventos.ToList();
        var corte = new DateTime(anoBase, 12, 31);
        var inicio = new DateTime(anoBase, 1, 1);
        var fim = inicio.AddYears(1);
        var tabela = new TabelaCodigosDirpf(codigos);
        var fiscais = new TabelaRegrasFiscais(regras);

        var bens = MontarBens(anoBase, corte, listaOperacoes, tabela, fiscais);
        var isentos = MontarIsentos(anoBase, corte, inicio, fim, listaProventos, tabela, fiscais);
        isentos.AddRange(MontarGanhoIsento(anoBase, corte, listaOperacoes, regras, tabela, fiscais));

        var exclusivos = MontarJcp(anoBase, corte, inicio, fim, listaProventos, tabela, fiscais);
        var rendaFixa = MontarRendaFixa(
            anoBase,
            corte,
            inicio,
            fim,
            titulos,
            operacoesRendaFixa,
            lancamentosRendaFixa,
            perfis,
            tabela,
            fiscais,
            exclusivos,
            isentos);

        return new DirpfAnoBase
        {
            Ano = anoBase,
            Bens = bens,
            RendimentosIsentos = isentos
                .OrderBy(linha => linha.Perfil, OrdemTexto)
                .ThenBy(linha => linha.Ticker, StringComparer.Ordinal)
                .ToList(),
            RendimentosExclusivos = exclusivos
                .OrderBy(linha => linha.Perfil, OrdemTexto)
                .ThenBy(linha => linha.CodigoAtivo, StringComparer.Ordinal)
                .ThenBy(linha => linha.Natureza, OrdemTexto)
                .ToList(),
            RendaFixa = rendaFixa
        };
    }

    public static void ExigirAno(int anoBase)
    {
        if (anoBase is < 1900 or > 2200)
            throw new ArgumentOutOfRangeException(nameof(anoBase), "O ano-base deve estar entre 1900 e 2200.");
    }

    private static List<LinhaBemDirpf> MontarBens(
        int anoBase,
        DateTime corte,
        IEnumerable<OperacaoAtivo> operacoes,
        TabelaCodigosDirpf tabela,
        TabelaRegrasFiscais fiscais)
    {
        var linhas = new List<LinhaBemDirpf>();
        foreach (var grupo in operacoes.GroupBy(operacao => (operacao.MembroFamiliaId, operacao.AtivoId)))
        {
            var amostra = grupo.First();
            var ativo = ExigirAtivo(amostra.Ativo, amostra.AtivoId);
            var perfil = ExigirPerfil(amostra.MembroFamilia, amostra.MembroFamiliaId);
            var posicao = CalculadoraPosicaoDirpf.Ate(grupo.Select(ParaMovimento), corte);
            if (posicao.Quantidade <= 0)
                continue;

            var variante = ReconhecedorFiagroDirpf.Eh(ativo) ? CatalogoCodigosDirpf.VarianteFiagro : "";
            var codigo = tabela.Exigir(TipoFichaDirpf.Bem, ativo.Tipo, corte, variante);
            var casas = fiscais.Para(corte, ativo.Tipo).CasasDecimais;
            var custo = CalculadoraResultadoRealizado.Arredondar(posicao.CustoTotal, casas);
            var medio = CalculadoraResultadoRealizado.Arredondar(posicao.PrecoMedio, casas);
            var tipo = ativo.Tipo.Rotulo();
            linhas.Add(new LinhaBemDirpf(
                anoBase,
                TextoPerfil(perfil),
                ativo.Ticker,
                ativo.Nome,
                tipo,
                codigo.Grupo,
                codigo.Codigo,
                codigo.Descricao,
                Cnpj: "",
                posicao.Quantidade,
                custo,
                medio,
                $"{ativo.Ticker} — {ativo.Nome}. Quantidade {Texto(posicao.Quantidade)} {tipo}. Custo de aquisição em 31/12/{anoBase}."));
        }

        return linhas
            .OrderBy(linha => linha.Perfil, OrdemTexto)
            .ThenBy(linha => linha.Ticker, StringComparer.Ordinal)
            .ToList();
    }

    private static List<LinhaRendimentoIsentoDirpf> MontarIsentos(
        int anoBase,
        DateTime corte,
        DateTime inicio,
        DateTime fim,
        IEnumerable<Provento> proventos,
        TabelaCodigosDirpf tabela,
        TabelaRegrasFiscais fiscais)
    {
        var doAno = proventos.Where(provento =>
            provento.Tipo == TipoProvento.Rendimento
            && provento.DataPagamento >= inicio
            && provento.DataPagamento < fim);

        var linhas = new List<LinhaRendimentoIsentoDirpf>();
        foreach (var grupo in doAno.GroupBy(provento => (provento.MembroFamiliaId, provento.AtivoId)))
        {
            var amostra = grupo.First();
            var ativo = ExigirAtivo(amostra.Ativo, amostra.AtivoId);
            var codigo = tabela.Vigente(TipoFichaDirpf.RendimentoIsento, ativo.Tipo, corte);
            if (codigo is null)
                continue;

            var perfil = ExigirPerfil(amostra.MembroFamilia, amostra.MembroFamiliaId);
            var casas = fiscais.Para(corte, ativo.Tipo).CasasDecimais;
            var valor = CalculadoraResultadoRealizado.Arredondar(grupo.Sum(provento => provento.ValorLiquido), casas);
            if (valor == 0)
                continue;

            var natureza = Natureza(ativo);
            linhas.Add(new LinhaRendimentoIsentoDirpf(
                anoBase,
                TextoPerfil(perfil),
                ativo.Ticker,
                ativo.Nome,
                natureza,
                codigo.Codigo,
                codigo.Descricao,
                Cnpj: "",
                valor,
                $"{natureza} — {ativo.Ticker} — {ativo.Nome}."));
        }

        return linhas;
    }

    private static List<LinhaRendimentoIsentoDirpf> MontarGanhoIsento(
        int anoBase,
        DateTime corte,
        IEnumerable<OperacaoAtivo> operacoes,
        IEnumerable<RegraFiscal> regras,
        TabelaCodigosDirpf tabela,
        TabelaRegrasFiscais fiscais)
    {
        var apuracao = ApuradorIrMensal.Apurar(operacoes.Select(ApuradorIrMensal.De), regras, anoBase);
        if (apuracao.GanhosIsentos.Count == 0)
            return [];

        var codigo = tabela.Exigir(
            TipoFichaDirpf.RendimentoIsento,
            TipoAtivo.Acao,
            corte,
            CatalogoCodigosDirpf.VarianteGanhoIsentoAcoes);
        var casas = fiscais.Para(corte, TipoAtivo.Acao).CasasDecimais;
        var linhas = new List<LinhaRendimentoIsentoDirpf>();
        foreach (var ganho in apuracao.GanhosIsentos)
        {
            var valor = CalculadoraResultadoRealizado.Arredondar(ganho.Valor, casas);
            if (valor == 0m)
                continue;

            linhas.Add(new LinhaRendimentoIsentoDirpf(
                anoBase,
                ganho.Perfil,
                "",
                "Conjunto de ações",
                "Ganho líquido isento",
                codigo.Codigo,
                codigo.Descricao,
                Cnpj: "",
                valor,
                "Ganhos líquidos isentos no mercado à vista de ações, dentro do limite mensal de alienação."));
        }

        return linhas;
    }

    private static string Natureza(AtivoFinanceiro ativo)
    {
        if (ReconhecedorFiagroDirpf.Eh(ativo))
            return "Rendimento de Fiagro";

        return ativo.Tipo switch
        {
            TipoAtivo.Acao => "Dividendo",
            TipoAtivo.Fii => "Rendimento de FII",
            _ => throw new InvalidOperationException($"Não há natureza de rendimento isento para {ativo.Tipo}.")
        };
    }

    private static List<LinhaRendimentoExclusivoDirpf> MontarJcp(
        int anoBase,
        DateTime corte,
        DateTime inicio,
        DateTime fim,
        IEnumerable<Provento> proventos,
        TabelaCodigosDirpf tabela,
        TabelaRegrasFiscais fiscais)
    {
        var doAno = proventos.Where(provento =>
            provento.Tipo == TipoProvento.Jcp
            && provento.DataPagamento >= inicio
            && provento.DataPagamento < fim).ToList();
        if (doAno.Count == 0)
            return [];

        ExigirJcpSemGrossUp(fiscais, corte);
        var codigo = tabela.ExigirVariante(TipoFichaDirpf.RendimentoExclusivo, CatalogoCodigosDirpf.VarianteJcp, corte);
        var casas = fiscais.Para(corte, TipoAtivo.Acao).CasasDecimais;
        var linhas = new List<LinhaRendimentoExclusivoDirpf>();
        foreach (var grupo in doAno.GroupBy(provento => (provento.MembroFamiliaId, provento.AtivoId)))
        {
            var amostra = grupo.First();
            var ativo = ExigirAtivo(amostra.Ativo, amostra.AtivoId);
            var perfil = ExigirPerfil(amostra.MembroFamilia, amostra.MembroFamiliaId);
            var valor = CalculadoraResultadoRealizado.Arredondar(grupo.Sum(provento => provento.ValorLiquido), casas);
            if (valor == 0m)
                continue;

            linhas.Add(new LinhaRendimentoExclusivoDirpf(
                anoBase,
                TextoPerfil(perfil),
                ativo.Ticker,
                ativo.Nome,
                "JCP",
                codigo.Codigo,
                codigo.Descricao,
                Cnpj: "",
                valor,
                IrRetido: null,
                $"JCP — {ativo.Ticker} — {ativo.Nome}."));
        }

        return linhas;
    }

    private static List<LinhaRendaFixaDirpf> MontarRendaFixa(
        int anoBase,
        DateTime corte,
        DateTime inicio,
        DateTime fim,
        IEnumerable<TituloRendaFixa> titulos,
        IEnumerable<OperacaoRendaFixa> operacoes,
        IEnumerable<Transacao> lancamentos,
        IEnumerable<MembroFamilia> perfis,
        TabelaCodigosDirpf tabela,
        TabelaRegrasFiscais fiscais,
        List<LinhaRendimentoExclusivoDirpf> exclusivos,
        List<LinhaRendimentoIsentoDirpf> isentos)
    {
        var listaTitulos = titulos.ToList();
        var listaOperacoes = operacoes.ToList();
        var listaLancamentos = lancamentos.ToList();
        var listaPerfis = perfis.ToList();
        var posicoes = new List<LinhaRendaFixaDirpf>();
        CodigoDirpf? codigoBem = null;
        CodigoDirpf? codigoRendimento = null;
        CodigoDirpf? codigoBemIsento = null;
        CodigoDirpf? codigoRendimentoIsento = null;
        var casas = 2;
        var regraLida = false;

        foreach (var titulo in listaTitulos)
        {
            var doTitulo = listaOperacoes.Where(operacao => operacao.TituloRendaFixaId == titulo.Id).ToList();
            var ateCorte = doTitulo.Where(operacao => operacao.Data.Date <= corte).ToList();
            if (ateCorte.Count == 0)
                continue;

            var calculada = CalculadoraRendaFixa.Calcular(ateCorte.Select(operacao => ParaMovimentoRendaFixa(operacao, titulo.Codigo)));
            var doAno = calculada.Resgates.Where(resgate => resgate.Data >= inicio && resgate.Data < fim).ToList();
            var idsDoAno = doAno.Select(resgate => resgate.Id).ToHashSet();
            var ir = ateCorte.Where(operacao => idsDoAno.Contains(operacao.Id)).Sum(operacao => operacao.IrRetido);
            var liquido = doAno.Sum(resgate => resgate.Resultado);
            if (calculada.Quantidade <= 0m && liquido <= 0m)
                continue;

            if (!regraLida)
            {
                ExigirRendaFixaSemRegressivo(fiscais, corte);
                casas = fiscais.Para(corte, TipoAtivo.Acao).CasasDecimais;
                regraLida = true;
            }

            var isento = EhTituloIsento(titulo.Tipo);
            if (isento)
            {
                codigoBemIsento ??= tabela.ExigirVariante(
                    TipoFichaDirpf.Bem, CatalogoCodigosDirpf.VarianteRendaFixaIsenta, corte);
                codigoRendimentoIsento ??= tabela.ExigirVariante(
                    TipoFichaDirpf.RendimentoIsento, CatalogoCodigosDirpf.VarianteRendaFixaIsenta, corte);
            }
            else
            {
                codigoBem ??= tabela.ExigirVariante(
                    TipoFichaDirpf.Bem, CatalogoCodigosDirpf.VarianteRendaFixa, corte);
                codigoRendimento ??= tabela.ExigirVariante(
                    TipoFichaDirpf.RendimentoExclusivo, CatalogoCodigosDirpf.VarianteRendaFixa, corte);
            }

            var codigoDoBem = isento ? codigoBemIsento! : codigoBem!;
            var liquidoDeclarado = CalculadoraResultadoRealizado.Arredondar(liquido, casas);
            var irDeclarado = CalculadoraResultadoRealizado.Arredondar(ir, casas);
            var perfil = PerfilDoTitulo(doTitulo, listaLancamentos, listaPerfis);
            if (calculada.Quantidade > 0m)
            {
                var custo = CalculadoraResultadoRealizado.Arredondar(calculada.CustoTotal, casas);
                var medio = CalculadoraResultadoRealizado.Arredondar(calculada.CustoMedio, casas);
                var tipo = RotuloTitulo(titulo.Tipo);
                posicoes.Add(new LinhaRendaFixaDirpf(
                    anoBase,
                    perfil,
                    titulo.Codigo,
                    titulo.Nome,
                    tipo,
                    titulo.Corretora,
                    codigoDoBem.Grupo,
                    codigoDoBem.Codigo,
                    codigoDoBem.Descricao,
                    Cnpj: "",
                    calculada.Quantidade,
                    custo,
                    medio,
                    irDeclarado,
                    liquidoDeclarado,
                    TextoOutro(
                        $"{titulo.Codigo} — {titulo.Nome}. {tipo}. Custo de aquisição em 31/12/{anoBase}.",
                        titulo.Tipo)));
            }

            if (liquidoDeclarado > 0m)
            {
                var descricao = TextoOutro(
                    $"Rendimento líquido de renda fixa — {titulo.Codigo} — {titulo.Nome}.",
                    titulo.Tipo);
                if (isento)
                {
                    isentos.Add(new LinhaRendimentoIsentoDirpf(
                        anoBase,
                        perfil,
                        titulo.Codigo,
                        titulo.Nome,
                        "Rendimento de renda fixa",
                        codigoRendimentoIsento!.Codigo,
                        codigoRendimentoIsento.Descricao,
                        Cnpj: "",
                        liquidoDeclarado,
                        descricao));
                }
                else
                {
                    exclusivos.Add(new LinhaRendimentoExclusivoDirpf(
                        anoBase,
                        perfil,
                        titulo.Codigo,
                        titulo.Nome,
                        "Rendimento de renda fixa",
                        codigoRendimento!.Codigo,
                        codigoRendimento.Descricao,
                        Cnpj: "",
                        liquidoDeclarado,
                        irDeclarado,
                        descricao));
                }
            }
        }

        return posicoes
            .OrderBy(linha => linha.Perfil, OrdemTexto)
            .ThenBy(linha => linha.Codigo, StringComparer.Ordinal)
            .ToList();
    }

    private static void ExigirJcpSemGrossUp(TabelaRegrasFiscais fiscais, DateTime corte)
    {
        if (fiscais.EstaLigada(CodigosRegraFiscal.JcpGrossUp, corte))
            throw new InvalidOperationException(
                "A regra RF.Jcp.GrossUp está ligada. O JCP da DIRPF usa o valor líquido gravado e não reconstitui o bruto.");
    }

    private static void ExigirRendaFixaSemRegressivo(TabelaRegrasFiscais fiscais, DateTime corte)
    {
        if (fiscais.EstaLigada(CodigosRegraFiscal.IrRegressivoAutomatico, corte))
            throw new InvalidOperationException(
                "A regra RF.IrRegressivo.Automatico está ligada. A DIRPF da renda fixa usa só o IR retido gravado.");
    }

    private static string PerfilDoTitulo(
        IReadOnlyList<OperacaoRendaFixa> operacoes,
        IReadOnlyList<Transacao> lancamentos,
        IReadOnlyList<MembroFamilia> perfis)
    {
        var ids = operacoes.Select(operacao => operacao.Id).ToHashSet();
        var donos = lancamentos
            .Where(lancamento => lancamento.OperacaoRendaFixaId is int id && ids.Contains(id))
            .Select(lancamento => lancamento.MembroFamiliaId)
            .Distinct()
            .ToList();

        if (donos.Count == 1)
        {
            var id = donos[0];
            var perfil = perfis.FirstOrDefault(membro => membro.Id == id)
                ?? lancamentos.First(lancamento => lancamento.MembroFamiliaId == id).MembroFamilia;
            return TextoPerfil(ExigirPerfil(perfil, id));
        }

        if (donos.Count == 0 && perfis.Count == 1)
            return TextoPerfil(perfis[0]);

        return PerfilNaoIdentificado;
    }

    private static MovimentoRendaFixa ParaMovimentoRendaFixa(OperacaoRendaFixa operacao, string codigo) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.Tipo,
        Quantidade = operacao.Quantidade,
        Valor = operacao.Valor,
        Custos = operacao.Custos,
        IrRetido = operacao.IrRetido,
        Codigo = codigo
    };

    private static bool EhTituloIsento(TipoTituloRendaFixa tipo)
        => tipo is TipoTituloRendaFixa.LCI or TipoTituloRendaFixa.LCA;

    private static string TextoOutro(string texto, TipoTituloRendaFixa tipo) =>
        tipo == TipoTituloRendaFixa.Outro ? $"{texto} {ObservacaoTituloOutro}" : texto;

    private static string RotuloTitulo(TipoTituloRendaFixa tipo) => tipo switch
    {
        TipoTituloRendaFixa.CDB => "CDB",
        TipoTituloRendaFixa.TesouroPrefixado => "Tesouro Prefixado",
        TipoTituloRendaFixa.TesouroIPCA => "Tesouro IPCA",
        TipoTituloRendaFixa.TesouroSelic => "Tesouro Selic",
        TipoTituloRendaFixa.Outro => "Outro",
        TipoTituloRendaFixa.LCI => "LCI",
        TipoTituloRendaFixa.LCA => "LCA",
        _ => tipo.ToString()
    };

    private static MovimentoPosicao ParaMovimento(OperacaoAtivo operacao) => new()
    {
        Id = operacao.Id,
        Data = operacao.Data,
        Tipo = operacao.Tipo,
        Quantidade = operacao.Quantidade,
        PrecoUnitario = operacao.PrecoUnitario,
        ValorOperacao = operacao.ValorOperacao,
        Custos = operacao.Custos
    };

    private static AtivoFinanceiro ExigirAtivo(AtivoFinanceiro? ativo, int id)
    {
        if (ativo is null)
            throw new InvalidOperationException($"A DIRPF precisa do ativo {id}.");

        return ativo;
    }

    private static MembroFamilia ExigirPerfil(MembroFamilia? perfil, int id)
    {
        if (perfil is null)
            throw new InvalidOperationException($"A DIRPF precisa do perfil {id}.");

        return perfil;
    }

    private static string TextoPerfil(MembroFamilia membro)
    {
        if (string.IsNullOrWhiteSpace(membro.Apelido))
            return membro.Nome;

        return $"{membro.Nome} ({membro.Apelido})";
    }

    private static string Texto(decimal quantidade)
        => quantidade.ToString("0.########", PtBr);
}
