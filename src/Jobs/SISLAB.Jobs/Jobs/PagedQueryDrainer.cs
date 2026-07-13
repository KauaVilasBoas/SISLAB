using SISLAB.SharedKernel.Messaging;

namespace SISLAB.Jobs.Jobs;

/// <summary>
/// Drains every page of a paginated read query into a single flat list, so an alert job can scan an entire
/// company's at-risk set regardless of page size. The alert jobs process the full result per cycle (a lab's
/// at-risk set is small), so paging exists only to bound each round-trip, not to skip rows.
/// </summary>
/// <remarks>
/// The factory takes a 1-based page number and returns the query for that page; the drainer keeps requesting
/// pages until it has collected <see cref="PagedResult{T}.TotalCount"/> rows (or a short page comes back),
/// guarding against an accidental infinite loop when the total is unexpectedly larger than what is returned.
/// </remarks>
internal static class PagedQueryDrainer
{
    /// <summary>Page size used when draining — the read side clamps this to its own maximum.</summary>
    internal const int PageSize = 200;

    internal static async Task<IReadOnlyList<TItem>> DrainAsync<TItem>(
        Func<int, IQuery<PagedResult<TItem>>> queryForPage,
        Func<IQuery<PagedResult<TItem>>, CancellationToken, Task<PagedResult<TItem>>> send,
        CancellationToken cancellationToken)
    {
        List<TItem> collected = [];
        int page = 1;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PagedResult<TItem> result = await send(queryForPage(page), cancellationToken);
            collected.AddRange(result.Items);

            bool lastPage = result.Items.Count < PageSize || collected.Count >= result.TotalCount;
            if (lastPage)
                return collected;

            page++;
        }
    }
}
