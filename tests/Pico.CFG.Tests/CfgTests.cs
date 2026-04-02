namespace Pico.CFG.Tests;

public class CfgTests
{
    [Test]
    public async Task CreateBuilder_ReturnsNonNull()
    {
        var builder = CFG.CreateBuilder();
        await Assert.That(builder).IsNotNull();
    }
}
