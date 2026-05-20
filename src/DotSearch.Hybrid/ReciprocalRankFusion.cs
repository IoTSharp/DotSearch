using System.Collections.Generic;
using DotSearch.Index;

namespace DotSearch.Hybrid;

/// <summary>
/// Reciprocal Rank Fusion（RRF）混合检索融合器。
/// </summary>
/// <remarks>
/// 公式：<c>RRF(d) = Σ 1 / (k + rank_i(d))</c>，其中 <c>rank_i(d)</c> 是文档 <c>d</c> 在
/// 第 <c>i</c> 个候选列表里的排名（从 1 开始），缺席的列表不计入。
/// 该实现独立于具体来源，可同时融合全文检索、向量检索或任何其他排序结果。
/// </remarks>
public static class ReciprocalRankFusion
{
    /// <summary>
    /// 推荐的 <c>k</c> 默认值。
    /// </summary>
    public const int DefaultK = 60;

    /// <summary>
    /// 融合多个已排序的候选列表，返回综合得分前 <paramref name="topK"/> 的命中。
    /// </summary>
    /// <param name="rankedLists">每个内层列表已按相关性降序排列。</param>
    /// <param name="topK">返回前 K 条。</param>
    /// <param name="k">RRF 平滑常数；默认 <see cref="DefaultK"/>。</param>
    public static IReadOnlyList<SearchHit> Fuse(
        IReadOnlyList<IReadOnlyList<SearchHit>> rankedLists,
        int topK,
        int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(rankedLists);
        if (k <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(k), "k must be positive.");
        }
        if (topK <= 0 || rankedLists.Count == 0)
        {
            return Array.Empty<SearchHit>();
        }

        Dictionary<string, double> scores = new(StringComparer.Ordinal);
        Dictionary<string, DocumentId> ids = new(StringComparer.Ordinal);

        foreach (IReadOnlyList<SearchHit> list in rankedLists)
        {
            for (int rank = 0; rank < list.Count; rank++)
            {
                SearchHit hit = list[rank];
                string key = hit.DocumentId.Value;
                double contribution = 1.0 / (k + (rank + 1));
                scores[key] = scores.TryGetValue(key, out double existing) ? existing + contribution : contribution;
                ids[key] = hit.DocumentId;
            }
        }

        List<SearchHit> result = new(scores.Count);
        foreach (KeyValuePair<string, double> kv in scores)
        {
            result.Add(new SearchHit(ids[kv.Key], kv.Value));
        }
        result.Sort(static (a, b) =>
        {
            int scoreCompare = b.Score.CompareTo(a.Score);
            return scoreCompare != 0
                ? scoreCompare
                : string.CompareOrdinal(a.DocumentId.Value, b.DocumentId.Value);
        });
        if (result.Count > topK)
        {
            result.RemoveRange(topK, result.Count - topK);
        }
        return result;
    }
}
