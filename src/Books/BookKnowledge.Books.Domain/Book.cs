using BookKnowledge.BuildingBlocks;

namespace BookKnowledge.Books.Domain;

public sealed class Book(Guid id, string title) : AggregateRoot<Guid>(id)
{
    public string Title { get; private set; } = title;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Title is required.", nameof(title));
        Title = title.Trim();
    }
}
