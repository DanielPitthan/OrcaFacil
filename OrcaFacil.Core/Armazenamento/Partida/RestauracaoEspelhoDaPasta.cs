using OrcaFacil.Core.Armazenamento;
using OrcaFacil.Core.Armazenamento.Espelho;
using OrcaFacil.Core.Armazenamento.Espelho.Pasta;

namespace OrcaFacil.Core.Armazenamento.Partida;

/// <summary>
/// Copia o espelho da pasta do usuário para o banco vivo.
/// Sem senha, não lê e não grava. Falha deixa o banco, o backup e o espelho no lugar.
/// </summary>
public sealed class RestauracaoEspelhoDaPasta : IRestauracaoEspelho
{
    private readonly IVolumePastaEspelho _volume;
    private readonly IProvedorSenhaEspelho _senha;
    private readonly IAreaTemporariaEspelho _area;
    private readonly VersaoSchemaAplicacao _versao;
    private readonly CaminhoBancoVivo _caminho;

    public RestauracaoEspelhoDaPasta(
        IVolumePastaEspelho volume,
        IProvedorSenhaEspelho senha,
        IAreaTemporariaEspelho area,
        VersaoSchemaAplicacao versao,
        CaminhoBancoVivo caminho)
    {
        _volume = volume ?? throw new ArgumentNullException(nameof(volume));
        _senha = senha ?? throw new ArgumentNullException(nameof(senha));
        _area = area ?? throw new ArgumentNullException(nameof(area));
        _versao = versao ?? throw new ArgumentNullException(nameof(versao));
        _caminho = caminho ?? throw new ArgumentNullException(nameof(caminho));
    }

    public async Task<ResultadoRestauracao> RestaurarAsync(CancellationToken cancellationToken = default)
    {
        var senha = _senha.Obter();
        if (string.IsNullOrEmpty(senha))
            return ResultadoRestauracao.Falha("Senha do espelho ausente. O espelho não foi lido e nenhum arquivo foi apagado.");

        var local = _area.CriarArquivo();
        try
        {
            var copia = await _volume.CopiarParaLocalAsync(_volume.NomeArquivo, local, cancellationToken);
            if (!copia.Sucesso)
                return ResultadoRestauracao.Falha(string.IsNullOrWhiteSpace(copia.Mensagem)
                    ? "Não foi possível ler o espelho. Nenhum arquivo foi apagado."
                    : copia.Mensagem);

            using var snapshot = await LerEspelho.AbrirAsync(local, senha, _versao, cancellationToken);
            await NormalizacaoFotoEspelho.AplicarAsync(snapshot.CaminhoSqlite, cancellationToken);
            return TrocaArquivoBanco.Substituir(_caminho.Valor, snapshot.CaminhoSqlite);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (FalhaEspelhoException ex)
        {
            return ResultadoRestauracao.Falha(ex.Message);
        }
        catch (Exception)
        {
            return ResultadoRestauracao.Falha("A restauração não concluiu. Nenhum arquivo foi apagado e nenhum dado foi semeado.");
        }
        finally
        {
            _area.Descartar(local);
        }
    }
}
