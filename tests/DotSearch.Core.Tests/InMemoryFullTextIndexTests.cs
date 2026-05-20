using DotSearch.Index;
using DotSearch.Query;
using DotSearch.Tokenizers.Unicode;
using Xunit;

namespace DotSearch.Core.Tests;

public class InMemoryFullTextIndexTests
{
    [Fact]
    public void Index_then_search_term_returns_hit()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("1")).Set("body", "Hello DotSearch world"));
        index.Index(new Document(new DocumentId("2")).Set("body", "Goodbye DotVector"));

        IReadOnlyList<SearchHit> hits = index.Search(new TermQuery("body", "dotsearch"), topK: 10);

        Assert.Single(hits);
        Assert.Equal("1", hits[0].DocumentId.Value);
        Assert.True(hits[0].Score > 0);
    }

    [Fact]
    public void And_query_intersects_documents()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("a")).Set("body", "alpha beta gamma"));
        index.Index(new Document(new DocumentId("b")).Set("body", "alpha gamma"));
        index.Index(new Document(new DocumentId("c")).Set("body", "beta gamma"));

        AndQuery and = new(new Query.Query[]
        {
            new TermQuery("body", "alpha"),
            new TermQuery("body", "beta"),
        });

        IReadOnlyList<SearchHit> hits = index.Search(and, topK: 10);
        Assert.Single(hits);
        Assert.Equal("a", hits[0].DocumentId.Value);
    }

    [Fact]
    public void Or_query_unions_documents_and_orders_by_score()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("a")).Set("body", "alpha alpha beta"));
        index.Index(new Document(new DocumentId("b")).Set("body", "alpha"));
        index.Index(new Document(new DocumentId("c")).Set("body", "gamma"));

        OrQuery or = new(new Query.Query[]
        {
            new TermQuery("body", "alpha"),
            new TermQuery("body", "beta"),
        });

        IReadOnlyList<SearchHit> hits = index.Search(or, topK: 10);
        Assert.Equal(2, hits.Count);
        Assert.Equal("a", hits[0].DocumentId.Value);
    }

    [Fact]
    public void Delete_removes_document_from_results()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("1")).Set("body", "hello"));
        Assert.True(index.Delete(new DocumentId("1")));
        Assert.Empty(index.Search(new TermQuery("body", "hello"), topK: 10));
        Assert.Equal(0, index.DocumentCount);
    }

    [Fact]
    public void Reindexing_same_id_replaces_previous_content()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("1")).Set("body", "hello"));
        index.Index(new Document(new DocumentId("1")).Set("body", "world"));

        Assert.Empty(index.Search(new TermQuery("body", "hello"), topK: 10));
        Assert.Single(index.Search(new TermQuery("body", "world"), topK: 10));
        Assert.Equal(1, index.DocumentCount);
    }

    [Fact]
    public void Search_respects_field_boundaries()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("1"))
            .Set("title", "alpha")
            .Set("body", "beta"));

        IReadOnlyList<SearchHit> titleHits = index.Search(new TermQuery("title", "alpha"), topK: 10);
        IReadOnlyList<SearchHit> bodyHits = index.Search(new TermQuery("body", "alpha"), topK: 10);

        Assert.Single(titleHits);
        Assert.Empty(bodyHits);
    }

    [Fact]
    public void Search_limits_results_to_top_k()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("a")).Set("body", "alpha"));
        index.Index(new Document(new DocumentId("b")).Set("body", "alpha"));
        index.Index(new Document(new DocumentId("c")).Set("body", "alpha"));

        IReadOnlyList<SearchHit> hits = index.Search(new TermQuery("body", "alpha"), topK: 2);

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public void Search_orders_equal_scores_by_document_id()
    {
        InMemoryFullTextIndex index = new(new UnicodeTokenizer());
        index.Index(new Document(new DocumentId("c")).Set("body", "alpha"));
        index.Index(new Document(new DocumentId("a")).Set("body", "alpha"));
        index.Index(new Document(new DocumentId("b")).Set("body", "alpha"));

        IReadOnlyList<SearchHit> hits = index.Search(new TermQuery("body", "alpha"), topK: 10);

        Assert.Collection(hits,
            hit => Assert.Equal("a", hit.DocumentId.Value),
            hit => Assert.Equal("b", hit.DocumentId.Value),
            hit => Assert.Equal("c", hit.DocumentId.Value));
    }

    [Fact]
    public void Index_rejects_empty_document_id()
    {
        Assert.Throws<ArgumentException>(() => new Document(new DocumentId(string.Empty)));
    }

    [Fact]
    public void Document_rejects_empty_field_name()
    {
        Document document = new(new DocumentId("1"));

        Assert.Throws<ArgumentException>(() => document.Set(string.Empty, "value"));
    }
}
