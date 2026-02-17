using AktieKoll.Tests.Fixture;
using Microsoft.Extensions.DependencyInjection;

namespace AktieKoll.Tests.Integration.TestHelpers;

public abstract class IntegrationTestBase : IClassFixture<WebApplicationFactoryFixture>, IAsyncLifetime, IDisposable
{
    protected readonly WebApplicationFactoryFixture Factory;
    protected readonly HttpClient Client;
    protected static CancellationToken Token => TestContext.Current.CancellationToken;

    protected IntegrationTestBase(WebApplicationFactoryFixture factory)
    {
        Factory = factory;
        Client = Factory.CreateClient();
    }

    public virtual ValueTask InitializeAsync()
    {
        Factory.ResetDatabase();
        return ValueTask.CompletedTask;
    }

    public virtual ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        Client?.Dispose();
        GC.SuppressFinalize(this);
    }

    protected IServiceScope CreateScope() => Factory.Services.CreateScope();
}