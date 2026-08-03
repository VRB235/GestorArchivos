using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Data;

/// <summary>
/// Extensiones para configurar <see cref="AppDbContext"/> con SQLite.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder<AppDbContext> UseAppSqlite(
        this DbContextOptionsBuilder<AppDbContext> optionsBuilder,
        string? databasePath = null)
    {
        var connectionString = SqliteDatabasePathProvider.BuildConnectionString(databasePath);
        return optionsBuilder.UseSqlite(connectionString);
    }
}
