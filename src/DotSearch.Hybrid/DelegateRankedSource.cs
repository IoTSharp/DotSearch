using DotSearch.Index;

namespace DotSearch.Hybrid;

/// <summary>
/// 用委托把任意外部排序器适配为 <see cref="IRankedSource{TRequest}"/>。
/// </summary>
public sealed class DelegateRankedSource<TRequest> : IRankedSource<TRequest>
{
    private readonly Func<TRequest, int, IReadOnlyList<SearchHit>> _search;

    /// <summary>
    /// 创建委托排序源。
    /// </summary>
    public DelegateRankedSource(string name, Func<TRequest, int, IReadOnlyList<SearchHit>> search)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(search);
        Name = name;
        _search = search;
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

        return _search(request, topK);
    }
}
