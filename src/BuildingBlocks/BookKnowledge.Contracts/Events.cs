namespace BookKnowledge.Contracts;

public sealed record BookUploaded(
    Guid BookId,
    string FileName,
    string ContentHash,
    DateTimeOffset OccurredAt);

public sealed record BookIndexingRequested(
    Guid BookId,
    DateTimeOffset OccurredAt);
