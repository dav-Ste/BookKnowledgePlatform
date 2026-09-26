namespace BookKnowledge.Books.Application;

public sealed record BookSearchResult(Guid Id, string Title);

public interface IBookRepository
{
    Task<IReadOnlyList<BookSearchResult>> SearchAsync(string term, CancellationToken cancellationToken);
}

public sealed class BookSearchService(IBookRepository repository)
{
    public Task<IReadOnlyList<BookSearchResult>> SearchAsync(string term, CancellationToken cancellationToken)
        => repository.SearchAsync(term, cancellationToken);
}
