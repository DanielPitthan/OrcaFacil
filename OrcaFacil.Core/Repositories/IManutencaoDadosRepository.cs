using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Repositories;

public interface IManutencaoDadosRepository
{
    /// <summary>Quantidade de registros por tabela, com nomes amigáveis.</summary>
    Task<Dictionary<string, int>> ContarRegistrosAsync();

    /// <summary>Remove os dados selecionados numa única transação e compacta o arquivo do banco.</summary>
    Task LimparAsync(TipoLimpezaDados tipo);
}
