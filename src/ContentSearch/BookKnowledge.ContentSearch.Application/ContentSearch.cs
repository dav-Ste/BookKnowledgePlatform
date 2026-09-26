namespace BookKnowledge.ContentSearch.Application;

public sealed record ContentSearchResult(
    Guid BookId,
    string BookTitle,
    string Chapter,
    int PageNumber,
    string Summary,
    string SourceReference);

public interface IBookContentSearch
{
    Task<IReadOnlyList<ContentSearchResult>> SearchAsync(string query, CancellationToken cancellationToken);
}
