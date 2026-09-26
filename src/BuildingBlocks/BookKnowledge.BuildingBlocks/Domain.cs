namespace BookKnowledge.BuildingBlocks;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public abstract record DomainEvent(DateTimeOffset OccurredAt) : IDomainEvent;

public abstract class Entity<TId>(TId id)
{
    public TId Id { get; protected init; } = id;
}

public abstract class AggregateRoot<TId>(TId id) : Entity<TId>(id)
{
    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
}
