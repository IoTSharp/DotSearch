using DotSearch.Query;
using Xunit;

namespace DotSearch.Core.Tests;

public class QueryTests
{
    [Fact]
    public void Or_query_snapshots_clauses()
    {
        Query.Query[] clauses =
        [
            new TermQuery("body", "alpha"),
        ];

        OrQuery query = new(clauses);
        clauses[0] = new TermQuery("body", "beta");

        TermQuery term = Assert.IsType<TermQuery>(query.Clauses[0]);
        Assert.Equal("alpha", term.Term);
    }

    [Fact]
    public void And_query_rejects_null_clause()
    {
        Query.Query?[] clauses =
        [
            new TermQuery("body", "alpha"),
            null,
        ];

        Assert.Throws<ArgumentException>(() => new AndQuery(clauses!));
    }
}
