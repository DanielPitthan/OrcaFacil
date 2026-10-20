namespace OrcaFacil.Core.Cesta;

/// <summary>
/// Fecha o cadastro aberto pelo Cadastrar da prévia.
/// No MudBlazor 9 o clique em Salvar pode correr antes do bind do MudTextField:
/// o texto do campo é o nome editado; o modelo ainda pode ter o nome pré-preenchido.
/// </summary>
public static class ConfirmacaoCadastroProduto
{
    public const string MensagemNome = "Informe o nome do produto.";

    public const string MensagemCategoria = "Selecione a categoria do produto.";

    public const string MensagemNomeLongo = "O nome pode ter no máximo 80 caracteres.";

    public const string MensagemFalha = "Não foi possível cadastrar o produto.";

    public static string NomeParaSalvar(string? nomeModelo, string? textoCampo)
    {
        if (textoCampo is not null)
            return textoCampo.Trim();

        return (nomeModelo ?? string.Empty).Trim();
    }

    public static string? Validar(string? nome, int categoriaId, bool somenteCategoria)
    {
        if (!somenteCategoria)
        {
            var texto = (nome ?? string.Empty).Trim();
            if (texto.Length == 0)
                return MensagemNome;

            if (texto.Length > 80)
                return MensagemNomeLongo;
        }

        if (categoriaId <= 0)
            return MensagemCategoria;

        return null;
    }
}
