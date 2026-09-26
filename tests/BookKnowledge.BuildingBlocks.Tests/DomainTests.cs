using BookKnowledge.BuildingBlocks;
using Xunit;

namespace BookKnowledge.BuildingBlocks.Tests;

public sealed class DomainTests
{
    [Fact]
    public void EntityExposesId()
    {
        var entity = new TestEntity(Guid.NewGuid());
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    private sealed class TestEntity(Guid id) : Entity<Guid>(id);
}
