using OrcaFacil.Core.Entities;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Dirpf;

/// <summary>
/// Semente dos códigos da DIRPF 2026, vigente a partir de 2021 (grupos atuais de Bens e Direitos).
/// Ano anterior não tem linha: a exportação recusa em vez de reaproveitar o código novo.
/// Fiagro continua FII no cadastro. Na exportação de bens, a variante Fiagro usa o código 07/02.
/// ETF usa 07/06. Fundo de índice de renda fixa é 07/08 na mesma página da Receita,
/// mas o cadastro tem um único tipo ETF, então a ficha não separa o 08.
/// Fonte: Bens Financeiros, Receita Federal, atualizado em 26/05/2026.
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/preenchimento/manual-mir/patrimonio/bens-financeiros
/// Rendimento isento de FII e Fiagro permanece 99, configurável.
/// O 26 aparece em matéria do Seu Dinheiro, não em fonte da Receita.
/// BDR e ETF não têm rendimento isento nesta semente.
/// JCP é tributação exclusiva, código 10, pela variante Jcp. O tipo do ativo na linha
/// só preenche a coluna obrigatória: a ficha procura a variante, não o tipo.
/// Renda fixa tributada: bem grupo 04 código 02 e rendimento exclusivo código 06,
/// na variante RendaFixa. O tipo Outro usa a mesma variante: o cadastro não separa LCI/LCA.
/// O manual da Receita publica o nome do bem e do rendimento, não o número.
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/preenchimento/manual-mir/patrimonio/bens-financeiros
/// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/preenchimento/manual-mir/rendimentos/rendimentos-do-capital
/// </summary>
public static class CatalogoCodigosDirpf
{
    public const string VarianteFiagro = "Fiagro";

    /// <summary>Juros sobre capital próprio, ficha de tributação exclusiva.</summary>
    public const string VarianteJcp = "Jcp";

    /// <summary>CDB, Tesouro e o tipo Outro. A mesma variante vale para o bem e para o rendimento.</summary>
    public const string VarianteRendaFixa = "RendaFixa";

    /// <summary>
    /// Ganho isento do art. 3º, I, da Lei nº 11.033/2004. O texto é o do manual
    /// do Meu Imposto de Renda; o número 20 é o da ficha e continua configurável.
    /// https://www.gov.br/receitafederal/pt-br/assuntos/meu-imposto-de-renda/preenchimento/manual-mir/rendimentos/rendimentos-do-capital
    /// </summary>
    public const string VarianteGanhoIsentoAcoes = "GanhoIsento";

    public const string DescricaoGanhoIsentoAcoes =
        "Ganhos líquidos em operações no mercado à vista de ações negociadas em bolsas de valores nas alienações realizadas até R$ 20.000,00 em cada mês, para o conjunto de ações";

    public const string DescricaoJcp = "Juros sobre capital próprio";

    public const string DescricaoRendimentoRendaFixa = "Rendimentos de aplicações financeiras";

    public const string DescricaoBemRendaFixa = "Títulos públicos e privados sujeitos à tributação";

    public static readonly DateTime Inicio = new(2021, 1, 1);

    public static IReadOnlyList<CodigoDirpf> Linhas() =>
    [
        Linha(1, TipoFichaDirpf.Bem, TipoAtivo.Acao, "03", "01", "Ações (inclusive as listadas em bolsa)"),
        Linha(2, TipoFichaDirpf.Bem, TipoAtivo.Fii, "07", "03", "Fundos de Investimento Imobiliário (FII)"),
        Linha(3, TipoFichaDirpf.Bem, TipoAtivo.Etf, "07", "06", "ETF - Entidade de investimento"),
        Linha(4, TipoFichaDirpf.Bem, TipoAtivo.Bdr, "04", "04", "Ativos negociados em bolsa no Brasil (BDRs, opções e outros, exceto ações e fundos)"),
        Linha(5, TipoFichaDirpf.RendimentoIsento, TipoAtivo.Acao, "", "09", "Lucros e dividendos recebidos"),
        Linha(6, TipoFichaDirpf.RendimentoIsento, TipoAtivo.Fii, "", "99", "Outros"),
        Linha(7, TipoFichaDirpf.Bem, TipoAtivo.Fii, "07", "02", "Fundos de Investimento nas Cadeias Produtivas Agroindustriais (Fiagro)", VarianteFiagro),
        Linha(8, TipoFichaDirpf.RendimentoIsento, TipoAtivo.Acao, "", "20", DescricaoGanhoIsentoAcoes, VarianteGanhoIsentoAcoes),
        Linha(9, TipoFichaDirpf.RendimentoExclusivo, TipoAtivo.Acao, "", "10", DescricaoJcp, VarianteJcp),
        Linha(10, TipoFichaDirpf.RendimentoExclusivo, TipoAtivo.Acao, "", "06", DescricaoRendimentoRendaFixa, VarianteRendaFixa),
        Linha(11, TipoFichaDirpf.Bem, TipoAtivo.Acao, "04", "02", DescricaoBemRendaFixa, VarianteRendaFixa)
    ];

    private static CodigoDirpf Linha(
        int id,
        TipoFichaDirpf ficha,
        TipoAtivo tipo,
        string grupo,
        string codigo,
        string descricao,
        string variante = "") => new()
    {
        Id = id,
        Ficha = ficha,
        TipoAtivo = tipo,
        Variante = variante,
        Grupo = grupo,
        Codigo = codigo,
        Descricao = descricao,
        VigenciaInicio = Inicio,
        VigenciaFim = null,
        EhSistema = true
    };
}
