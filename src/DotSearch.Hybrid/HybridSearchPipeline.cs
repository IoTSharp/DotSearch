using DotSearch.Index;

namespace DotSearch.Hybrid;

/// <summary>
/// 调用多个排序源并通过 RRF 融合结果的混合检索管线。
/// </summary>
public sealed class HybridSearchPipeline<TRequest>
{
    private readonly IReadOnlyList<IRankedSource<TRequest>> _sources;
    private readonly int _rrfK;

    /// <summary>
    /// 创建混合检索管线。
    /// </summary>
    /// <param name="sources">参与融合的排序源。</param>
    /// <param name="rrfK">RRF 平滑常数。</param>
    public HybridSearchPipeline(IReadOnlyList<IRankedSource<TRequest>> sources, int rrfK = ReciprocalRankFusion.DefaultK)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            throw new ArgumentException("At least one ranked source is required.", nameof(sources));
        }
        if (rrfK <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rrfK), "RRF k must be positive.");
        }

        IRankedSource<TRequest>[] snapshot = new IRankedSource<TRequest>[sources.Count];
        for (int i = 0; i < sources.Count; i++)
        {
            if (sources[i] is not { } source)
            {
                throw new ArgumentException("Sources cannot contain null.", nameof(sources));
            }
            snapshot[i] = source;
        }

        _sources = Array.AsReadOnly(snapshot);
        _rrfK = rrfK;
    }

    /// <summary>
    /// 参与融合的排序源快照。
    /// </summary>
    public IReadOnlyList<IRankedSource<TRequest>> Sources => _sources;

    /// <summary>
    /// 执行混合检索。
    /// </summary>
    /// <param name="request">调用方请求对象。</param>
    /// <param name="topK">融合后返回数量。</param>
    /// <param name="sourceTopK">每个排序源取回数量；为 0 时使用 <paramref name="topK"/>。</param>
    public IReadOnlyList<SearchHit> Search(TRequest request, int topK, int sourceTopK = 0)
    {
        if (topK <= 0)
        {
            return Array.Empty<SearchHit>();
        }

        int perSourceTopK = sourceTopK > 0 ? sourceTopK : topK;
        SearchHit[][] rankedLists = new SearchHit[_sources.Count][];
        for (int i = 0; i < _sources.Count; i++)
        {
            IReadOnlyList<SearchHit> sourceHits = _sources[i].Search(request, perSourceTopK);
            rankedLists[i] = Snapshot(sourceHits);
        }

        return ReciprocalRankFusion.Fuse(rankedLists, topK, _rrfK);
    }

    private static SearchHit[] Snapshot(IReadOnlyList<SearchHit> hits)
    {
        SearchHit[] snapshot = new SearchHit[hits.Count];
        for (int i = 0; i < hits.Count; i++)
        {
            snapshot[i] = hits[i];
        }
        return snapshot;
    }
}
