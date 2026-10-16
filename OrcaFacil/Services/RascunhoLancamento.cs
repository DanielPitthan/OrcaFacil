using OrcaFacil.Core.Dtos;
using OrcaFacil.Core.Lancamentos;

namespace OrcaFacil.Services;

/// <summary>Leva a cópia de uma fatura para a tela de novo lançamento, sem persistir.</summary>
public sealed class RascunhoLancamento
{
    private TransacaoFormModel? _pendente;

    public void GuardarCopia(TransacaoFormModel modelo) => _pendente = FaturaCartaoEditor.ClonarComoNova(modelo);

    public TransacaoFormModel? Consumir()
    {
        var atual = _pendente;
        _pendente = null;
        return atual;
    }
}
