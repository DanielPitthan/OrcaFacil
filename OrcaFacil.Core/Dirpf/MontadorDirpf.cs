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
/// e vai para a exclusiva, código da variante Jcp, também pelo ano do pagamento.
/// O ganho isento de ações no limite mensal entra na ficha de isentos, pelo código vigente.
/// BDR e ETF não têm código de rendimento isento na semente e não entram nessa ficha.
/// Renda fixa usa o IR retido gravado. A tabela regressiva não entra na conta.
/// Lucro de resgate já virou receita e prejuízo já virou despesa; aqui só se declara o líquido positivo.
/// O CNPJ de Bens e Direitos e de Rendimentos Isentos sai vazio: o campo no ativo é a #100.
/// Nas abas Rendimentos exclusivos e Renda fixa, CNPJ vazio da fonte pagadora vira aviso.
/// A exportação segue. A #100 não cobre essas duas abas nem o título de renda fixa.
/// Posição com quantidade zero fica de fora. Não há piso de valor.
/// Título do tipo Outro continua no grupo 04/02 e no código 06. A linha só ganha o aviso de LCI/LCA.
/// LCI e LCA vão para Rendimentos Isentos, código da variante RendaFixaIsenta, e para o bem 04/03.
/// Não levam o aviso do tipo Outro.
/// CRI, CRA e LIG não têm tipo no enum. O primeiro segmento do código ou do nome,
/// separado por " - ", ganha a observação de isenção da pessoa física.
/// O código e o grupo dessa frase saem da variante RendaFixaIsenta vigente na data de corte.
/// Debênture, qualquer que seja, fica na linha tributada. O primeiro segmento só reconhece
/// que é debênture. A frase é neutra: incentivada da Lei nº 12.431/2011 pode ser isenta;
/// a de infraestrutura da Lei nº 14.801/2024 é tributada.
/// O perfil do título usa os lançamentos das operações do papel, inclusive as de anos anteriores.
/// Dois donos em anos diferentes ficam como não identificado.
/// </summary>
public static class MontadorDirpf
{
    public const string PerfilNaoIdentificado = "Não identificado";

    public const string ObservacaoTituloOutro =
        "Se for LCI/LCA, é isento: declarar em Rendimentos Isentos (código 12) e no grupo 04/03";

    /// <summary>
    /// Texto da coluna CNPJ quando a fonte pagadora não tem número.
    /// Não é um CNPJ: começa por "Aviso" para não ser copiado no campo da declaração.
    /// </summary>
    public const string AvisoCnpjFontePagadora = "Aviso: CNPJ da fonte pagadora não informado";

    /// <summary>
    /// O nome do produto não separa incentivada, infraestrutura e comum.
    /// Nenhuma debênture sai como isenta. A frase só avisa os dois regimes.
    /// </summary>
    public const string ObservacaoDebenture =
        "Se for debênture incentivada (Lei 12.431/2011, arts. 2º e 3º; P&R IRPF 738), é isenta para pessoa física e deve ser declarada em Rendimentos Isentos; se for debênture de infraestrutura da Lei 14.801/2024, é tributada pela tabela regressiva.";

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
        // Ano-base = ano do pagamento (juros recebidos). A pergunta 320 das Perguntas e
        // Respostas IRPF 2026 tributa "na data do pagamento ou crédito"; as instruções
        // da DIRPF mandam informar os juros recebidos. O crédito ainda não pago é o bem
        // "Juros Sobre Capital Próprio a receber", e o provento não guarda essa data.
        // https://www.gov.br/receitafederal/pt-br/centrais-de-conteudo/publicacoes/perguntas-e-respostas/dirpf/p-r-irpf-2026-v1-00-2026-04-23.pdf
        // https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/preenchimento/manual-mir/rendimentos/rendimentos-do-capital
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
                CnpjDaFontePagadora(""),
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
        string? observacaoIsencao = null;

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
            // O lançamento de um resgate anterior ao ano-base também identifica o dono.
            // doTitulo não corta em 1º de janeiro: o título comprado antes continua classificado.
            // Dois donos, ainda que em anos diferentes, não escolhem um: fica "Não identificado".
            var perfil = PerfilDoTitulo(doTitulo, listaLancamentos, listaPerfis);
            string? observacaoProduto = null;
            if (EhProdutoIsentoPessoaFisica(titulo))
                observacaoProduto = observacaoIsencao ??= ObservacaoIsencaoPessoaFisica(tabela, corte);
            else if (EhDebenture(titulo))
                observacaoProduto = ObservacaoDebenture;

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
                    CnpjDaFontePagadora(""),
                    calculada.Quantidade,
                    custo,
                    medio,
                    irDeclarado,
                    liquidoDeclarado,
                    TextoComObservacao(
                        $"{titulo.Codigo} — {titulo.Nome}. {tipo}. Custo de aquisição em 31/12/{anoBase}.",
                        titulo.Tipo,
                        observacaoProduto)));
            }

            if (liquidoDeclarado > 0m)
            {
                var descricao = TextoComObservacao(
                    $"Rendimento líquido de renda fixa — {titulo.Codigo} — {titulo.Nome}.",
                    titulo.Tipo,
                    observacaoProduto);
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
                        CnpjDaFontePagadora(""),
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

    /// <summary>
    /// CNPJ da fonte pagadora nas abas Rendimentos exclusivos e Renda fixa.
    /// Vazio vira aviso e a linha continua. Número informado segue como está.
    /// </summary>
    public static string CnpjDaFontePagadora(string? cnpj)
        => string.IsNullOrWhiteSpace(cnpj) ? AvisoCnpjFontePagadora : cnpj.Trim();

    private static bool EhTituloIsento(TipoTituloRendaFixa tipo)
        => tipo is TipoTituloRendaFixa.LCI or TipoTituloRendaFixa.LCA;

    /// <summary>
    /// Primeiro segmento do produto, no corte " - ", como o extrato classifica LCI e LCA.
    /// CRI, CRA e LIG são isentos para a pessoa física
    /// (Perguntas e Respostas IRPF 2026, pergunta 738, itens II, III e VI).
    /// O número do código e o grupo saem da variante vigente.
    /// </summary>
    private static readonly string[] PrefixosIsentosPessoaFisica = ["CRI", "CRA", "LIG"];

    /// <summary>
    /// A palavra inteira, com ou sem acento, no singular ou no plural.
    /// "DEBENTURE INCENTIVADA" e "DEBÊNTURE DE INFRAESTRUTURA" também entram:
    /// o restante do segmento não escolhe o regime.
    /// </summary>
    private static readonly string[] PalavrasDebenture =
    [
        "DEBENTURE",
        "DEBÊNTURE",
        "DEBENTURES",
        "DEBÊNTURES"
    ];

    private static bool EhProdutoIsentoPessoaFisica(TituloRendaFixa titulo)
    {
        if (EhTituloIsento(titulo.Tipo))
            return false;

        return EhPrefixoIsentoPessoaFisica(titulo.Codigo) || EhPrefixoIsentoPessoaFisica(titulo.Nome);
    }

    private static bool EhDebenture(TituloRendaFixa titulo)
    {
        if (EhTituloIsento(titulo.Tipo))
            return false;

        return EhPrefixoDebenture(titulo.Codigo) || EhPrefixoDebenture(titulo.Nome);
    }

    private static bool EhPrefixoIsentoPessoaFisica(string? produto)
    {
        var primeiro = PrimeiroSegmento(produto);
        if (primeiro is null)
            return false;

        foreach (var prefixo in PrefixosIsentosPessoaFisica)
        {
            if (primeiro.Equals(prefixo, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool EhPrefixoDebenture(string? produto)
    {
        var primeiro = PrimeiroSegmento(produto);
        if (primeiro is null)
            return false;

        foreach (var palavra in PalavrasDebenture)
        {
            if (primeiro.Equals(palavra, StringComparison.OrdinalIgnoreCase))
                return true;

            if (primeiro.StartsWith(palavra + " ", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string? PrimeiroSegmento(string? produto)
    {
        if (string.IsNullOrWhiteSpace(produto))
            return null;

        var partes = produto.Split(" - ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return partes.Length >= 1 ? partes[0] : null;
    }

    private static string ObservacaoIsencaoPessoaFisica(TabelaCodigosDirpf tabela, DateTime corte)
    {
        var rendimento = tabela.ExigirVariante(
            TipoFichaDirpf.RendimentoIsento, CatalogoCodigosDirpf.VarianteRendaFixaIsenta, corte);
        var bem = tabela.ExigirVariante(
            TipoFichaDirpf.Bem, CatalogoCodigosDirpf.VarianteRendaFixaIsenta, corte);
        return $"Isento para pessoa física: declarar em Rendimentos Isentos (código {rendimento.Codigo}) e em Bens e Direitos no grupo {bem.Grupo}/{bem.Codigo}";
    }

    private static string TextoComObservacao(string texto, TipoTituloRendaFixa tipo, string? observacao)
    {
        if (observacao is not null)
            return $"{texto} {observacao}";

        return TextoOutro(texto, tipo);
    }

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
