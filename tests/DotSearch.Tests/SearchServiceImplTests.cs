using DotSearch.Grpc;
using DotSearch.Server;
using Xunit;

namespace DotSearch.Tests;

public sealed class SearchServiceImplTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "dotsearch-service-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Service_supports_index_lifecycle_and_document_search()
    {
        IndexRegistry registry = new(_directory);
        SearchServiceImpl service = new(registry);

        await service.CreateIndex(new CreateIndexRequest
        {
            Name = "main",
            Tokenizer = TokenizerKind.Unicode,
        }, TestServerCallContext.Create());

        ListIndexesResponse list = await service.ListIndexes(new ListIndexesRequest(), TestServerCallContext.Create());
        Assert.Contains("main", list.Names);

        UpsertResponse upsert = await service.Upsert(new UpsertRequest
        {
            Index = "main",
            Documents =
            {
                new DotSearch.Grpc.Document
                {
                    Id = "1",
                    Fields = { ["body"] = "hello dotsearch service" },
                },
            },
        }, TestServerCallContext.Create());
        Assert.Equal(1, upsert.Upserted);

        SearchResponse search = await service.Search(new SearchRequest
        {
            Index = "main",
            Query = new DotSearch.Grpc.Query
            {
                Term = new TermClause { Field = "body", Term = "dotsearch" },
            },
            TopK = 10,
        }, TestServerCallContext.Create());
        Assert.Single(search.Hits);
        Assert.Equal("1", search.Hits[0].Id);

        DeleteResponse delete = await service.Delete(new DeleteRequest
        {
            Index = "main",
            Ids = { "1" },
        }, TestServerCallContext.Create());
        Assert.Equal(1, delete.Deleted);

        SearchResponse afterDelete = await service.Search(new SearchRequest
        {
            Index = "main",
            Query = new DotSearch.Grpc.Query
            {
                Term = new TermClause { Field = "body", Term = "dotsearch" },
            },
            TopK = 10,
        }, TestServerCallContext.Create());
        Assert.Empty(afterDelete.Hits);

        await service.DeleteIndex(new DeleteIndexRequest { Name = "main" }, TestServerCallContext.Create());
        Assert.Empty(registry.List());
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
