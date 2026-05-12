using DotSearch.Hybrid;
using DotSearch.Index;
using Xunit;

namespace DotSearch.Hybrid.Tests;

public class ReciprocalRankFusionTests
{
    [Fact]
    public void Documents_present_in_multiple_lists_outrank_singletons()
    {
        SearchHit[] listA =
        {
            new(new DocumentId("a"), 10),
            new(new DocumentId("b"), 9),
            new(new DocumentId("c"), 8),
        };
        SearchHit[] listB =
        {
            new(new DocumentId("b"), 5),
            new(new DocumentId("d"), 4),
            new(new DocumentId("a"), 3),
        };

        IReadOnlyList<SearchHit> fused = ReciprocalRankFusion.Fuse(new[] { listA, listB }, topK: 4);

        // 同时出现在两个列表的文档（a、b）应排在仅出现在单一列表的文档（c、d）前面。
        Assert.Equal(4, fused.Count);
        string[] top2 = { fused[0].DocumentId.Value, fused[1].DocumentId.Value };
        Assert.Contains("a", top2);
        Assert.Contains("b", top2);
    }

    [Fact]
    public void Empty_input_returns_empty()
    {
        Assert.Empty(ReciprocalRankFusion.Fuse(Array.Empty<IReadOnlyList<SearchHit>>(), topK: 5));
    }
}
