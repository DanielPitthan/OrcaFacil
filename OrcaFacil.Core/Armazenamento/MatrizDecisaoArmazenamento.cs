namespace OrcaFacil.Core.Armazenamento;

/// <summary>Uma célula da matriz. <see cref="MatrizDecisaoArmazenamento.Decidir"/> só procura nesta lista.</summary>
public sealed record LinhaMatrizArmazenamento(
    SituacaoBancoVivo BancoVivo,
    SituacaoEspelho Espelho,
    ComparacaoSchemaEspelho SchemaEspelho,
    AcaoArmazenamento Acao,
    string Motivo);

/// <summary>Ação escolhida e a frase que a recuperação pode mostrar.</summary>
public sealed record DecisaoArmazenamento(AcaoArmazenamento Acao, string Motivo);

/// <summary>
/// Matriz de decisão do armazenamento. Função pura: não lê arquivo, não copia, não semeia e não apaga.
/// </summary>
/// <remarks>
/// A lista <see cref="Tabela"/> é a decisão. Cada combinação coerente de banco vivo,
/// espelho e schema do espelho aparece uma vez.
///
/// Banco vivo íntegro, desatualizado ou mais novo que o app não depende do espelho:
/// usar, migrar no lugar, ou pedir atualização. Copiar o espelho por cima desses
/// casos jogaria fora o arquivo que o usuário está usando.
///
/// Semear só existe na célula "banco ausente e espelho ausente". Espelho presente,
/// mesmo corrompido, não é instalação nova.
///
/// Schema do espelho mais novo que o app, quando o banco vivo não dá para usar,
/// pede atualização. Não restaura esse arquivo e não semeia no lugar.
///
/// Falha de execução (cópia ou migração que não conclui) não é linha da matriz.
/// O coordenador transforma essa falha em <see cref="AcaoArmazenamento.TelaDeRecuperacao"/>
/// sem chamar semeadura e sem chamar limpeza.
/// </remarks>
public static class MatrizDecisaoArmazenamento
{
    private static readonly LinhaMatrizArmazenamento[] Linhas = Montar();

    public static IReadOnlyList<LinhaMatrizArmazenamento> Tabela => Linhas;

    static MatrizDecisaoArmazenamento()
    {
        var chaves = new HashSet<(SituacaoBancoVivo, SituacaoEspelho, ComparacaoSchemaEspelho)>();
        foreach (var linha in Linhas)
        {
            if (string.IsNullOrWhiteSpace(linha.Motivo))
                throw new InvalidOperationException("Linha da matriz sem motivo.");

            var estado = new EstadoObservadoArmazenamento(linha.BancoVivo, linha.Espelho, linha.SchemaEspelho);
            estado.GarantirCoerente();
            if (!chaves.Add((linha.BancoVivo, linha.Espelho, linha.SchemaEspelho)))
                throw new InvalidOperationException("Linha duplicada na matriz de armazenamento.");
        }
    }

    public static DecisaoArmazenamento Decidir(EstadoObservadoArmazenamento estado)
    {
        ArgumentNullException.ThrowIfNull(estado);
        estado.GarantirCoerente();

        foreach (var linha in Linhas)
        {
            if (linha.BancoVivo == estado.BancoVivo
                && linha.Espelho == estado.Espelho
                && linha.SchemaEspelho == estado.SchemaEspelho)
                return new DecisaoArmazenamento(linha.Acao, linha.Motivo);
        }

        throw new InvalidOperationException(
            $"A matriz não tem linha para banco {estado.BancoVivo}, espelho {estado.Espelho}, schema {estado.SchemaEspelho}.");
    }

    private static LinhaMatrizArmazenamento[] Montar()
    {
        const string usar = "O banco vivo está íntegro. Ele é a fonte desta execução; o espelho não o substitui.";
        const string migrar = "O banco vivo é de um schema anterior e segue coerente. A migração ocorre nele; o espelho não é copiado por cima.";
        const string bancoMaisNovo = "O banco vivo foi gravado por uma versão mais nova do app. Pare e peça a atualização; não restaure o espelho por cima.";

        LinhaMatrizArmazenamento L(
            SituacaoBancoVivo banco,
            SituacaoEspelho espelho,
            ComparacaoSchemaEspelho schema,
            AcaoArmazenamento acao,
            string motivo) => new(banco, espelho, schema, acao, motivo);

        return
        [
            L(SituacaoBancoVivo.Integro, SituacaoEspelho.Ausente, ComparacaoSchemaEspelho.SemEspelho, AcaoArmazenamento.UsarBancoVivo, usar),
            L(SituacaoBancoVivo.Integro, SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel, AcaoArmazenamento.UsarBancoVivo, usar),
            L(SituacaoBancoVivo.Integro, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.Igual, AcaoArmazenamento.UsarBancoVivo, usar),
            L(SituacaoBancoVivo.Integro, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisAntigo, AcaoArmazenamento.UsarBancoVivo, usar),
            L(SituacaoBancoVivo.Integro, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisNovo, AcaoArmazenamento.UsarBancoVivo, usar),

            L(SituacaoBancoVivo.Desatualizado, SituacaoEspelho.Ausente, ComparacaoSchemaEspelho.SemEspelho, AcaoArmazenamento.MigrarBancoVivo, migrar),
            L(SituacaoBancoVivo.Desatualizado, SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel, AcaoArmazenamento.MigrarBancoVivo, migrar),
            L(SituacaoBancoVivo.Desatualizado, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.Igual, AcaoArmazenamento.MigrarBancoVivo, migrar),
            L(SituacaoBancoVivo.Desatualizado, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisAntigo, AcaoArmazenamento.MigrarBancoVivo, migrar),
            L(SituacaoBancoVivo.Desatualizado, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisNovo, AcaoArmazenamento.MigrarBancoVivo, migrar),

            L(SituacaoBancoVivo.SchemaMaisNovoQueOApp, SituacaoEspelho.Ausente, ComparacaoSchemaEspelho.SemEspelho, AcaoArmazenamento.PedirAtualizacaoDoApp, bancoMaisNovo),
            L(SituacaoBancoVivo.SchemaMaisNovoQueOApp, SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel, AcaoArmazenamento.PedirAtualizacaoDoApp, bancoMaisNovo),
            L(SituacaoBancoVivo.SchemaMaisNovoQueOApp, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.Igual, AcaoArmazenamento.PedirAtualizacaoDoApp, bancoMaisNovo),
            L(SituacaoBancoVivo.SchemaMaisNovoQueOApp, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisAntigo, AcaoArmazenamento.PedirAtualizacaoDoApp, bancoMaisNovo),
            L(SituacaoBancoVivo.SchemaMaisNovoQueOApp, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisNovo, AcaoArmazenamento.PedirAtualizacaoDoApp, bancoMaisNovo),

            L(SituacaoBancoVivo.Ausente, SituacaoEspelho.Ausente, ComparacaoSchemaEspelho.SemEspelho, AcaoArmazenamento.Semear,
                "Não há banco vivo nem espelho. É instalação nova: semear."),
            L(SituacaoBancoVivo.Ausente, SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel, AcaoArmazenamento.TelaDeRecuperacao,
                "Não há banco vivo e o espelho está corrompido. Abra a recuperação; não semeie."),
            L(SituacaoBancoVivo.Ausente, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.Igual, AcaoArmazenamento.RestaurarDoEspelho,
                "Não há banco vivo. O espelho é legível e o schema é o desta versão: restaurar."),
            L(SituacaoBancoVivo.Ausente, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisAntigo, AcaoArmazenamento.RestaurarDoEspelho,
                "Não há banco vivo. O espelho é legível e o schema é anterior: restaurar."),
            L(SituacaoBancoVivo.Ausente, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisNovo, AcaoArmazenamento.PedirAtualizacaoDoApp,
                "Não há banco vivo e o espelho é de uma versão mais nova do app. Pare e peça a atualização; não semeie."),

            L(SituacaoBancoVivo.Inconsistente, SituacaoEspelho.Ausente, ComparacaoSchemaEspelho.SemEspelho, AcaoArmazenamento.TelaDeRecuperacao,
                "O banco vivo está inconsistente e não há espelho. Abra a recuperação; não semeie e não apague."),
            L(SituacaoBancoVivo.Inconsistente, SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel, AcaoArmazenamento.TelaDeRecuperacao,
                "O banco vivo está inconsistente e o espelho está corrompido. Abra a recuperação; não semeie e não apague."),
            L(SituacaoBancoVivo.Inconsistente, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.Igual, AcaoArmazenamento.RestaurarDoEspelho,
                "O banco vivo está inconsistente. O espelho é legível e o schema é o desta versão: restaurar."),
            L(SituacaoBancoVivo.Inconsistente, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisAntigo, AcaoArmazenamento.RestaurarDoEspelho,
                "O banco vivo está inconsistente. O espelho é legível e o schema é anterior: restaurar."),
            L(SituacaoBancoVivo.Inconsistente, SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.EspelhoMaisNovo, AcaoArmazenamento.PedirAtualizacaoDoApp,
                "O banco vivo está inconsistente e o espelho é de uma versão mais nova do app. Pare e peça a atualização; não restaure e não semeie.")
        ];
    }
}
