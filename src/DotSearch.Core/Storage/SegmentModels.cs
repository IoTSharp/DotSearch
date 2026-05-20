using DotSearch.Index;

namespace DotSearch.Storage;

internal sealed class SegmentDocument
{
    private readonly Dictionary<string, string> _fields = new(StringComparer.Ordinal);

    public SegmentDocument(int localId, DocumentId id)
    {
        LocalId = localId;
        Id = id;
    }

    public int LocalId { get; }

    public DocumentId Id { get; }

    public Dictionary<string, string> Fields => _fields;
}

internal sealed class SegmentPostingList
{
    public SegmentPostingList(string field, string term, Dictionary<int, int> postings)
    {
        Field = field;
        Term = term;
        Postings = postings;
    }

    public string Field { get; }

    public string Term { get; }

    public Dictionary<int, int> Postings { get; }
}

internal sealed class SegmentData
{
    public SegmentData(long id)
    {
        Id = id;
    }

    public long Id { get; }

    public List<SegmentDocument> Documents { get; } = new();

    public List<SegmentPostingList> PostingLists { get; } = new();

    public Dictionary<string, Dictionary<int, int>> FieldLengths { get; } = new(StringComparer.Ordinal);
}

internal sealed class SegmentReader
{
    private readonly Dictionary<string, Dictionary<string, Dictionary<int, int>>> _postings = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<int, int>> _fieldLengths = new(StringComparer.Ordinal);
    private readonly Dictionary<int, DocumentId> _documents = new();
    private readonly Dictionary<string, SegmentDocument> _documentSnapshots = new(StringComparer.Ordinal);

    public SegmentReader(SegmentData data, string path, long sizeBytes)
    {
        Id = data.Id;
        Path = path;
        SizeBytes = sizeBytes;

        foreach (SegmentDocument document in data.Documents)
        {
            _documents[document.LocalId] = document.Id;
            _documentSnapshots[document.Id.Value] = document;
        }

        foreach (SegmentPostingList postingList in data.PostingLists)
        {
            Dictionary<string, Dictionary<int, int>> terms = GetOrCreate(_postings, postingList.Field);
            terms[postingList.Term] = postingList.Postings;
        }

        foreach (KeyValuePair<string, Dictionary<int, int>> fieldLengths in data.FieldLengths)
        {
            _fieldLengths[fieldLengths.Key] = fieldLengths.Value;
        }
    }

    public long Id { get; }

    public string Path { get; }

    public long SizeBytes { get; }

    public int DocumentCount => _documents.Count;

    public IReadOnlyDictionary<int, DocumentId> Documents => _documents;

    public IReadOnlyDictionary<string, SegmentDocument> DocumentSnapshots => _documentSnapshots;

    public bool TryGetPostings(string field, string term, out Dictionary<int, int> postings)
    {
        postings = null!;
        return _postings.TryGetValue(field, out Dictionary<string, Dictionary<int, int>>? terms)
            && terms.TryGetValue(term, out postings!);
    }

    public bool TryGetFieldLengths(string field, out Dictionary<int, int> lengths)
    {
        return _fieldLengths.TryGetValue(field, out lengths!);
    }

    private static Dictionary<TKey, TValue> GetOrCreate<TKey, TValue>(Dictionary<string, Dictionary<TKey, TValue>> outer, string key)
        where TKey : notnull
    {
        if (!outer.TryGetValue(key, out Dictionary<TKey, TValue>? inner))
        {
            inner = new Dictionary<TKey, TValue>();
            outer[key] = inner;
        }
        return inner;
    }
}
