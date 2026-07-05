namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Resultado paginado de uma <see cref="PagedQuery{TResult}"/>.
/// <see cref="TotalCount"/> vem do <c>COUNT(*) OVER()</c> da própria query (uma ida ao banco).
/// </summary>
/// <typeparam name="TItem">Tipo de cada item da página.</typeparam>
public sealed class PagedResult<TItem>
{
    public PagedResult(IReadOnlyList<TItem> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>Itens da página atual.</summary>
    public IReadOnlyList<TItem> Items { get; }

    /// <summary>Total de registros considerando todos os filtros (não só a página).</summary>
    public int TotalCount { get; }

    /// <summary>Página atual (1-based).</summary>
    public int Page { get; }

    /// <summary>Tamanho da página.</summary>
    public int PageSize { get; }

    /// <summary>Total de páginas, derivado de <see cref="TotalCount"/> e <see cref="PageSize"/>.</summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);

    /// <summary>Cria um resultado vazio para a página informada.</summary>
    public static PagedResult<TItem> Empty(int page, int pageSize)
        => new(Array.Empty<TItem>(), totalCount: 0, page, pageSize);
}
