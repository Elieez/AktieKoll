using AktieKoll.Services;
using AktieKoll.Tests;
using CsvHelper;
using Devlead.Testing.MockHttp;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

public static partial class ServiceProviderFixture
{
    static partial void InitServiceProvider(IServiceCollection services)
    {
        services
            .AddMockHttpClient<Constants>()
            .AddSingleton<CsvFetchService>()
            .AddSingleton<Func<TextReader, CsvReader>>(reader =>
            {
                var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("sv-SE"))
                {
                    Delimiter = ";"
                };
                return new CsvReader(reader, config);
            });


    }
    public static IServiceCollection AuthorizedClient(this IServiceCollection services)
    {
        return ConfigureMockHttpClient(services);
    }
    private static IServiceCollection ConfigureMockHttpClient(IServiceCollection services)
    {
        return services.ConfigureMockHttpClient<Constants>(
            client =>
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    "Authorization",
                    "Basic YWRvOnRlc3QtcGF0"
                );
            }
        );
    }
}
