using System.Globalization;
using Microsoft.EntityFrameworkCore;
using OrcaFacil.Core.Ativos;
using OrcaFacil.Core.Data;
using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Monta Bens e Direitos e Rendimentos Isentos de um ano-base.
/// A posição usa operações até 31/12, inclusive as de anos anteriores.
/// O rendimento isento usa o ano da data de pagamento. JCP fica de fora.
/// BDR e ETF não têm código de rendimento isento na semente e não entram nessa ficha.
/// O CNPJ sai vazio: o cadastro do ativo não guarda esse número.
/// Posição com quantidade zero fica de fora. Não há piso de valor.
/// </summary>
public static class MontadorDirpf
{
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

        return Montar(anoBase, operacoes, proventos, codigos, regras);
    }

    public static DirpfAnoBase Montar(
        int anoBase,
        IEnumerable<OperacaoAtivo> operacoes,
        IEnumerable<Provento> proventos,
        IEnumerable<CodigoDirpf> codigos,
        IEnumerable<RegraFiscal> regras)
    {
        ArgumentNullException.ThrowIfNull(operacoes);
        ArgumentNullException.ThrowIfNull(proventos);
        ArgumentNullException.ThrowIfNull(codigos);
        ArgumentNullException.ThrowIfNull(regras);
        ExigirAno(anoBase);

        var corte = new DateTime(anoBase, 12, 31);
        var inicio = new DateTime(anoBase, 1, 1);
        var fim = inicio.AddYears(1);
        var tabela = new TabelaCodigosDirpf(codigos);
        var fiscais = new TabelaRegrasFiscais(regras);

        var bens = MontarBens(anoBase, corte, operacoes, tabela, fiscais);
        var isentos = MontarIsentos(anoBase, corte, inicio, fim, proventos, tabela, fiscais);

        return new DirpfAnoBase
        {
            Ano = anoBase,
            Bens = bens,
            RendimentosIsentos = isentos
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

            var codigo = tabela.Exigir(TipoFichaDirpf.Bem, ativo.Tipo, corte);
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

            var natureza = Natureza(ativo.Tipo);
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

        return linhas
            .OrderBy(linha => linha.Perfil, OrdemTexto)
            .ThenBy(linha => linha.Ticker, StringComparer.Ordinal)
            .ToList();
    }

    private static string Natureza(TipoAtivo tipo) => tipo switch
    {
        TipoAtivo.Acao => "Dividendo",
        TipoAtivo.Fii => "Rendimento de FII",
        _ => throw new InvalidOperationException($"Não há natureza de rendimento isento para {tipo}.")
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
