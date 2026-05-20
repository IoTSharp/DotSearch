using System.Collections.Generic;
using DotSearch.Scoring;
using DotSearch.Tokenization;

namespace DotSearch.Index;

/// <summary>
/// 单段、内存常驻的倒排索引，作为 v0.1 MVP。
/// </summary>
/// <remarks>
/// <para>实现仅满足以下能力：</para>
/// <list type="bullet">
///   <item>按 (field, term) 维护 posting list（doc -> tf）。</item>
///   <item>统计每个 (doc, field) 的长度，用于 BM25 长度归一化。</item>
///   <item>支持 <see cref="Query.TermQuery"/> / <see cref="Query.AndQuery"/> / <see cref="Query.OrQuery"/>。</item>
///   <item>删除采用 tombstone：保留 docId，但在结果中跳过。</item>
/// </list>
/// <para>并发安全性：内部使用 <see cref="System.Threading.Lock"/> 串行化写入与查询，简单但够 MVP 用。
/// 高并发与持久化在 v0.2 引入段格式后再做。</para>
/// </remarks>
public sealed class InMemoryFullTextIndex : IFullTextIndex
{
    private readonly System.Threading.Lock _lock = new();
    private readonly ITokenizer _tokenizer;
    private readonly Bm25Parameters _bm25;

    // 文档表：docId -> 外部主键；删除时置 null。
    private readonly List<DocumentId?> _documents = new();
    private readonly Dictionary<string, int> _docIdLookup = new(StringComparer.Ordinal);

    // (field, term) -> (docId -> tf)
    private readonly Dictionary<string, Dictionary<string, Dictionary<int, int>>> _postings = new(StringComparer.Ordinal);

    // (field, docId) -> length
    private readonly Dictionary<string, Dictionary<int, int>> _fieldLengths = new(StringComparer.Ordinal);

    private int _liveCount;

    /// <summary>
    /// 创建内存索引。
    /// </summary>
    /// <param name="tokenizer">分词器。</param>
    /// <param name="bm25">BM25 参数；默认 <see cref="Bm25Parameters.Default"/>。</param>
    public InMemoryFullTextIndex(ITokenizer tokenizer, Bm25Parameters? bm25 = null)
    {
        ArgumentNullException.ThrowIfNull(tokenizer);
        _tokenizer = tokenizer;
        _bm25 = bm25 ?? Bm25Parameters.Default;
    }

    /// <inheritdoc />
    public int DocumentCount
    {
        get
        {
            lock (_lock)
            {
                return _liveCount;
            }
        }
    }

    /// <inheritdoc />
    public void Index(Document document)
    {
        ArgumentNullException.ThrowIfNull(document);

        lock (_lock)
        {
            // 已存在则先逻辑删除。
            if (_docIdLookup.TryGetValue(document.Id.Value, out int existing))
            {
                RemoveFromPostings(existing);
                _documents[existing] = null;
                _liveCount--;
            }

            int docId = _documents.Count;
            _documents.Add(document.Id);
            _docIdLookup[document.Id.Value] = docId;
            _liveCount++;

            CollectingTokenSink sink = new();
            foreach (KeyValuePair<string, string> field in document.Fields)
            {
                sink.Clear();
                _tokenizer.Tokenize(field.Value.AsSpan(), sink);

                Dictionary<string, Dictionary<int, int>> termsForField = GetOrCreate(_postings, field.Key);
                int length = 0;
                foreach (Token token in sink.Tokens)
                {
                    length++;
                    Dictionary<int, int> docs = GetOrCreate(termsForField, token.Text);
                    docs[docId] = docs.TryGetValue(docId, out int tf) ? tf + 1 : 1;
                }

                Dictionary<int, int> lengths = GetOrCreate(_fieldLengths, field.Key);
                lengths[docId] = length;
            }
        }
    }

    /// <inheritdoc />
    public bool Delete(DocumentId id)
    {
        lock (_lock)
        {
            if (!_docIdLookup.TryGetValue(id.Value, out int docId) || _documents[docId] is null)
            {
                return false;
            }

            RemoveFromPostings(docId);
            _documents[docId] = null;
            _docIdLookup.Remove(id.Value);
            _liveCount--;
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SearchHit> Search(Query.Query query, int topK)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (topK <= 0)
        {
            return Array.Empty<SearchHit>();
        }

        lock (_lock)
        {
            Dictionary<int, double> scores = new();
            Score(query, scores);

            if (scores.Count == 0)
            {
                return Array.Empty<SearchHit>();
            }

            List<SearchHit> hits = new(scores.Count);
            foreach (KeyValuePair<int, double> kv in scores)
            {
                DocumentId? id = _documents[kv.Key];
                if (id is { } docId)
                {
                    hits.Add(new SearchHit(docId, kv.Value));
                }
            }

            hits.Sort(static (a, b) =>
            {
                int scoreCompare = b.Score.CompareTo(a.Score);
                return scoreCompare != 0
                    ? scoreCompare
                    : string.CompareOrdinal(a.DocumentId.Value, b.DocumentId.Value);
            });
            if (hits.Count > topK)
            {
                hits.RemoveRange(topK, hits.Count - topK);
            }
            return hits;
        }
    }

    private void Score(Query.Query query, Dictionary<int, double> scores)
    {
        switch (query)
        {
            case Query.TermQuery term:
                ScoreTerm(term, scores);
                break;
            case Query.OrQuery or:
                foreach (Query.Query clause in or.Clauses)
                {
                    Score(clause, scores);
                }
                break;
            case Query.AndQuery and:
                ScoreAnd(and, scores);
                break;
            default:
                throw new NotSupportedException($"Unsupported query node: {query.GetType().Name}");
        }
    }

    private void ScoreTerm(Query.TermQuery term, Dictionary<int, double> scores)
    {
        if (!_postings.TryGetValue(term.Field, out Dictionary<string, Dictionary<int, int>>? termsForField))
        {
            return;
        }
        if (!termsForField.TryGetValue(term.Term, out Dictionary<int, int>? docs))
        {
            return;
        }
        if (!_fieldLengths.TryGetValue(term.Field, out Dictionary<int, int>? lengths))
        {
            return;
        }

        double avg = AverageFieldLength(lengths);
        int n = _liveCount;
        int df = docs.Count;

        foreach (KeyValuePair<int, int> doc in docs)
        {
            if (_documents[doc.Key] is null)
            {
                continue;
            }
            int dl = lengths.TryGetValue(doc.Key, out int len) ? len : 0;
            double s = Bm25.Score(doc.Value, dl, avg, n, df, _bm25);
            scores[doc.Key] = scores.TryGetValue(doc.Key, out double existing) ? existing + s : s;
        }
    }

    private void ScoreAnd(Query.AndQuery and, Dictionary<int, double> scores)
    {
        if (and.Clauses.Count == 0)
        {
            return;
        }

        Dictionary<int, double> first = new();
        Score(and.Clauses[0], first);

        for (int i = 1; i < and.Clauses.Count; i++)
        {
            Dictionary<int, double> next = new();
            Score(and.Clauses[i], next);

            Dictionary<int, double> merged = new();
            foreach (KeyValuePair<int, double> kv in first)
            {
                if (next.TryGetValue(kv.Key, out double s))
                {
                    merged[kv.Key] = kv.Value + s;
                }
            }
            first = merged;
            if (first.Count == 0)
            {
                return;
            }
        }

        foreach (KeyValuePair<int, double> kv in first)
        {
            scores[kv.Key] = scores.TryGetValue(kv.Key, out double existing) ? existing + kv.Value : kv.Value;
        }
    }

    private static double AverageFieldLength(Dictionary<int, int> lengths)
    {
        if (lengths.Count == 0)
        {
            return 0.0;
        }
        long total = 0;
        foreach (int v in lengths.Values)
        {
            total += v;
        }
        return (double)total / lengths.Count;
    }

    private void RemoveFromPostings(int docId)
    {
        foreach (Dictionary<string, Dictionary<int, int>> termsForField in _postings.Values)
        {
            foreach (Dictionary<int, int> docs in termsForField.Values)
            {
                docs.Remove(docId);
            }
        }
        foreach (Dictionary<int, int> lengths in _fieldLengths.Values)
        {
            lengths.Remove(docId);
        }
    }

    private static Dictionary<TKey, TValue> GetOrCreate<TKey, TValue>(Dictionary<string, Dictionary<TKey, TValue>> outer, string key)
        where TKey : notnull
        where TValue : new()
    {
        if (!outer.TryGetValue(key, out Dictionary<TKey, TValue>? inner))
        {
            inner = new Dictionary<TKey, TValue>();
            outer[key] = inner;
        }
        return inner;
    }

    private static Dictionary<int, int> GetOrCreate(Dictionary<string, Dictionary<int, int>> outer, string key)
    {
        if (!outer.TryGetValue(key, out Dictionary<int, int>? inner))
        {
            inner = new Dictionary<int, int>();
            outer[key] = inner;
        }
        return inner;
    }
}
