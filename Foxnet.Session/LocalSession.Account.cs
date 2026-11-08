using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public enum SignInMode { FirstSignIn, Password, Recovery }

public enum PasswordResult { Accepted, Incorrect, RecoveryRequired }

public sealed record SignInState(SignInMode Mode, string? SuggestedLogin = null);

public sealed partial class LocalSession
{
    private const int FailedAttemptLimit = 3;
    private const int PasswordLengthLimit = 128;
    private const int HashIterations = 600_000;

    public SignInState GetSignInState()
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, """
            SELECT p.nickname, a.failed_attempts FROM profile p
            LEFT JOIN player_account a ON a.singleton = p.singleton
            WHERE p.singleton = 1;
            """);
        using var reader = query.ExecuteReader();
        if (!reader.Read())
            return new SignInState(SignInMode.FirstSignIn);
        if (reader.IsDBNull(1))
            return new SignInState(SignInMode.FirstSignIn, reader.GetString(0));
        return new SignInState(reader.GetInt32(1) >= FailedAttemptLimit ? SignInMode.Recovery : SignInMode.Password);
    }

    public void CreateAccount(string login, string password)
    {
        login = NormalizeNickname(login);
        ValidateNewPassword(password);
        var (salt, hash) = CreatePasswordHash(password);

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        if (ReadAccount(connection, transaction) != null)
            throw new InvalidOperationException("Профиль уже защищён паролем.");

        Execute(connection, transaction, """
            INSERT INTO profile(singleton, id, nickname) VALUES(1, $id, $login)
            ON CONFLICT(singleton) DO UPDATE SET nickname = excluded.nickname;
            """, ("id", Guid.NewGuid().ToString("D")), ("login", login));
        Execute(connection, transaction, """
            INSERT INTO player_account(singleton, salt, password_hash) VALUES(1, $salt, $hash);
            """, ("salt", salt), ("hash", hash));
        transaction.Commit();
    }

    public PasswordResult CheckPassword(string password)
    {
        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var account = ReadAccount(connection, transaction)
            ?? throw new InvalidOperationException("Сначала задайте логин и пароль.");
        if (account.FailedAttempts >= FailedAttemptLimit)
            return PasswordResult.RecoveryRequired;

        bool accepted = password.Length <= PasswordLengthLimit &&
            CryptographicOperations.FixedTimeEquals(HashPassword(password, account.Salt), account.Hash);
        int attempts = accepted ? 0 : account.FailedAttempts + 1;
        if (attempts != account.FailedAttempts)
        {
            Execute(connection, transaction, "UPDATE player_account SET failed_attempts = $attempts WHERE singleton = 1;",
                ("attempts", attempts));
        }
        transaction.Commit();

        if (accepted)
            return PasswordResult.Accepted;
        return attempts == FailedAttemptLimit ? PasswordResult.RecoveryRequired : PasswordResult.Incorrect;
    }

    public void RecoverAccount(string login, string password, string confirmation)
    {
        ValidateNewPassword(password);
        if (!string.Equals(password, confirmation, StringComparison.Ordinal))
            throw new ArgumentException("Пароли не совпадают.");

        using var connection = _database.Open();
        using var transaction = connection.BeginTransaction();
        var account = ReadAccount(connection, transaction);
        if (account == null || account.FailedAttempts < FailedAttemptLimit)
            throw new InvalidOperationException("Восстановление пароля недоступно.");
        if (!string.Equals(account.Login, login.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Неверный логин.");

        var (salt, hash) = CreatePasswordHash(password);
        Execute(connection, transaction, """
            UPDATE player_account SET salt = $salt, password_hash = $hash, failed_attempts = 0 WHERE singleton = 1;
            """, ("salt", salt), ("hash", hash));
        transaction.Commit();
    }

    private static Account? ReadAccount(SqliteConnection connection, SqliteTransaction transaction)
    {
        using var query = Command(connection, transaction, """
            SELECT p.nickname, a.salt, a.password_hash, a.failed_attempts
            FROM player_account a JOIN profile p ON p.singleton = a.singleton
            WHERE a.singleton = 1;
            """);
        using var reader = query.ExecuteReader();
        return reader.Read() ? new Account(reader.GetString(0), reader.GetFieldValue<byte[]>(1),
            reader.GetFieldValue<byte[]>(2), reader.GetInt32(3)) : null;
    }

    private static void ValidateNewPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length > PasswordLengthLimit)
            throw new ArgumentException("Введите пароль длиной до 128 символов.");
    }

    private static (byte[] Salt, byte[] Hash) CreatePasswordHash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        return (salt, HashPassword(password, salt));
    }

    private static byte[] HashPassword(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, HashIterations, HashAlgorithmName.SHA256, 32);

    private sealed record Account(string Login, byte[] Salt, byte[] Hash, int FailedAttempts);
}
