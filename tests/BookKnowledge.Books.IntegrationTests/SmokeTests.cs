using Xunit;

namespace BookKnowledge.Books.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public void TestProjectIsReadyForIntegrationTests()
        => Assert.True(true);
}
