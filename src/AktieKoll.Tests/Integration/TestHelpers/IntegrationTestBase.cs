using AktieKoll.Tests.Fixture;
using Microsoft.Extensions.DependencyInjection;

namespace AktieKoll.Tests.Integration.TestHelpers;

public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactoryFixture>, IDisposable
{
    protected readonly WebApplicationFactoryFixture Factory;
    protected readonly HttpClient Client;
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    protected IntegrationTestBase(WebApplicationFactoryFixture factory)
    {
        Factory = factory;
        Client = Factory.CreateClient();

        Factory.ResetDatabase();
    }


    public void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected IServiceScope CreateScope() => Factory.Services.CreateScope();
}