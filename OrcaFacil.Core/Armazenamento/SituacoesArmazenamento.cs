namespace OrcaFacil.Core.Armazenamento;

/// <summary>O que o coordenador pode decidir. Não há ação de apagar arquivo.</summary>
public enum AcaoArmazenamento
{
    /// <summary>O banco interno já está no schema deste app.</summary>
    UsarBancoVivo,

    /// <summary>O banco interno é antigo e coerente. Migra no lugar. O espelho não é copiado por cima.</summary>
    MigrarBancoVivo,

    /// <summary>Copia o espelho para o banco interno. Se a cópia falhar, nada é semeado e nada é apagado.</summary>
    RestaurarDoEspelho,

    /// <summary>
    /// Não há dado utilizável para o app: arquivo ausente, SQLite vazio sem espelho,
    /// ou schema íntegro ainda sem membro/perfil. O coordenador garante o schema e semeia.
    /// </summary>
    Semear,

    /// <summary>O schema encontrado é mais novo que este app. Para e pede atualização.</summary>
    PedirAtualizacaoDoApp,

    /// <summary>Não há caminho seguro. A UI de recuperação explica; este fluxo não apaga e não semeia.</summary>
    TelaDeRecuperacao
}

/// <summary>
/// Situação do banco interno, já traduzida do <see cref="Data.RelatorioIntegridadeBanco"/>.
/// </summary>
public enum SituacaoBancoVivo
{
    Ausente,

    /// <summary>O arquivo existe, foi lido e não tem tabela do app nem histórico. Reinstalação com SQLite vazio.</summary>
    Vazio,

    Integro,

    /// <summary>
    /// O schema deste app está íntegro e não há membro/perfil.
    /// A semeadura que falhou depois do schema é repetida. O espelho não substitui o arquivo.
    /// </summary>
    SemPerfil,

    Desatualizado,

    /// <summary>
    /// A migration AddCartoes está pela metade. A migração passa por
    /// <see cref="OrcaFacil.Core.Data.ReconciliadorAddCartoes"/> antes de repetir o Migrate.
    /// </summary>
    ParcialAddCartoes,

    Inconsistente,
    SchemaMaisNovoQueOApp
}

/// <summary>Situação do arquivo-espelho na pasta escolhida pelo usuário.</summary>
public enum SituacaoEspelho
{
    Ausente,
    Legivel,
    Corrompido
}

/// <summary>Schema do espelho comparado com o schema que este build do app sabe abrir.</summary>
public enum ComparacaoSchemaEspelho
{
    /// <summary>Não há arquivo de espelho.</summary>
    SemEspelho,

    /// <summary>O arquivo existe, mas não deu para ler a versão.</summary>
    NaoAplicavel,

    Igual,
    EspelhoMaisAntigo,
    EspelhoMaisNovo
}

/// <summary>Estado observado antes de qualquer cópia, migração ou semeadura.</summary>
public sealed record EstadoObservadoArmazenamento(
    SituacaoBancoVivo BancoVivo,
    SituacaoEspelho Espelho,
    ComparacaoSchemaEspelho SchemaEspelho)
{
    public bool EhCoerente => (Espelho, SchemaEspelho) switch
    {
        (SituacaoEspelho.Ausente, ComparacaoSchemaEspelho.SemEspelho) => true,
        (SituacaoEspelho.Corrompido, ComparacaoSchemaEspelho.NaoAplicavel) => true,
        (SituacaoEspelho.Legivel, ComparacaoSchemaEspelho.Igual
            or ComparacaoSchemaEspelho.EspelhoMaisAntigo
            or ComparacaoSchemaEspelho.EspelhoMaisNovo) => true,
        _ => false
    };

    public void GarantirCoerente()
    {
        if (!EhCoerente)
        {
            throw new ArgumentException(
                $"Espelho {Espelho} não combina com a comparação de schema {SchemaEspelho}.");
        }
    }
}

/// <summary>Compara números de schema. Quem grava o número no espelho é o PR do snapshot.</summary>
public static class ComparadorSchemaEspelho
{
    public static ComparacaoSchemaEspelho Comparar(int versaoApp, int versaoEspelho)
    {
        if (versaoApp < 0)
            throw new ArgumentOutOfRangeException(nameof(versaoApp));
        if (versaoEspelho < 0)
            throw new ArgumentOutOfRangeException(nameof(versaoEspelho));

        if (versaoEspelho == versaoApp)
            return ComparacaoSchemaEspelho.Igual;
        if (versaoEspelho < versaoApp)
            return ComparacaoSchemaEspelho.EspelhoMaisAntigo;
        return ComparacaoSchemaEspelho.EspelhoMaisNovo;
    }
}
