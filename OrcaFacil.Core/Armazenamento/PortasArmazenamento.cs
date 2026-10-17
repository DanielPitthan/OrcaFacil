using OrcaFacil.Core.Data;

namespace OrcaFacil.Core.Armazenamento;

/// <summary>
/// Banco interno. <see cref="VerificarAsync"/> tem de delegar a
/// <see cref="VerificadorIntegridadeBanco.VerificarAsync"/> e não pode criar o arquivo.
/// </summary>
public interface ISondaBancoVivo
{
    bool Existe();

    Task<RelatorioIntegridadeBanco> VerificarAsync(CancellationToken cancellationToken = default);
}

/// <summary>Versão de schema que este build grava no espelho. O coordenador só compara o número.</summary>
public interface IVersaoSchemaApp
{
    int Atual { get; }
}

/// <summary>
/// Espelho na pasta do usuário. <see cref="LerAsync"/> não copia, não apaga e não abre UI de senha.
/// Sem senha ou com arquivo inválido, devolve ilegível.
/// </summary>
public interface IEspelhoDados
{
    bool Existe();

    Task<LeituraEspelho> LerAsync(CancellationToken cancellationToken = default);
}

/// <summary>Metadados lidos do espelho. Arquivo ilegível não traz versão.</summary>
public sealed record LeituraEspelho
{
    public LeituraEspelho(bool legivel, int? versaoSchema, string? motivo)
    {
        if (legivel)
        {
            if (versaoSchema is null or < 0)
                throw new ArgumentException("Espelho legível precisa de versão de schema maior ou igual a zero.");
            if (!string.IsNullOrEmpty(motivo))
                throw new ArgumentException("Espelho legível não tem motivo de falha.");
        }
        else if (versaoSchema is not null)
        {
            throw new ArgumentException("Espelho ilegível não tem versão de schema.");
        }
        else if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new ArgumentException("Espelho ilegível precisa do motivo.");
        }

        Legivel = legivel;
        VersaoSchema = versaoSchema;
        Motivo = motivo;
    }

    public bool Legivel { get; }

    public int? VersaoSchema { get; }

    public string? Motivo { get; }

    public static LeituraEspelho Ok(int versaoSchema) => new(true, versaoSchema, null);

    public static LeituraEspelho Ilegivel(string motivo) => new(false, null, motivo);
}

/// <summary>
/// Cópia do espelho para o banco vivo. Se falhar, os dois arquivos permanecem:
/// não apaga o banco vivo e não apaga o espelho. O coordenador também não semeia.
/// </summary>
public interface IRestauracaoEspelho
{
    Task<ResultadoRestauracao> RestaurarAsync(CancellationToken cancellationToken = default);
}

public sealed record ResultadoRestauracao(bool Sucesso, string? MotivoFalha)
{
    public static ResultadoRestauracao Ok() => new(true, null);

    public static ResultadoRestauracao Falha(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            throw new ArgumentException("Informe o motivo da falha.", nameof(motivo));

        return new ResultadoRestauracao(false, motivo);
    }
}

/// <summary>
/// Migração do banco vivo. A implementação chama <see cref="PreparoBanco.MigrarEVerificarAsync"/>.
/// </summary>
public interface IMigracaoBancoVivo
{
    Task<ResultadoPreparoBanco> MigrarEVerificarAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Dados iniciais de uma instalação nova. O coordenador só chama quando a matriz escolhe
/// <see cref="AcaoArmazenamento.Semear"/>. Restauração que falha não chega aqui.
/// </summary>
public interface ISemeaduraDados
{
    Task SemearAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Apaga os dois lados. Só <see cref="IArmazenamentoDados.LimparTudoAsync"/> chama,
/// e só com <see cref="PedidoLimparTudo"/> explícito.
/// </summary>
public interface ILimpezaTotalDados
{
    Task RemoverBancoVivoAsync(CancellationToken cancellationToken = default);

    Task RemoverEspelhoAsync(CancellationToken cancellationToken = default);
}
