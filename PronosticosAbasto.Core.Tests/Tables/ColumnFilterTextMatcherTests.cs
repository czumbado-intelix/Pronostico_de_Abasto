using PronosticosAbasto.Core.Tables;

namespace PronosticosAbasto.Core.Tests.Tables;

public class ColumnFilterTextMatcherTests
{
    [Fact]
    public void Exact_search_finds_same_code()
    {
        Assert.True(ColumnFilterTextMatcher.EqualsNormalized("100002822", "100002822"));
    }

    [Fact]
    public void Exact_search_ignores_pasted_whitespace()
    {
        Assert.True(ColumnFilterTextMatcher.EqualsNormalized("100002822", "  100002822\r\n"));
    }

    [Fact]
    public void Exact_search_does_not_match_partial_code()
    {
        Assert.False(ColumnFilterTextMatcher.EqualsNormalized("100002822", "10000282"));
    }

    [Fact]
    public void Contains_search_ignores_spaces_inside_values()
    {
        Assert.True(ColumnFilterTextMatcher.ContainsNormalized("1 440", "1440"));
    }
}
