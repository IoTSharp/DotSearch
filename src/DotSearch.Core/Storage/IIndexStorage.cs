namespace DotSearch.Storage;

/// <summary>
/// 索引持久化存储抽象。v0.2 起接入段格式（segment + manifest）。
/// </summary>
/// <remarks>
/// v0.1 的 <see cref="Index.InMemoryFullTextIndex"/> 不依赖该接口；
/// 留作占位以便后续把段读写、manifest 维护、合并策略统一接入。
/// </remarks>
public interface IIndexStorage
{
    /// <summary>
    /// 数据库目录的绝对路径。
    /// </summary>
    string Directory { get; }
}
