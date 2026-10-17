using Buscarr.Server.Data;
using Buscarr.Server.Downloads;
using Buscarr.Server.Import;
using Buscarr.Server.Settings;
using Buscarr.Server.Subsonic;
using Buscarr.Server.Tests.Fakes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Buscarr.Server.Tests.Downloads;

public class ProgramWiringTests(BuscarrApiFactory factory) : IClassFixture<BuscarrApiFactory>
{
    [Fact]
    public void TheDownloadQueueServicesAreRegistered()
    {
        Assert.NotNull(factory.Services.GetRequiredService<DownloadRunner>());
        Assert.NotNull(factory.Services.GetRequiredService<GeneratorTrigger>());

        using var scope = factory.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SearchQueue>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ImportQueue>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RequestGenerator>());
    }

    [Fact]
    public void TheWorkersStayOutOfTheTests()
    {
        Assert.DoesNotContain(
            factory.Services.GetServices<IHostedService>(),
            service => service is GeneratorWorker or SearchWorker);
    }

    [Fact]
    public async Task SavedSettingsReachTheServicesWithoutARestart()
    {
        var store = factory.Services.GetRequiredService<ServerSettingsStore>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BuscarrDbContext>();
        var previous = store.Current;

        await store.SaveAsync(db, new ServerSettings
        {
            SubsonicUrl = "http://navidrome:4533",
            SubsonicUser = "fulano",
            SubsonicPassword = "senha",
            LibraryPath = "/music",
            DownloadsPath = "/downloads",
        });

        Assert.True(factory.Services.GetRequiredService<SubsonicClient>().IsConfigured);
        Assert.True(factory.Services.GetRequiredService<ImportPipeline>().Enabled);

        await store.SaveAsync(db, previous);
    }
}
