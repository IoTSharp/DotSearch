using BenchmarkDotNet.Attributes;
using DotSearch.Index;
using DotSearch.Tokenizers.Unicode;

namespace DotSearch.Benchmarks;

[MemoryDiagnoser]
public class IndexingBenchmarks
{
    private readonly Document[] _documents;

    public IndexingBenchmarks()
    {
        _documents = BenchmarkData.CreateDocuments(1_000);
    }

    [Benchmark]
    public int Index_1000_documents()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        foreach (Document document in _documents)
        {
            index.Index(document);
        }
        return index.DocumentCount;
    }
}
