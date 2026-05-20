using DotSearch.Index;

namespace DotSearch.Hybrid;

/// <summary>
/// 混合检索中的一个排序源，例如全文检索、向量检索或业务排序器。
/// </summary>
/// <typeparam name="TRequest">调用方定义的检索请求类型。</typeparam>
public interface IRankedSource<in TRequest>
{
    /// <summary>
    /// 排序源名称，用于诊断与日志。
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 返回该来源自己的前 <paramref name="topK"/> 个排序结果，结果应已按相关性降序排列。
    /// </summary>
    IReadOnlyList<SearchHit> Search(TRequest request, int topK);
}
