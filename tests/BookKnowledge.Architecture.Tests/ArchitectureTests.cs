using NetArchTest.Rules;
using Xunit;

namespace BookKnowledge.Architecture.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void DomainShouldNotDependOnInfrastructureOrApi()
    {
        var result = Types.InAssembly(typeof(BookKnowledge.Books.Domain.Book).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("BookKnowledge.Books.Infrastructure", "BookKnowledge.Books.Api")
            .GetResult();

        Assert.True(result.IsSuccessful);
    }
}
