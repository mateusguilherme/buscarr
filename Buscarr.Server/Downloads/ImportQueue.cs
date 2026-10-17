using Buscarr.Server.Data;
using Buscarr.Server.Import;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Downloads;

public sealed class ImportQueue(BuscarrDbContext db, ImportPipeline pipeline)
{
    public async Task<int?> ClaimNextAsync()
    {
        if (!pipeline.Enabled)
        {
            return null;
        }

        var request = await db.DownloadRequests
            .Where(r => r.State == DownloadState.Downloaded && r.EditionReleaseId != null && r.Directory != null)
            .OrderBy(r => r.Id)
            .FirstOrDefaultAsync();

        if (request is null)
        {
            return null;
        }

        request.State = DownloadState.Importing;
        await db.SaveChangesAsync();

        return request.Id;
    }
}
