namespace SISLAB.SharedKernel.Messaging;

/// <summary>
/// Base para queries paginadas do read-side. Fornece Page/PageSize e os limites
/// <see cref="FirstResult"/>/<see cref="LastResult"/> (1-based) usados na paginação por
/// <c>ROW_NUMBER()</c> no PostgreSQL:
/// <c>WHERE row_number BETWEEN @FirstResult AND @LastResult</c>.
/// </summary>
/// <typeparam name="TResult">Tipo do resultado (tipicamente <see cref="PagedResult{TItem}"/>).</typeparam>
public abstract record PagedQuery<TResult> : IQuery<TResult>
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly int _page = 1;
    private readonly int _pageSize = DefaultPageSize;

    /// <summary>Página solicitada (1-based). Valores menores que 1 são normalizados para 1.</summary>
    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    /// <summary>Tamanho da página. Limitado entre 1 e 200.</summary>
    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = Math.Clamp(value, 1, MaxPageSize);
    }

    /// <summary>Primeiro número de linha (1-based) da página — para <c>ROW_NUMBER BETWEEN</c>.</summary>
    public int FirstResult => ((Page - 1) * PageSize) + 1;

    /// <summary>Último número de linha (1-based) da página — para <c>ROW_NUMBER BETWEEN</c>.</summary>
    public int LastResult => Page * PageSize;
}
