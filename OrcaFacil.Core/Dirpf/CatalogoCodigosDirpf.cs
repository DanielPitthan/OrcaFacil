using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Semente dos códigos da DIRPF 2026, vigente a partir de 2021 (grupos atuais de Bens e Direitos).
/// Ano anterior não tem linha: a exportação recusa em vez de reaproveitar o código novo.
/// Fiagro usa a linha de FII porque o cadastro não separa os dois.
/// ETF de mercado usa o código 09; o 08 é o de índice de renda fixa, e o tipo não distingue.
/// Rendimento isento de FII entra como 99 (Outros). Há guia antigo citando 26.
/// BDR e ETF não têm rendimento isento nesta semente.
/// </summary>
public static class CatalogoCodigosDirpf
{
    public static readonly DateTime Inicio = new(2021, 1, 1);

    public static IReadOnlyList<CodigoDirpf> Linhas() =>
    [
        Linha(1, TipoFichaDirpf.Bem, TipoAtivo.Acao, "03", "01", "Ações (inclusive as listadas em bolsa)"),
        Linha(2, TipoFichaDirpf.Bem, TipoAtivo.Fii, "07", "03", "Fundos de Investimento Imobiliário (FII)"),
        Linha(3, TipoFichaDirpf.Bem, TipoAtivo.Etf, "07", "09", "Demais fundos de índice de mercado (ETF)"),
        Linha(4, TipoFichaDirpf.Bem, TipoAtivo.Bdr, "04", "04", "Ativos negociados em bolsa no Brasil (BDRs, opções e outros, exceto ações e fundos)"),
        Linha(5, TipoFichaDirpf.RendimentoIsento, TipoAtivo.Acao, "", "09", "Lucros e dividendos recebidos"),
        Linha(6, TipoFichaDirpf.RendimentoIsento, TipoAtivo.Fii, "", "99", "Outros")
    ];

    private static CodigoDirpf Linha(
        int id,
        TipoFichaDirpf ficha,
        TipoAtivo tipo,
        string grupo,
        string codigo,
        string descricao) => new()
    {
        Id = id,
        Ficha = ficha,
        TipoAtivo = tipo,
        Grupo = grupo,
        Codigo = codigo,
        Descricao = descricao,
        VigenciaInicio = Inicio,
        VigenciaFim = null,
        EhSistema = true
    };
}
