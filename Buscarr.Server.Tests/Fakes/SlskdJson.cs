using System.Text.Json;

namespace Buscarr.Server.Tests.Fakes;

public static class SlskdJson
{
    public static string Application(bool isConnected, bool isLoggedIn) =>
        JsonSerializer.Serialize(new
        {
            server = new
            {
                address = "server.slsknet.org:2242",
                isConnected,
                isLoggedIn,
            },
        });
}
