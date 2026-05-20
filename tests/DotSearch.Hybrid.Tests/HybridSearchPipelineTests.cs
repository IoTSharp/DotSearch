using DotSearch.Index;
using DotSearch.Query;
using DotSearch.Tokenization;
using Xunit;

namespace DotSearch.Hybrid.Tests;

public class HybridSearchPipelineTests
{
    [Fact]
    public void Pipeline_fuses_fulltext_and_external_ranked_source()
    {
        InMemoryFullTextIndex index = new(new WhitespaceTokenizer());
        index.Index(new Document(new DocumentId("bm25-only")).Set("body", "alpha alpha search"));
        index.Index(new Document(new DocumentId("shared")).Set("body", "alpha vector"));
        index.Index(new Document(new DocumentId("vector-only")).Set("body", "semantic"));

        HybridRequest request = new("alpha");
        FullTextRankedSource<HybridRequest> fullText = new(
            index,
            static req => new TermQuery("body", req.Text));
        DelegateRankedSource<HybridRequest> vector = new(
            "vector",
            static (_, _) =>
            [
                new SearchHit(new DocumentId("shared"), 0.98),
                new SearchHit(new DocumentId("vector-only"), 0.97),
            ]);

        HybridSearchPipeline<HybridRequest> pipeline = new(new IRankedSource<HybridRequest>[] { fullText, vector });

        IReadOnlyList<SearchHit> hits = pipeline.Search(request, topK: 3, sourceTopK: 10);

        Assert.Equal("shared", hits[0].DocumentId.Value);
        Assert.Contains(hits, hit => hit.DocumentId.Value == "bm25-only");
        Assert.Contains(hits, hit => hit.DocumentId.Value == "vector-only");
    }

    [Fact]
    public void Pipeline_snapshots_sources()
    {
        IRankedSource<HybridRequest>[] sources =
        [
            new DelegateRankedSource<HybridRequest>("a", static (_, _) => Array.Empty<SearchHit>()),
        ];

        HybridSearchPipeline<HybridRequest> pipeline = new(sources);
        sources[0] = new DelegateRankedSource<HybridRequest>("b", static (_, _) => Array.Empty<SearchHit>());

        Assert.Equal("a", pipeline.Sources[0].Name);
    }

    [Fact]
    public void Pipeline_rejects_empty_sources()
    {
        Assert.Throws<ArgumentException>(() => new HybridSearchPipeline<HybridRequest>(Array.Empty<IRankedSource<HybridRequest>>()));
    }

    private sealed record HybridRequest(string Text);

    private sealed class WhitespaceTokenizer : ITokenizer
    {
        public void Tokenize(ReadOnlySpan<char> text, ITokenSink sink)
        {
            int i = 0;
            while (i < text.Length)
            {
                while (i < text.Length && char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                int start = i;
                while (i < text.Length && !char.IsWhiteSpace(text[i]))
                {
                    i++;
                }

                if (i > start)
                {
                    sink.Emit(text[start..i], start, i, 1);
                }
            }
        }
    }
}
