using Android.Content;
using Android.Database;
using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;
using AndroidUri = Android.Net.Uri;

namespace OrcaFacil.Platforms.Android.Espelho;

/// <summary>
/// Pasta do espelho via SAF (<c>DocumentsContract</c> / <c>ContentResolver</c>).
/// A troca é temporário e depois rename. Se o provedor não anuncia
/// <see cref="DocumentContractFlags.SupportsRename"/>, a publicação para e o
/// arquivo anterior fica no nome definitivo — não há cópia por cima.
/// </summary>
/// <remarks>
/// Provedores como o Drive podem anunciar rename e ainda assim implementá-lo como
/// cópia mais exclusão, sem atomicidade. A pós-condição de troca confirmada exige
/// o destino visível e a origem ausente. Se os dois nomes continuam visíveis, este
/// volume não apaga nenhum dos dois: o protocolo publica com resíduo e a reserva
/// do arquivo anterior só sai numa troca confirmada. Fora isso, o único delete é o
/// temporário criado nesta operação, pelo URI que o próprio <c>createDocument</c> devolveu.
/// A URI da pasta não entra em mensagem nem em log.
/// </remarks>
public sealed class VolumePastaEspelhoAndroid : IVolumePastaEspelho
{
    private const string MimeEspelho = "application/octet-stream";

    private readonly IMemoriaPastaEspelho _memoria;

    public VolumePastaEspelhoAndroid(IMemoriaPastaEspelho memoria)
    {
        _memoria = memoria ?? throw new ArgumentNullException(nameof(memoria));
    }

    public string NomeArquivo => FormatoEspelho.NomeArquivoSugerido;

    public DiagnosticoPastaEspelho Consultar()
    {
        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return new DiagnosticoPastaEspelho(codigo, false);

        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PastaApagada, false);

            var pasta = DocumentsContract.BuildDocumentUriUsingTree(arvore, treeId);
            if (pasta is null || !DocumentoVisivel(resolver, pasta))
                return new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.PastaApagada, false);

            var busca = Buscar(resolver, arvore, treeId, NomeArquivo);
            if (busca.Falha is CodigoFalhaPastaEspelho falha)
                return new DiagnosticoPastaEspelho(falha, false);

            return new DiagnosticoPastaEspelho(CodigoFalhaPastaEspelho.Nenhuma, busca.Quantidade > 0);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new DiagnosticoPastaEspelho(Classificar(ex, resolver, arvore, null), false);
        }
    }

    public Task<ResultadoOperacaoPasta> CriarTemporarioAsync(
        string nomeTemporario,
        string arquivoOrigemLocal,
        CancellationToken cancellationToken)
    {
        var recusa = RegrasPastaEspelho.ImpedirCriacao(NomeArquivo, nomeTemporario);
        if (recusa is CodigoFalhaPastaEspelho codigo)
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

        return Task.FromResult(CriarTemporario(nomeTemporario, arquivoOrigemLocal, cancellationToken));
    }

    public Task<ResultadoRenomeacaoVolume> RenomearAsync(
        string nomeAtual,
        string nomeNovo,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!RegrasPastaEspelho.PodeRenomear(NomeArquivo, nomeAtual, nomeNovo))
            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.NomeRecusado));

        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(codigo));

        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.PastaApagada));

            var destino = Buscar(resolver, arvore, treeId, nomeNovo);
            if (destino.Falha is CodigoFalhaPastaEspelho falhaDestino)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(falhaDestino));
            if (destino.Quantidade > 0)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca, destinoJaExistia: true));

            var origem = Buscar(resolver, arvore, treeId, nomeAtual);
            if (origem.Falha is CodigoFalhaPastaEspelho falhaOrigem)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(falhaOrigem));
            if (origem.Quantidade == 0 || origem.Uri is null)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ArquivoAusente));
            if (origem.Quantidade != 1)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.Indisponivel));

            if (origem.Flags is DocumentContractFlags flags && !flags.HasFlag(DocumentContractFlags.SupportsRename))
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca));

            var renomeado = DocumentsContract.RenameDocument(resolver, origem.Uri, nomeNovo);
            if (renomeado is null)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca));

            var origemDepois = Buscar(resolver, arvore, treeId, nomeAtual);
            var destinoDepois = Buscar(resolver, arvore, treeId, nomeNovo);
            if (origemDepois.Falha is CodigoFalhaPastaEspelho falhaPosOrigem)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(falhaPosOrigem));
            if (destinoDepois.Falha is CodigoFalhaPastaEspelho falhaPosDestino)
                return Task.FromResult(ResultadoRenomeacaoVolume.Falha(falhaPosDestino));

            if (destinoDepois.Quantidade == 1 && origemDepois.Quantidade == 0)
                return Task.FromResult(ResultadoRenomeacaoVolume.Ok());

            // Os dois nomes visíveis: o rename virou cópia e a origem ficou.
            // Não apagar nenhum dos dois. O protocolo é quem decide o resíduo.
            if (destinoDepois.Quantidade > 0 && origemDepois.Quantidade > 0)
                return Task.FromResult(ResultadoRenomeacaoVolume.CopiaSemExcluirOrigem());

            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(CodigoFalhaPastaEspelho.TrocaNaoConfirmada));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Task.FromResult(ResultadoRenomeacaoVolume.Falha(Classificar(ex, resolver, arvore, null)));
        }
    }

    public Task<ResultadoOperacaoPasta> DescartarTemporarioAsync(
        string nomeTemporario,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var recusa = RegrasPastaEspelho.ImpedirDescarteTemporario(NomeArquivo, nomeTemporario);
        if (recusa is CodigoFalhaPastaEspelho codigo)
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

        return Task.FromResult(ApagarPorNome(nomeTemporario, exigeFinalPresente: false));
    }

    public Task<ResultadoOperacaoPasta> DescartarReservaAposTrocaAsync(
        string nomeReserva,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TentarAbrir(out var resolver, out var arvore, out var codigoAbertura))
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigoAbertura));

        bool finalPresente;
        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            var buscaFinal = Buscar(resolver, arvore, treeId!, NomeArquivo);
            if (buscaFinal.Falha is CodigoFalhaPastaEspelho falha)
                return Task.FromResult(ResultadoOperacaoPasta.Falha(falha));

            finalPresente = buscaFinal.Quantidade > 0;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Task.FromResult(ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null)));
        }

        var recusa = RegrasPastaEspelho.ImpedirDescarteReserva(NomeArquivo, nomeReserva, finalPresente);
        if (recusa is CodigoFalhaPastaEspelho codigo)
            return Task.FromResult(ResultadoOperacaoPasta.Falha(codigo));

        return Task.FromResult(ApagarPorNome(nomeReserva, exigeFinalPresente: true));
    }

    public async Task<ResultadoOperacaoPasta> CopiarParaLocalAsync(
        string nome,
        string caminhoLocal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(nome, NomeArquivo, StringComparison.Ordinal))
            return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.NomeRecusado);

        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return ResultadoOperacaoPasta.Falha(codigo);

        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            var busca = Buscar(resolver, arvore, treeId!, nome);
            if (busca.Falha is CodigoFalhaPastaEspelho falha)
                return ResultadoOperacaoPasta.Falha(falha);
            if (busca.Quantidade == 0 || busca.Uri is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ArquivoAusente);
            if (busca.Quantidade != 1)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel);

            var entrada = resolver.OpenInputStream(busca.Uri);
            if (entrada is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeLeitura);

            await using (entrada)
            await using (var saida = new FileStream(
                caminhoLocal,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                81920,
                FileOptions.Asynchronous))
            {
                await entrada.CopyToAsync(saida, cancellationToken);
                await saida.FlushAsync(cancellationToken);
            }

            return ResultadoOperacaoPasta.Ok();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null));
        }
    }

    private ResultadoOperacaoPasta CriarTemporario(
        string nomeTemporario,
        string arquivoOrigemLocal,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return ResultadoOperacaoPasta.Falha(codigo);

        AndroidUri? criado = null;
        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.PastaApagada);

            var pasta = DocumentsContract.BuildDocumentUriUsingTree(arvore, treeId);
            if (pasta is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.PastaApagada);

            var flagsPasta = FlagsDe(resolver, pasta);
            if (flagsPasta is DocumentContractFlags flags && !flags.HasFlag(DocumentContractFlags.DirSupportsCreate))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);

            var jaExiste = Buscar(resolver, arvore, treeId, nomeTemporario);
            if (jaExiste.Falha is CodigoFalhaPastaEspelho falhaBusca)
                return ResultadoOperacaoPasta.Falha(falhaBusca);
            if (jaExiste.Quantidade > 0)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);

            criado = DocumentsContract.CreateDocument(resolver, pasta, MimeEspelho, nomeTemporario);
            if (criado is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);

            if (!NomeConfere(resolver, criado, nomeTemporario))
            {
                DescartarDocumentoCriadoAgora(resolver, criado);
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.ProvedorSemTroca);
            }

            var saida = resolver.OpenOutputStream(criado);
            if (saida is null)
            {
                DescartarDocumentoCriadoAgora(resolver, criado);
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeEscrita);
            }

            using (saida)
            using (var entrada = new FileStream(
                arquivoOrigemLocal,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.SequentialScan))
            {
                cancellationToken.ThrowIfCancellationRequested();
                entrada.CopyTo(saida);
                saida.Flush();
            }

            return ResultadoOperacaoPasta.Ok();
        }
        catch (OperationCanceledException)
        {
            if (criado is not null)
                DescartarDocumentoCriadoAgora(resolver, criado);
            throw;
        }
        catch (Exception ex)
        {
            if (criado is not null)
                DescartarDocumentoCriadoAgora(resolver, criado);

            return ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null));
        }
    }

    private ResultadoOperacaoPasta ApagarPorNome(string nome, bool exigeFinalPresente)
    {
        if (!TentarAbrir(out var resolver, out var arvore, out var codigo))
            return ResultadoOperacaoPasta.Falha(codigo);

        try
        {
            var treeId = DocumentsContract.GetTreeDocumentId(arvore);
            if (string.IsNullOrEmpty(treeId))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.PastaApagada);

            if (exigeFinalPresente)
            {
                var final = Buscar(resolver, arvore, treeId, NomeArquivo);
                if (final.Falha is CodigoFalhaPastaEspelho falhaFinal)
                    return ResultadoOperacaoPasta.Falha(falhaFinal);
                if (final.Quantidade == 0)
                    return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.EspelhoPreservadoForaDoNome);
            }

            var busca = Buscar(resolver, arvore, treeId, nome);
            if (busca.Falha is CodigoFalhaPastaEspelho falha)
                return ResultadoOperacaoPasta.Falha(falha);
            if (busca.Quantidade == 0)
                return ResultadoOperacaoPasta.Ok();
            if (busca.Quantidade != 1 || busca.Uri is null)
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.Indisponivel);

            if (busca.Flags is DocumentContractFlags flags && !flags.HasFlag(DocumentContractFlags.SupportsDelete))
                return ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeEscrita);

            var apagou = DocumentsContract.DeleteDocument(resolver, busca.Uri);
            return apagou
                ? ResultadoOperacaoPasta.Ok()
                : ResultadoOperacaoPasta.Falha(CodigoFalhaPastaEspelho.FalhaDeEscrita);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return ResultadoOperacaoPasta.Falha(Classificar(ex, resolver, arvore, null));
        }
    }

    private static void DescartarDocumentoCriadoAgora(ContentResolver resolver, AndroidUri uri)
    {
        try
        {
            DocumentsContract.DeleteDocument(resolver, uri);
        }
        catch (Exception)
        {
        }
    }

    private bool TentarAbrir(out ContentResolver resolver, out AndroidUri arvore, out CodigoFalhaPastaEspelho codigo)
    {
        resolver = null!;
        arvore = null!;
        var texto = _memoria.LerUri();
        if (!UriEscolhaPasta.EhUriDeConteudo(texto))
        {
            codigo = CodigoFalhaPastaEspelho.PastaNaoEscolhida;
            return false;
        }

        var obtido = Resolver();
        if (obtido is null)
        {
            codigo = CodigoFalhaPastaEspelho.Indisponivel;
            return false;
        }

        AndroidUri? uri;
        try
        {
            uri = AndroidUri.Parse(texto);
        }
        catch (Exception)
        {
            codigo = CodigoFalhaPastaEspelho.Indisponivel;
            return false;
        }

        if (uri is null || !"content".Equals(uri.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            codigo = CodigoFalhaPastaEspelho.Indisponivel;
            return false;
        }

        if (!PermissaoPastaEspelho.Vigente(obtido, uri))
        {
            codigo = CodigoFalhaPastaEspelho.PermissaoRevogada;
            return false;
        }

        resolver = obtido;
        arvore = uri;
        codigo = CodigoFalhaPastaEspelho.Nenhuma;
        return true;
    }

    private static ContentResolver? Resolver()
    {
        var atividade = Platform.CurrentActivity;
        if (atividade?.ContentResolver is not null)
            return atividade.ContentResolver;

        return Android.App.Application.Context?.ContentResolver;
    }

    private static CodigoFalhaPastaEspelho Classificar(
        Exception excecao,
        ContentResolver resolver,
        AndroidUri arvore,
        bool? documentoEncontrado)
    {
        bool? permissao = null;
        try
        {
            permissao = PermissaoPastaEspelho.Vigente(resolver, arvore);
        }
        catch (Exception)
        {
        }

        return ClassificadorFalhaPastaEspelho.Classificar(
            SinalFalhaPasta.De(excecao, permissao, documentoEncontrado));
    }

    private static bool DocumentoVisivel(ContentResolver resolver, AndroidUri documento)
    {
        ICursor? cursor = null;
        try
        {
            cursor = resolver.Query(documento, [DocumentsContract.Document.ColumnDocumentId], null, null, null);
            return cursor is not null && cursor.MoveToFirst();
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static DocumentContractFlags? FlagsDe(ContentResolver resolver, AndroidUri documento)
    {
        ICursor? cursor = null;
        try
        {
            cursor = resolver.Query(documento, [DocumentsContract.Document.ColumnFlags], null, null, null);
            if (cursor is null || !cursor.MoveToFirst())
                return null;

            var indice = cursor.GetColumnIndex(DocumentsContract.Document.ColumnFlags);
            if (indice < 0 || cursor.IsNull(indice))
                return null;

            return (DocumentContractFlags)cursor.GetInt(indice);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static bool NomeConfere(ContentResolver resolver, AndroidUri documento, string nome)
    {
        ICursor? cursor = null;
        try
        {
            cursor = resolver.Query(documento, [DocumentsContract.Document.ColumnDisplayName], null, null, null);
            if (cursor is null || !cursor.MoveToFirst())
                return false;

            var indice = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
            if (indice < 0 || cursor.IsNull(indice))
                return false;

            return string.Equals(cursor.GetString(indice), nome, StringComparison.Ordinal);
        }
        catch (Exception)
        {
            return false;
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static BuscaNome Buscar(ContentResolver resolver, AndroidUri arvore, string treeId, string nome)
    {
        ICursor? cursor = null;
        try
        {
            var filhos = DocumentsContract.BuildChildDocumentsUriUsingTree(arvore, treeId);
            if (filhos is null)
                return BuscaNome.ComFalha(CodigoFalhaPastaEspelho.PastaApagada);

            cursor = resolver.Query(
                filhos,
                [
                    DocumentsContract.Document.ColumnDocumentId,
                    DocumentsContract.Document.ColumnDisplayName,
                    DocumentsContract.Document.ColumnFlags
                ],
                null,
                null,
                null);

            if (cursor is null)
                return BuscaNome.ComFalha(CodigoFalhaPastaEspelho.PastaApagada);

            var indiceId = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDocumentId);
            var indiceNome = cursor.GetColumnIndex(DocumentsContract.Document.ColumnDisplayName);
            var indiceFlags = cursor.GetColumnIndex(DocumentsContract.Document.ColumnFlags);
            if (indiceId < 0 || indiceNome < 0)
                return BuscaNome.ComFalha(CodigoFalhaPastaEspelho.Indisponivel);

            AndroidUri? encontrado = null;
            DocumentContractFlags? flags = null;
            var quantidade = 0;
            while (cursor.MoveToNext())
            {
                if (cursor.IsNull(indiceNome))
                    continue;

                var exibicao = cursor.GetString(indiceNome);
                if (!string.Equals(exibicao, nome, StringComparison.Ordinal))
                    continue;

                quantidade++;
                if (quantidade > 1)
                    continue;

                var id = cursor.IsNull(indiceId) ? null : cursor.GetString(indiceId);
                if (!string.IsNullOrEmpty(id))
                    encontrado = DocumentsContract.BuildDocumentUriUsingTree(arvore, id);

                if (indiceFlags >= 0 && !cursor.IsNull(indiceFlags))
                    flags = (DocumentContractFlags)cursor.GetInt(indiceFlags);
            }

            return new BuscaNome(null, quantidade, encontrado, flags);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return BuscaNome.ComFalha(ClassificarBusca(ex));
        }
        finally
        {
            cursor?.Close();
            cursor?.Dispose();
        }
    }

    private static CodigoFalhaPastaEspelho ClassificarBusca(Exception excecao)
        => ClassificadorFalhaPastaEspelho.Classificar(SinalFalhaPasta.De(excecao));

    private readonly record struct BuscaNome(
        CodigoFalhaPastaEspelho? Falha,
        int Quantidade,
        AndroidUri? Uri,
        DocumentContractFlags? Flags)
    {
        public static BuscaNome ComFalha(CodigoFalhaPastaEspelho codigo) => new(codigo, 0, null, null);
    }
}
