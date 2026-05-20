using DotSearch.Index;

namespace DotSearch.Hybrid;

/// <summary>
/// 把 <see cref="IFullTextIndex"/> 适配为混合检索排序源。
/// </summary>
public sealed class FullTextRankedSource<TRequest> : IRankedSource<TRequest>
{
    private readonly IFullTextIndex _index;
    private readonly Func<TRequest, Query.Query> _querySelector;

    /// <summary>
    /// 创建全文检索排序源。
    /// </summary>
    public FullTextRankedSource(IFullTextIndex index, Func<TRequest, Query.Query> querySelector, string name = "fulltext")
    {
        ArgumentNullException.ThrowIfNull(index);
        ArgumentNullException.ThrowIfNull(querySelector);
        ArgumentException.ThrowIfNullOrEmpty(name);
        _index = index;
        _querySelector = querySelector;
        Name = name;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<SearchHit> Search(TRequest request, int topK)
    {
        if (topK <= 0)
        {
            return Array.Empty<SearchHit>();
        }

        return _index.Search(_querySelector(request), topK);
    }
}
