using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Data;

/// <summary>
/// Crea instancias de <see cref="AppDbContext"/> en tiempo de ejecución.
/// </summary>
public static class AppDbContextRuntimeFactory
{
    public static AppDbContext Create(string? databasePath = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseAppSqlite(databasePath);
        return new AppDbContext(optionsBuilder.Options);
    }
}
