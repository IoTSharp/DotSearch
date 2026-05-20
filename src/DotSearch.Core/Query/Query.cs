using System.Collections.Generic;

namespace DotSearch.Query;

/// <summary>
/// 查询节点抽象基类。AST 不可变。
/// </summary>
public abstract class Query
{
}

/// <summary>
/// 单词项查询：匹配指定字段的单个 token。
/// </summary>
public sealed class TermQuery : Query
{
    /// <summary>
    /// 创建词项查询。
    /// </summary>
    /// <param name="field">字段名。</param>
    /// <param name="term">词项文本。</param>
    public TermQuery(string field, string term)
    {
        ArgumentException.ThrowIfNullOrEmpty(field);
        ArgumentException.ThrowIfNullOrEmpty(term);
        Field = field;
        Term = term;
    }

    /// <summary>字段名。</summary>
    public string Field { get; }

    /// <summary>词项文本。</summary>
    public string Term { get; }
}

/// <summary>
/// 布尔 OR 组合（Should 列表，至少匹配一项）。
/// </summary>
public sealed class OrQuery : Query
{
    /// <summary>
    /// 创建 OR 查询。
    /// </summary>
    public OrQuery(IReadOnlyList<Query> clauses)
    {
        ArgumentNullException.ThrowIfNull(clauses);
        Query[] snapshot = new Query[clauses.Count];
        for (int i = 0; i < clauses.Count; i++)
        {
            if (clauses[i] is not { } clause)
            {
                throw new ArgumentException("Clauses cannot contain null.", nameof(clauses));
            }
            snapshot[i] = clause;
        }
        Clauses = Array.AsReadOnly(snapshot);
    }

    /// <summary>子句列表。</summary>
    public IReadOnlyList<Query> Clauses { get; }
}

/// <summary>
/// 布尔 AND 组合（必须全部匹配）。
/// </summary>
public sealed class AndQuery : Query
{
    /// <summary>
    /// 创建 AND 查询。
    /// </summary>
    public AndQuery(IReadOnlyList<Query> clauses)
    {
        ArgumentNullException.ThrowIfNull(clauses);
        Query[] snapshot = new Query[clauses.Count];
        for (int i = 0; i < clauses.Count; i++)
        {
            if (clauses[i] is not { } clause)
            {
                throw new ArgumentException("Clauses cannot contain null.", nameof(clauses));
            }
            snapshot[i] = clause;
        }
        Clauses = Array.AsReadOnly(snapshot);
    }

    /// <summary>子句列表。</summary>
    public IReadOnlyList<Query> Clauses { get; }
}
