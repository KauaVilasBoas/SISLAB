using SISLAB.SharedKernel.Messaging;

namespace SISLAB.SharedKernel.Tests;

public sealed class PagingTests
{
    private sealed record SampleQuery : PagedQuery<PagedResult<string>>;

    [Fact]
    public void Normalizes_invalid_page_and_page_size()
    {
        SampleQuery query = new() { Page = 0, PageSize = 0 };

        Assert.Equal(1, query.Page);
        Assert.Equal(1, query.PageSize);
    }

    [Fact]
    public void Clamps_page_size_to_maximum()
    {
        SampleQuery query = new() { PageSize = 10_000 };

        Assert.Equal(200, query.PageSize);
    }

    [Fact]
    public void Computes_row_number_bounds_for_page()
    {
        SampleQuery query = new() { Page = 3, PageSize = 20 };

        Assert.Equal(41, query.FirstResult);
        Assert.Equal(60, query.LastResult);
    }

    [Theory]
    [InlineData(0, 20, 0)]
    [InlineData(45, 20, 3)]
    [InlineData(40, 20, 2)]
    public void Computes_total_pages(int totalCount, int pageSize, int expectedTotalPages)
    {
        PagedResult<string> result = new(Array.Empty<string>(), totalCount, page: 1, pageSize);

        Assert.Equal(expectedTotalPages, result.TotalPages);
    }
}
