using FluentAssertions;
using MediaVault.LinkHub.Infrastructure.Data;
using Microsoft.Data.Sqlite;

namespace MediaVault.LinkHub.Tests.Data;

public sealed class SqliteDatabaseBackupServiceTests : IDisposable
{
    private readonly string _root;
    private readonly string _dbPath;
    private readonly string _backupDir;
    private readonly SqliteDatabaseBackupService _sut;

    public SqliteDatabaseBackupServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "MediaVaultBackupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _dbPath = Path.Combine(_root, "mediavault_linkhub.db");
        _backupDir = Path.Combine(_root, "Backups");

        using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "CREATE TABLE Demo(Id INTEGER PRIMARY KEY, Name TEXT); INSERT INTO Demo(Name) VALUES('alpha');";
            cmd.ExecuteNonQuery();
        }

        _sut = new SqliteDatabaseBackupService(_dbPath, _backupDir, maxBackups: 3, appDataDirectory: _root);
    }

    [Fact]
    public async Task CreateBackupAsync_writes_consistent_copy()
    {
        var result = await _sut.CreateBackupAsync("unit-test");

        result.Created.Should().BeTrue();
        result.BackupFilePath.Should().NotBeNullOrWhiteSpace();
        File.Exists(result.BackupFilePath!).Should().BeTrue();

        string? name;
        await using (var connection = new SqliteConnection($"Data Source={result.BackupFilePath}"))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Name FROM Demo";
            name = (string?)await cmd.ExecuteScalarAsync();
        }

        SqliteConnection.ClearAllPools();
        name.Should().Be("alpha");
    }

    [Fact]
    public async Task EnsureRecentBackupAsync_skips_when_fresh()
    {
        var first = await _sut.CreateBackupAsync("first");
        var second = await _sut.EnsureRecentBackupAsync(TimeSpan.FromHours(24), "second");

        second.Skipped.Should().BeTrue();
        second.BackupFilePath.Should().Be(first.BackupFilePath);
        _sut.ListBackups().Should().HaveCount(1);
    }

    [Fact]
    public async Task StageRestore_and_TryApplyPendingRestore_replaces_database()
    {
        var backup = await _sut.CreateBackupAsync("before-change");

        await using (var connection = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await connection.OpenAsync();
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE Demo SET Name='beta'";
            await cmd.ExecuteNonQueryAsync();
        }

        await _sut.StageRestoreAsync(backup.BackupFilePath!);
        _sut.TryApplyPendingRestore(out var message).Should().BeTrue();
        message.Should().Contain("restaurada");

        string? name;
        await using (var verify = new SqliteConnection($"Data Source={_dbPath}"))
        {
            await verify.OpenAsync();
            await using var verifyCmd = verify.CreateCommand();
            verifyCmd.CommandText = "SELECT Name FROM Demo";
            name = (string?)await verifyCmd.ExecuteScalarAsync();
        }

        SqliteConnection.ClearAllPools();
        name.Should().Be("alpha");
    }

    [Fact]
    public async Task CreateBackupAsync_prunes_beyond_max()
    {
        for (var i = 0; i < 5; i++)
        {
            await _sut.CreateBackupAsync($"n{i}");
            // Garantizar nombres/timestamps distintos en FS lentos.
            await Task.Delay(20);
        }

        _sut.ListBackups().Should().HaveCount(3);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Pool/antivirus pueden retener el handle un instante.
        }
    }
}
