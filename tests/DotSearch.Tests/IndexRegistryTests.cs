using DotSearch.Grpc;
using DotSearch.Index;
using DotSearch.Query;
using DotSearch.Server;
using Xunit;
using IndexDocument = DotSearch.Index.Document;

namespace DotSearch.Tests;

public sealed class IndexRegistryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dotsearch-server-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Registry_reopens_existing_persistent_indexes()
    {
        IndexRegistry registry = new(_directory);
        Assert.True(registry.TryCreate("main", TokenizerKind.Unicode));
        IFullTextIndex? index = registry.Get("main");
        Assert.NotNull(index);
        index.Index(new IndexDocument(new DocumentId("1")).Set("body", "hello persisted server"));

        IndexRegistry reopened = new(_directory);
        IFullTextIndex? reopenedIndex = reopened.Get("main");
        Assert.NotNull(reopenedIndex);
        IReadOnlyList<SearchHit> hits = reopenedIndex.Search(new TermQuery("body", "persisted"), topK: 10);

        Assert.Single(hits);
        Assert.Equal("1", hits[0].DocumentId.Value);
    }

    [Fact]
    public void Delete_removes_persistent_index_directory()
    {
        IndexRegistry registry = new(_directory);
        Assert.True(registry.TryCreate("main", TokenizerKind.Unicode));

        Assert.True(registry.TryDelete("main"));

        Assert.False(Directory.Exists(Path.Combine(_directory, "main.dsx")));
        IndexRegistry reopened = new(_directory);
        Assert.Null(reopened.Get("main"));
    }

    [Fact]
    public void Registry_rejects_unsafe_index_names()
    {
        IndexRegistry registry = new(_directory);

        Assert.False(registry.TryCreate("../escape", TokenizerKind.Unicode));
        Assert.False(registry.TryCreate("nested/name", TokenizerKind.Unicode));
        Assert.Null(registry.Get("../escape"));
        Assert.False(Directory.Exists(Path.Combine(_directory, "..", "escape.dsx")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
