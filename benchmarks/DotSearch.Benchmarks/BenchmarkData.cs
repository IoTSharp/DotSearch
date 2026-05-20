using DotSearch.Index;

namespace DotSearch.Benchmarks;

internal static class BenchmarkData
{
    private static readonly string[] Terms =
    [
        "dotsearch",
        "search",
        "index",
        "query",
        "token",
        "vector",
        "hybrid",
        "service",
        "segment",
        "storage",
    ];

    public static Document[] CreateDocuments(int count)
    {
        Document[] documents = new Document[count];
        for (int i = 0; i < documents.Length; i++)
        {
            string body = string.Join(' ', Terms[(i + 0) % Terms.Length], Terms[(i + 3) % Terms.Length], Terms[(i + 7) % Terms.Length]);
            documents[i] = new Document(new DocumentId(i.ToString("D8"))).Set("body", body);
        }
        return documents;
    }
}
