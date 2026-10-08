namespace OrcaFacil.Core.Enums;

[Flags]
public enum TipoLimpezaDados
{
    Nenhum = 0,

    /// <summary>Lançamentos e os fechamentos de período associados.</summary>
    Lancamentos = 1,

    Orcamentos = 2,

    Metas = 4,

    /// <summary>Zera a aplicação: todas as tabelas, inclusive categorias, perfil e logs.</summary>
    Tudo = 8
}
