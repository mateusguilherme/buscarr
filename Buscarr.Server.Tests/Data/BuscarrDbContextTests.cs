using Buscarr.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Buscarr.Server.Tests.Data;

public sealed class BuscarrDbContextTests
{
    [Fact]
    public void StringColumns_HaveMaxLengths()
    {
        using var db = new BuscarrDbContext(new DbContextOptionsBuilder<BuscarrDbContext>().UseSqlite("Data Source=:memory:").Options);

        var unlimited = db.Model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties())
            .Where(property => property.ClrType == typeof(string) && property.GetMaxLength() is null)
            .Select(property => $"{property.DeclaringType.DisplayName()}.{property.Name}")
            .ToList();

        Assert.Empty(unlimited);
    }
}
