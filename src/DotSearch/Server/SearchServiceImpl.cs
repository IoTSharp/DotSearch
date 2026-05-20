using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using DotSearch.Grpc;
using DotSearch.Index;
using Grpc.Core;

namespace DotSearch.Server;

/// <summary>
/// gRPC <c>SearchService</c> 实现。
/// </summary>
internal sealed class SearchServiceImpl : SearchService.SearchServiceBase
{
    private readonly IndexRegistry _registry;

    public SearchServiceImpl(IndexRegistry registry)
    {
        _registry = registry;
    }

    public override Task<PingResponse> Ping(PingRequest request, ServerCallContext context)
    {
        string version = typeof(SearchServiceImpl).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
        return Task.FromResult(new PingResponse { Version = version });
    }

    public override Task<CreateIndexResponse> CreateIndex(CreateIndexRequest request, ServerCallContext context)
    {
        if (string.IsNullOrEmpty(request.Name))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "name is required."));
        }
        if (!_registry.TryCreate(request.Name, request.Tokenizer))
        {
            throw new RpcException(new Status(StatusCode.AlreadyExists, $"index '{request.Name}' already exists."));
        }
        return Task.FromResult(new CreateIndexResponse());
    }

    public override Task<DeleteIndexResponse> DeleteIndex(DeleteIndexRequest request, ServerCallContext context)
    {
        _registry.TryDelete(request.Name);
        return Task.FromResult(new DeleteIndexResponse());
    }

    public override Task<ListIndexesResponse> ListIndexes(ListIndexesRequest request, ServerCallContext context)
    {
        ListIndexesResponse response = new();
        foreach (string name in _registry.List())
        {
            response.Names.Add(name);
        }
        return Task.FromResult(response);
    }

    public override Task<UpsertResponse> Upsert(UpsertRequest request, ServerCallContext context)
    {
        IFullTextIndex index = RequireIndex(request.Index);
        int count = 0;
        foreach (DotSearch.Grpc.Document doc in request.Documents)
        {
            DotSearch.Index.Document model = new(new DocumentId(doc.Id));
            foreach (KeyValuePair<string, string> field in doc.Fields)
            {
                model.Set(field.Key, field.Value);
            }
            index.Index(model);
            count++;
        }
        return Task.FromResult(new UpsertResponse { Upserted = count });
    }

    public override Task<DeleteResponse> Delete(DeleteRequest request, ServerCallContext context)
    {
        IFullTextIndex index = RequireIndex(request.Index);
        int deleted = 0;
        foreach (string id in request.Ids)
        {
            if (index.Delete(new DocumentId(id)))
            {
                deleted++;
            }
        }
        return Task.FromResult(new DeleteResponse { Deleted = deleted });
    }

    public override Task<SearchResponse> Search(SearchRequest request, ServerCallContext context)
    {
        IFullTextIndex index = RequireIndex(request.Index);
        if (request.Query is null)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "query is required."));
        }
        DotSearch.Query.Query query = Translate(request.Query);
        IReadOnlyList<SearchHit> hits = index.Search(query, request.TopK <= 0 ? 10 : request.TopK);

        SearchResponse response = new();
        foreach (SearchHit hit in hits)
        {
            response.Hits.Add(new Hit { Id = hit.DocumentId.Value, Score = (float)hit.Score });
        }
        return Task.FromResult(response);
    }

    private IFullTextIndex RequireIndex(string name)
    {
        IFullTextIndex? index = _registry.Get(name);
        if (index is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"index '{name}' not found."));
        }
        return index;
    }

    private static DotSearch.Query.Query Translate(DotSearch.Grpc.Query query)
    {
        if (query.Term is { } term)
        {
            return new DotSearch.Query.TermQuery(term.Field, term.Term);
        }
        if (query.Boolean is { } boolean)
        {
            List<DotSearch.Query.Query> clauses = new(boolean.Clauses.Count);
            foreach (DotSearch.Grpc.Query clause in boolean.Clauses)
            {
                clauses.Add(Translate(clause));
            }
            return boolean.Op switch
            {
                BooleanOp.Or => new DotSearch.Query.OrQuery(clauses),
                _ => new DotSearch.Query.AndQuery(clauses),
            };
        }
        throw new RpcException(new Status(StatusCode.InvalidArgument, "query must specify a clause."));
    }
}
