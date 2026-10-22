namespace OrcaFacil.Core.Armazenamento.Espelho;

/// <summary>
/// Regra isolada da leitura não confirmada: a senha nova entra na memória e a
/// antiga fica retida até uma leitura funcionar. A senha retida existe só em
/// memória. Enquanto as duas falharem, as duas continuam retidas e cada leitura
/// tenta a nova e, se ela falhar, a antiga uma única vez. Quem abrir o arquivo
/// fica com essa senha; a outra sai. Se foi a antiga que abriu, ela passa a ser
/// a atual. Para mudar a regra, mexa só aqui.
/// </summary>
public static class AdocaoSenhaLeituraNaoConfirmada
{
    public static void Aplicar(IDefinicaoSenhaEspelho definicao, string senhaNova)
    {
        ArgumentNullException.ThrowIfNull(definicao);
        ArgumentException.ThrowIfNullOrWhiteSpace(senhaNova);

        if (definicao is ProvedorSenhaEspelhoMemoria memoria)
        {
            memoria.DefinirRetendoAnterior(senhaNova, senhaNova);
            return;
        }

        definicao.Definir(senhaNova, senhaNova);
    }

    public static void AoLeituraFuncionar(IProvedorSenhaEspelho provedor, string senhaQueAbriu)
    {
        ArgumentNullException.ThrowIfNull(provedor);
        if (provedor is ISenhaEspelhoRetida retida)
            retida.ConfirmarLeitura(senhaQueAbriu);
    }
}
