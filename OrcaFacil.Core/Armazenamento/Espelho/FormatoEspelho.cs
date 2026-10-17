using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Arquivo único do espelho. O cabeçalho é texto claro autenticado como AAD do AES-256-GCM;
/// o SQLite vai cifrado em seguida. A tag fecha o arquivo.
/// </summary>
/// <remarks>
/// Layout versão 1, little-endian:
/// magic <c>ORFESPEL</c> (8), versão do formato (u16), tamanho do cabeçalho (u32),
/// versão de schema do app (i32), última migration (u16 + UTF-8), data UTC em milissegundos (i64),
/// tamanho do SQLite (i64), SHA-256 do SQLite (32), salt (16), iterações PBKDF2 (u32),
/// nonce GCM (12), verificador da senha (32). Depois: ciphertext do mesmo tamanho do SQLite e tag (16).
///
/// A chave mestra sai de PBKDF2-HMAC-SHA256 (salt de 16 bytes, 600 mil iterações, 64 bytes).
/// Os 32 primeiros bytes são a chave AES-256-GCM; os 32 seguintes autenticam a senha com
/// HMAC-SHA256 de um domínio fixo. O HMAC não cobre o arquivo: senha errada falha nele
/// (<see cref="SenhaIncorretaException"/>). Cabeçalho e ciphertext entram no GCM; adulteração
/// com a senha certa falha na tag (<see cref="ArquivoCorrompidoException"/>).
/// Argon2id não está em System.Security.Cryptography; por isso o KDF é PBKDF2.
/// </remarks>
public static class FormatoEspelho
{
    public const ushort VersaoAtual = 1;

    public const int IteracoesPadrao = 600_000;

    public const int IteracoesMinimas = 600_000;

    public const int IteracoesMaximas = 2_000_000;

    public const int TamanhoMagic = 8;

    public const int OffsetVersaoFormato = 8;

    public const int OffsetTamanhoCabecalho = 10;

    public const int TamanhoPrefixo = 14;

    public const int TamanhoChave = 32;

    public const int TamanhoTag = 16;

    public const int TamanhoSalt = 16;

    public const int TamanhoNonce = 12;

    public const int TamanhoHash = 32;

    public const int TamanhoVerificador = 32;

    public const int MigrationMaximoBytes = 200;

    /// <summary>Prefixo 14 + schema 4 + tamanho da migration 2 + campos fixos 112.</summary>
    public const int TamanhoMinimoCabecalho = 132;

    public const int TamanhoMaximoCabecalho = TamanhoMinimoCabecalho + MigrationMaximoBytes;

    public const long TamanhoMaximoConteudo = 512L * 1024 * 1024;

    public const string NomeArquivoSugerido = "orcafacil.espelho";

    public static ReadOnlySpan<byte> Magic => "ORFESPEL"u8;

    public static ReadOnlySpan<byte> DominioSenha => "orca-espelho-senha-v1"u8;
}

public sealed record MetadadosEspelho(
    ushort VersaoFormato,
    int VersaoSchema,
    string UltimaMigration,
    DateTimeOffset GeradoEm,
    long TamanhoBytes,
    string HashSha256,
    int IteracoesPbkdf2);

/// <summary>Cifra e decifra o arquivo. Não abre SQLite e não publica.</summary>
public static class CofreEspelho
{
    public static MetadadosEspelho Gravar(
        string caminhoArquivo,
        ReadOnlySpan<byte> sqlite,
        string senha,
        int versaoSchema,
        string ultimaMigration,
        DateTimeOffset geradoEm,
        int iteracoes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoArquivo);
        RegraSenhaEspelho.Exigir(senha);
        ArgumentNullException.ThrowIfNull(ultimaMigration);
        if (versaoSchema < 0)
            throw new ArgumentOutOfRangeException(nameof(versaoSchema));
        if (iteracoes < FormatoEspelho.IteracoesMinimas || iteracoes > FormatoEspelho.IteracoesMaximas)
            throw new ArgumentOutOfRangeException(nameof(iteracoes));
        if (sqlite.Length > FormatoEspelho.TamanhoMaximoConteudo)
            throw new ArgumentOutOfRangeException(nameof(sqlite));

        var migrationBytes = Encoding.UTF8.GetBytes(ultimaMigration);
        if (migrationBytes.Length > FormatoEspelho.MigrationMaximoBytes)
            throw new ArgumentException("A identificação da migration excede o cabeçalho.", nameof(ultimaMigration));

        var gerado = DateTimeOffset.FromUnixTimeMilliseconds(geradoEm.ToUnixTimeMilliseconds());
        var hash = SHA256.HashData(sqlite);
        var salt = RandomNumberGenerator.GetBytes(FormatoEspelho.TamanhoSalt);
        var nonce = RandomNumberGenerator.GetBytes(FormatoEspelho.TamanhoNonce);
        byte[]? senhaBytes = null;
        byte[]? material = null;
        try
        {
            senhaBytes = Encoding.UTF8.GetBytes(senha);
            material = Derivar(senhaBytes, salt, iteracoes);
            var verificador = HMACSHA256.HashData(material.AsSpan(FormatoEspelho.TamanhoChave), FormatoEspelho.DominioSenha);
            var cabecalho = MontarCabecalho(
                versaoSchema,
                migrationBytes,
                gerado.ToUnixTimeMilliseconds(),
                sqlite.Length,
                hash,
                salt,
                iteracoes,
                nonce,
                verificador);

            var ciphertext = new byte[sqlite.Length];
            var tag = new byte[FormatoEspelho.TamanhoTag];
            using (var aes = new AesGcm(material.AsSpan(0, FormatoEspelho.TamanhoChave), FormatoEspelho.TamanhoTag))
            {
                aes.Encrypt(nonce, sqlite, ciphertext, tag, cabecalho);
            }

            var arquivo = new byte[cabecalho.Length + ciphertext.Length + tag.Length];
            cabecalho.CopyTo(arquivo, 0);
            ciphertext.CopyTo(arquivo.AsSpan(cabecalho.Length));
            tag.CopyTo(arquivo.AsSpan(cabecalho.Length + ciphertext.Length));
            File.WriteAllBytes(caminhoArquivo, arquivo);

            return new MetadadosEspelho(
                FormatoEspelho.VersaoAtual,
                versaoSchema,
                ultimaMigration,
                gerado,
                sqlite.Length,
                Convert.ToHexString(hash).ToLowerInvariant(),
                iteracoes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(salt);
            CryptographicOperations.ZeroMemory(nonce);
            if (material is not null)
                CryptographicOperations.ZeroMemory(material);
            if (senhaBytes is not null)
                CryptographicOperations.ZeroMemory(senhaBytes);
        }
    }

    public static (MetadadosEspelho Metadados, byte[] Sqlite) Ler(string caminhoArquivo, string senha)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caminhoArquivo);
        ArgumentNullException.ThrowIfNull(senha);

        byte[] arquivo;
        try
        {
            var info = new FileInfo(caminhoArquivo);
            if (!info.Exists
                || info.Length < FormatoEspelho.TamanhoPrefixo
                || info.Length > FormatoEspelho.TamanhoMaximoConteudo + FormatoEspelho.TamanhoMaximoCabecalho + FormatoEspelho.TamanhoTag)
                throw new ArquivoCorrompidoException();

            arquivo = File.ReadAllBytes(caminhoArquivo);
        }
        catch (FalhaEspelhoException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            throw new ArquivoCorrompidoException();
        }

        var cabecalho = LerCabecalho(arquivo, out var campos);
        var ciphertext = arquivo.AsSpan(cabecalho.Length, arquivo.Length - cabecalho.Length - FormatoEspelho.TamanhoTag);
        var tag = arquivo.AsSpan(arquivo.Length - FormatoEspelho.TamanhoTag, FormatoEspelho.TamanhoTag);

        byte[]? senhaBytes = null;
        byte[]? material = null;
        byte[]? sqlite = null;
        try
        {
            senhaBytes = Encoding.UTF8.GetBytes(senha);
            material = Derivar(senhaBytes, campos.Salt, campos.Iteracoes);
            var verificador = HMACSHA256.HashData(material.AsSpan(FormatoEspelho.TamanhoChave), FormatoEspelho.DominioSenha);
            if (!CryptographicOperations.FixedTimeEquals(verificador, campos.Verificador))
                throw new SenhaIncorretaException();

            sqlite = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(material.AsSpan(0, FormatoEspelho.TamanhoChave), FormatoEspelho.TamanhoTag);
                aes.Decrypt(campos.Nonce, ciphertext, tag, sqlite, cabecalho);
            }
            catch (CryptographicException)
            {
                throw new ArquivoCorrompidoException();
            }

            var hash = SHA256.HashData(sqlite);
            if (!CryptographicOperations.FixedTimeEquals(hash, campos.Hash))
                throw new ArquivoCorrompidoException();

            var metadados = new MetadadosEspelho(
                FormatoEspelho.VersaoAtual,
                campos.VersaoSchema,
                campos.UltimaMigration,
                campos.GeradoEm,
                sqlite.LongLength,
                Convert.ToHexString(hash).ToLowerInvariant(),
                campos.Iteracoes);
            var pronto = sqlite;
            sqlite = null;
            return (metadados, pronto);
        }
        finally
        {
            if (material is not null)
                CryptographicOperations.ZeroMemory(material);
            if (senhaBytes is not null)
                CryptographicOperations.ZeroMemory(senhaBytes);
            if (sqlite is not null)
                CryptographicOperations.ZeroMemory(sqlite);
        }
    }

    private static byte[] Derivar(byte[] senha, ReadOnlySpan<byte> salt, int iteracoes)
        => Rfc2898DeriveBytes.Pbkdf2(
            senha,
            salt,
            iteracoes,
            HashAlgorithmName.SHA256,
            FormatoEspelho.TamanhoChave * 2);

    private static byte[] MontarCabecalho(
        int versaoSchema,
        byte[] migration,
        long geradoUnixMs,
        long tamanho,
        byte[] hash,
        byte[] salt,
        int iteracoes,
        byte[] nonce,
        byte[] verificador)
    {
        using var buffer = new MemoryStream();
        Escrever(buffer, FormatoEspelho.Magic);
        EscreverU16(buffer, FormatoEspelho.VersaoAtual);
        EscreverU32(buffer, 0);
        EscreverI32(buffer, versaoSchema);
        EscreverU16(buffer, (ushort)migration.Length);
        Escrever(buffer, migration);
        EscreverI64(buffer, geradoUnixMs);
        EscreverI64(buffer, tamanho);
        Escrever(buffer, hash);
        Escrever(buffer, salt);
        EscreverU32(buffer, (uint)iteracoes);
        Escrever(buffer, nonce);
        Escrever(buffer, verificador);

        var cabecalho = buffer.ToArray();
        if (cabecalho.Length < FormatoEspelho.TamanhoMinimoCabecalho || cabecalho.Length > FormatoEspelho.TamanhoMaximoCabecalho)
            throw new InvalidOperationException("Cabeçalho do espelho fora do tamanho da versão 1.");

        BinaryPrimitives.WriteUInt32LittleEndian(
            cabecalho.AsSpan(FormatoEspelho.OffsetTamanhoCabecalho),
            (uint)cabecalho.Length);
        return cabecalho;
    }

    private static ReadOnlySpan<byte> LerCabecalho(byte[] arquivo, out CamposCabecalho campos)
    {
        if (arquivo.Length < FormatoEspelho.TamanhoPrefixo
            || !arquivo.AsSpan(0, FormatoEspelho.TamanhoMagic).SequenceEqual(FormatoEspelho.Magic))
            throw new ArquivoCorrompidoException();

        var versaoFormato = BinaryPrimitives.ReadUInt16LittleEndian(arquivo.AsSpan(FormatoEspelho.OffsetVersaoFormato, 2));
        if (versaoFormato != FormatoEspelho.VersaoAtual)
            throw new VersaoFormatoNaoSuportadaException(versaoFormato);

        var tamanhoCabecalho = BinaryPrimitives.ReadUInt32LittleEndian(arquivo.AsSpan(FormatoEspelho.OffsetTamanhoCabecalho, 4));
        if (tamanhoCabecalho < FormatoEspelho.TamanhoMinimoCabecalho
            || tamanhoCabecalho > FormatoEspelho.TamanhoMaximoCabecalho
            || arquivo.Length < tamanhoCabecalho + FormatoEspelho.TamanhoTag)
            throw new ArquivoCorrompidoException();

        var cabecalho = arquivo.AsSpan(0, (int)tamanhoCabecalho);
        var offset = FormatoEspelho.TamanhoPrefixo;
        var versaoSchema = LerI32(cabecalho, ref offset);
        var ultima = LerTexto(cabecalho, ref offset);
        var geradoMs = LerI64(cabecalho, ref offset);
        var tamanho = LerI64(cabecalho, ref offset);
        var hash = LerBytes(cabecalho, ref offset, FormatoEspelho.TamanhoHash);
        var salt = LerBytes(cabecalho, ref offset, FormatoEspelho.TamanhoSalt);
        var iteracoesValor = LerU32(cabecalho, ref offset);
        var nonce = LerBytes(cabecalho, ref offset, FormatoEspelho.TamanhoNonce);
        var verificador = LerBytes(cabecalho, ref offset, FormatoEspelho.TamanhoVerificador);
        if (offset != cabecalho.Length)
            throw new ArquivoCorrompidoException();

        if (versaoSchema < 0 || tamanho < 0 || tamanho > FormatoEspelho.TamanhoMaximoConteudo)
            throw new ArquivoCorrompidoException();
        if (iteracoesValor < (uint)FormatoEspelho.IteracoesMinimas || iteracoesValor > (uint)FormatoEspelho.IteracoesMaximas)
            throw new ArquivoCorrompidoException();

        var iteracoes = (int)iteracoesValor;

        var ciphertext = arquivo.Length - cabecalho.Length - FormatoEspelho.TamanhoTag;
        if (tamanho != ciphertext)
            throw new ArquivoCorrompidoException();

        DateTimeOffset gerado;
        try
        {
            gerado = DateTimeOffset.FromUnixTimeMilliseconds(geradoMs);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ArquivoCorrompidoException();
        }

        campos = new CamposCabecalho(versaoSchema, ultima, gerado, hash, salt, iteracoes, nonce, verificador);
        return cabecalho;
    }

    private static int LerI32(ReadOnlySpan<byte> dados, ref int offset)
    {
        Exigir(dados, offset, 4);
        var valor = BinaryPrimitives.ReadInt32LittleEndian(dados.Slice(offset, 4));
        offset += 4;
        return valor;
    }

    private static long LerI64(ReadOnlySpan<byte> dados, ref int offset)
    {
        Exigir(dados, offset, 8);
        var valor = BinaryPrimitives.ReadInt64LittleEndian(dados.Slice(offset, 8));
        offset += 8;
        return valor;
    }

    private static uint LerU32(ReadOnlySpan<byte> dados, ref int offset)
    {
        Exigir(dados, offset, 4);
        var valor = BinaryPrimitives.ReadUInt32LittleEndian(dados.Slice(offset, 4));
        offset += 4;
        return valor;
    }

    private static byte[] LerBytes(ReadOnlySpan<byte> dados, ref int offset, int tamanho)
    {
        Exigir(dados, offset, tamanho);
        var valor = dados.Slice(offset, tamanho).ToArray();
        offset += tamanho;
        return valor;
    }

    private static string LerTexto(ReadOnlySpan<byte> dados, ref int offset)
    {
        Exigir(dados, offset, 2);
        var tamanho = BinaryPrimitives.ReadUInt16LittleEndian(dados.Slice(offset, 2));
        offset += 2;
        if (tamanho > FormatoEspelho.MigrationMaximoBytes)
            throw new ArquivoCorrompidoException();

        Exigir(dados, offset, tamanho);
        var bytes = dados.Slice(offset, tamanho);
        offset += tamanho;
        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        catch (ArgumentException)
        {
            throw new ArquivoCorrompidoException();
        }
    }

    private static void Exigir(ReadOnlySpan<byte> dados, int offset, int tamanho)
    {
        if (tamanho < 0 || offset < 0 || offset > dados.Length - tamanho)
            throw new ArquivoCorrompidoException();
    }

    private static void Escrever(Stream stream, ReadOnlySpan<byte> dados) => stream.Write(dados);

    private static void EscreverU16(Stream stream, ushort valor)
    {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, valor);
        stream.Write(buffer);
    }

    private static void EscreverU32(Stream stream, uint valor)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, valor);
        stream.Write(buffer);
    }

    private static void EscreverI32(Stream stream, int valor)
    {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, valor);
        stream.Write(buffer);
    }

    private static void EscreverI64(Stream stream, long valor)
    {
        Span<byte> buffer = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, valor);
        stream.Write(buffer);
    }

    private readonly struct CamposCabecalho(
        int versaoSchema,
        string ultimaMigration,
        DateTimeOffset geradoEm,
        byte[] hash,
        byte[] salt,
        int iteracoes,
        byte[] nonce,
        byte[] verificador)
    {
        public int VersaoSchema { get; } = versaoSchema;
        public string UltimaMigration { get; } = ultimaMigration;
        public DateTimeOffset GeradoEm { get; } = geradoEm;
        public byte[] Hash { get; } = hash;
        public byte[] Salt { get; } = salt;
        public int Iteracoes { get; } = iteracoes;
        public byte[] Nonce { get; } = nonce;
        public byte[] Verificador { get; } = verificador;
    }
}
