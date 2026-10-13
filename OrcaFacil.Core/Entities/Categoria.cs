using System.ComponentModel.DataAnnotations;
using OrcaFacil.Core.Enums;

namespace OrcaFacil.Core.Entities;

public class Categoria
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string Nome { get; set; } = string.Empty;

    [Required, MaxLength(4)]
    public string Icone { get; set; } = "🏷️";

    [Required, MaxLength(9)]
    public string CorHex { get; set; } = "#7f7caf";

    public TipoTransacao TipoPadrao { get; set; } = TipoTransacao.Despesa;

    public bool EhPadraoDoSistema { get; set; }

    /// <summary>Quando verdadeiro, a despesa pode ser detalhada em itens que redistribuem o valor nas categorias.</summary>
    public bool PermiteItensFatura { get; set; }

    public bool Ativo { get; set; } = true;

    public ICollection<Transacao> Transacoes { get; set; } = new List<Transacao>();

    public ICollection<Orcamento> Orcamentos { get; set; } = new List<Orcamento>();
}
