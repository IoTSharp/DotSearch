using BenchmarkDotNet.Attributes;
using DotSearch.Hybrid;
using DotSearch.Index;

namespace DotSearch.Benchmarks;

[MemoryDiagnoser]
public class HybridBenchmarks
{
    private SearchHit[] _bm25 = null!;
    private SearchHit[] _vector = null!;

    [GlobalSetup]
    public void Setup()
    {
        _bm25 = CreateHits("doc-", 1_000, scoreStart: 1_000);
        _vector = CreateHits("doc-", 1_000, scoreStart: 500);
    }

    [Benchmark]
    public int Rrf_fuse_two_sources_top20()
        => ReciprocalRankFusion.Fuse(new[] { _bm25, _vector }, topK: 20).Count;

    private static SearchHit[] CreateHits(string prefix, int count, double scoreStart)
    {
        SearchHit[] hits = new SearchHit[count];
        for (int i = 0; i < hits.Length; i++)
        {
            hits[i] = new SearchHit(new DocumentId(prefix + i.ToString("D5")), scoreStart - i);
        }
        return hits;
    }
}
