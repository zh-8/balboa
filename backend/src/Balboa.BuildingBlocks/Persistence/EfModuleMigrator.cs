using Balboa.BuildingBlocks.Messaging;
using Microsoft.EntityFrameworkCore;

namespace Balboa.BuildingBlocks.Persistence;

public sealed class EfModuleMigrator<TDbContext>(TDbContext db, string moduleName) : IModuleMigrator
    where TDbContext : DbContext
{
    public string ModuleName => moduleName;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        db.Database.MigrateAsync(cancellationToken);
}
