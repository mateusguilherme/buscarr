using Buscarr.Server.Downloads;
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
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RequestGenerator>());
    }

    [Fact]
    public void TheWorkersStayOutOfTheTests()
    {
        Assert.DoesNotContain(
            factory.Services.GetServices<IHostedService>(),
            service => service is GeneratorWorker or SearchWorker);
    }
}
