using BenchmarkDotNet.Attributes;
using DotSearch.Index;
using DotSearch.Query;
using DotSearch.Tokenizers.Unicode;

namespace DotSearch.Benchmarks;

[MemoryDiagnoser]
public class QueryBenchmarks
{
    private InMemoryFullTextIndex _index = null!;
    private TermQuery _term = null!;
    private AndQuery _and = null!;

    [GlobalSetup]
    public void Setup()
    {
        _index = new InMemoryFullTextIndex(new UnicodeTokenizer());
        foreach (Document document in BenchmarkData.CreateDocuments(5_000))
        {
            _index.Index(document);
        }

        _term = new TermQuery("body", "dotsearch");
        _and = new AndQuery(new Query.Query[]
        {
            new TermQuery("body", "dotsearch"),
            new TermQuery("body", "search"),
        });
    }

    [Benchmark]
    public int Term_query_top10() => _index.Search(_term, topK: 10).Count;

    [Benchmark]
    public int And_query_top10() => _index.Search(_and, topK: 10).Count;
}
