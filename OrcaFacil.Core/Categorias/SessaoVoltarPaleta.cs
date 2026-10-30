namespace OrcaFacil.Core.Categorias;

/// <summary>
/// Com a paleta aberta, o voltar fecha só ela. Sem registro, o voltar segue para a página.
/// O fechamento é o cancelamento do diálogo: a cor arrastada não entra na categoria.
/// </summary>
public sealed class SessaoVoltarPaleta
{
    private readonly object _trava = new();
    private Action? _fechar;

    public bool Aberta
    {
        get
        {
            lock (_trava)
                return _fechar is not null;
        }
    }

    public void Abrir(Action fechar)
    {
        ArgumentNullException.ThrowIfNull(fechar);
        lock (_trava)
            _fechar = fechar;
    }

    public void Fechar(Action fechar)
    {
        ArgumentNullException.ThrowIfNull(fechar);
        lock (_trava)
        {
            if (ReferenceEquals(_fechar, fechar))
                _fechar = null;
        }
    }

    /// <summary>
    /// true quando a paleta estava aberta e o voltar foi entregue a ela.
    /// O registro continua até <see cref="Fechar"/>, para um segundo voltar
    /// não passar para a página enquanto o diálogo ainda não soltou.
    /// </summary>
    public bool TentarConsumir()
    {
        Action? fechar;
        lock (_trava)
            fechar = _fechar;

        if (fechar is null)
            return false;

        fechar();
        return true;
    }
}
