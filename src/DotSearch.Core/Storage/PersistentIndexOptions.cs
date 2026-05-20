namespace DotSearch.Storage;

/// <summary>
/// 持久化索引选项。
/// </summary>
public sealed class PersistentIndexOptions
{
    /// <summary>
    /// 是否在写入后自动触发后台段合并。
    /// </summary>
    public bool EnableBackgroundMerge { get; init; } = true;

    /// <summary>
    /// 活动段数达到该阈值时触发后台合并。
    /// </summary>
    public int BackgroundMergeSegmentThreshold { get; init; } = 8;
}
