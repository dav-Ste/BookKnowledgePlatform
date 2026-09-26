using Xunit;

namespace BookKnowledge.Gateway.IntegrationTests;

public sealed class SmokeTests
{
    [Fact]
    public void TestProjectIsReadyForGatewayTests()
        => Assert.True(true);
}
