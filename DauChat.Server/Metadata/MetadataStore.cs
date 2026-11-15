using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace DauChat.Server.Metadata;

public sealed class MetadataStore : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly object _sync = new();

    public MetadataStore(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        _connection = new SqliteConnection($"Data Source={databasePath}");
        _connection.Open();
        Initialize();
    }

    public void Initialize()
    {
        lock (_sync)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                PRAGMA foreign_keys = ON;
                CREATE TABLE IF NOT EXISTS Users (
                    UserId TEXT PRIMARY KEY, Username TEXT NOT NULL UNIQUE, PasswordHash TEXT NOT NULL,
                    FullName TEXT NOT NULL, Email TEXT NOT NULL, AvatarPath TEXT NULL, Role INTEGER NOT NULL,
                    Status INTEGER NOT NULL, ClassId TEXT NULL, LockReason TEXT NULL,
                    CreatedAtUtc TEXT NOT NULL, LastSeenAtUtc TEXT NULL);
                CREATE TABLE IF NOT EXISTS Classes (
                    ClassId TEXT PRIMARY KEY, ClassCode TEXT NOT NULL UNIQUE, ClassName TEXT NOT NULL,
                    Department TEXT NOT NULL, AcademicYear TEXT NOT NULL, LecturerId TEXT NULL,
                    LinkedRoomId TEXT NULL, CreatedAtUtc TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS ClassMembers (
                    ClassId TEXT NOT NULL, UserId TEXT NOT NULL, PRIMARY KEY (ClassId, UserId),
                    FOREIGN KEY(ClassId) REFERENCES Classes(ClassId) ON DELETE CASCADE,
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
                CREATE TABLE IF NOT EXISTS Rooms (
                    RoomId TEXT PRIMARY KEY, RoomName TEXT NOT NULL, Description TEXT NULL,
                    RoomType INTEGER NOT NULL, ClassId TEXT NULL, OwnerId TEXT NOT NULL,
                    CreatedAtUtc TEXT NOT NULL, LastActivityAtUtc TEXT NULL);
                CREATE TABLE IF NOT EXISTS RoomMembers (
                    RoomId TEXT NOT NULL, UserId TEXT NOT NULL, JoinedAtUtc TEXT NOT NULL,
                    PRIMARY KEY (RoomId, UserId),
                    FOREIGN KEY(RoomId) REFERENCES Rooms(RoomId) ON DELETE CASCADE,
                    FOREIGN KEY(UserId) REFERENCES Users(UserId) ON DELETE CASCADE);
                CREATE INDEX IF NOT EXISTS IX_Users_Username ON Users(Username);
                CREATE INDEX IF NOT EXISTS IX_ClassMembers_UserId ON ClassMembers(UserId);
                CREATE INDEX IF NOT EXISTS IX_RoomMembers_UserId ON RoomMembers(UserId);
                """;
            command.ExecuteNonQuery();
        }
    }

    public void SeedAdmin(string username, string password, string fullName, string email)
    {
        lock (_sync)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Users(UserId, Username, PasswordHash, FullName, Email, Role, Status, CreatedAtUtc)
                SELECT $id, $username, $hash, $fullName, $email, 1, 1, $created
                WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Username = $username);
                """;
            command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
            command.Parameters.AddWithValue("$username", username);
            command.Parameters.AddWithValue("$hash", HashPassword(password));
            command.Parameters.AddWithValue("$fullName", fullName);
            command.Parameters.AddWithValue("$email", email);
            command.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("O"));
            command.ExecuteNonQuery();
        }
    }

    public static string HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 120_000, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256$120000${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool VerifyPassword(string password, string encodedHash)
    {
        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var iterations)) return false;
        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }

    public void Dispose() => _connection.Dispose();
}
