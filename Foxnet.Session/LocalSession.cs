using Microsoft.Data.Sqlite;
using static Foxnet.Session.SessionDatabase;

namespace Foxnet.Session;

public sealed partial class LocalSession
{
    public const int AvatarSizeLimit = 8 * 1024 * 1024;

    private readonly SessionDatabase _database;
    private readonly Scenario _scenario;
    private readonly Dictionary<string, IncomingDefinition> _messages;
    private readonly Dictionary<string, DeviceDefinition> _devices;
    private readonly Dictionary<string, ContactDefinition> _contacts;
    private readonly TimeProvider _clock;
    private readonly Random _random;
    private readonly IPhysicalDelivery? _delivery;
    private readonly IDialogueProvider? _dialogue;

    public LocalSession(string databasePath, Scenario scenario, TimeProvider? clock = null, Random? random = null,
        IPhysicalDelivery? delivery = null, IDialogueProvider? dialogue = null)
    {
        _database = new SessionDatabase(databasePath);
        _scenario = scenario;
        _clock = clock ?? TimeProvider.System;
        _random = random ?? Random.Shared;
        _delivery = delivery;
        _dialogue = dialogue;
        _contacts = scenario.Contacts.ToDictionary(contact => contact.Id, StringComparer.Ordinal);
        _messages = scenario.Messages.ToDictionary(message => message.Id, StringComparer.Ordinal);
        _devices = scenario.Devices.ToDictionary(device => device.Id, StringComparer.Ordinal);
        Documents = new DocumentLibrary(_database);
    }

    public DocumentLibrary Documents { get; }

    public void Initialize()
    {
        using var connection = _database.OpenForInitialization();
        using var transaction = connection.BeginTransaction();
        Migrate(connection, transaction);

        foreach (var contact in _scenario.Contacts)
            Execute(connection, transaction, """
                INSERT INTO contacts(id, nickname) VALUES($id, $nickname)
                ON CONFLICT(id) DO UPDATE SET nickname = excluded.nickname
                WHERE $authored AND contacts.nickname <> excluded.nickname;
                """, ("id", contact.Id), ("nickname", contact.Nickname), ("authored", contact.Details != null));

        foreach (var device in _scenario.Devices)
            Execute(connection, transaction, """
                INSERT INTO devices(id, name, kind, available) VALUES($id, $name, $kind, $available)
                ON CONFLICT(id) DO NOTHING;
                """, ("id", device.Id), ("name", device.Name), ("kind", device.Kind), ("available", device.InitiallyAvailable));

        foreach (var message in _scenario.Messages.Where(message => message.DeliverOnStart))
            Deliver(connection, transaction, message, UtcNow());

        transaction.Commit();
    }

    private long UtcNow() => _clock.GetUtcNow().ToUnixTimeMilliseconds();

    private static DateTimeOffset At(long timestamp) => DateTimeOffset.FromUnixTimeMilliseconds(timestamp);

    public PlayerProfile GetProfile()
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, "SELECT id, nickname, avatar FROM profile WHERE singleton = 1;");
        using var reader = query.ExecuteReader();
        if (!reader.Read())
            throw new InvalidDataException("В локальной сессии отсутствует профиль.");
        if (!Guid.TryParse(reader.GetString(0), out var id) || string.IsNullOrWhiteSpace(reader.GetString(1)))
            throw new InvalidDataException("В локальной сессии поврежден профиль.");
        return new PlayerProfile(id, reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetFieldValue<byte[]>(2));
    }

    public void Rename(string nickname)
    {
        nickname = NormalizeNickname(nickname);
        using var connection = _database.Open();
        Execute(connection, null, "UPDATE profile SET nickname = $nickname WHERE singleton = 1;", ("nickname", nickname));
    }

    private static string NormalizeNickname(string nickname)
    {
        nickname = nickname.Trim();
        if (nickname.Length is < 2 or > 32 || nickname.Any(char.IsControl))
            throw new ArgumentException("Введите от 2 до 32 символов без переносов строк.");
        return nickname;
    }

    public void SetAvatar(byte[]? image)
    {
        if (image is { Length: 0 or > AvatarSizeLimit })
            throw new ArgumentException("Файл аватара должен занимать не более 8 МБ и содержать изображение.");

        using var connection = _database.Open();
        Execute(connection, null, "UPDATE profile SET avatar = $avatar WHERE singleton = 1;", ("avatar", image));
    }

    public IReadOnlyList<Device> GetDevices()
    {
        using var connection = _database.Open();
        using var query = Command(connection, null, """
            SELECT id, name, kind, authorized, pid, started_at FROM devices WHERE available = 1 ORDER BY name;
            """);
        using var reader = query.ExecuteReader();
        var devices = new List<Device>();
        while (reader.Read())
        {
            var identity = reader.IsDBNull(4) ? null : new ConsoleIdentity(reader.GetInt32(4), reader.GetInt64(5));
            devices.Add(new Device(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3), identity));
        }
        return devices;
    }

    public bool Authorize(string deviceId, string login, string password)
    {
        if (!_devices.TryGetValue(deviceId, out var device) ||
            !string.Equals(device.Credentials.Login, login, StringComparison.Ordinal) ||
            !string.Equals(device.Credentials.Password, password, StringComparison.Ordinal))
            return false;

        using var connection = _database.Open();
        return Execute(connection, null, "UPDATE devices SET authorized = 1 WHERE id = $id AND available = 1;",
            ("id", deviceId)) == 1;
    }

    internal Device GetDevice(string deviceId) => GetDevices().FirstOrDefault(device => device.Id == deviceId)
        ?? throw new InvalidOperationException("Устройство недоступно.");

    internal bool SetConnection(string deviceId, ConsoleIdentity? identity, ConsoleIdentity? previous)
    {
        using var connection = _database.Open();
        return Execute(connection, null, """
            UPDATE devices SET pid = $pid, started_at = $startedAt
            WHERE id = $id AND authorized = 1 AND pid IS $previousPid AND started_at IS $previousStartedAt;
            """, ("id", deviceId), ("pid", identity?.ProcessId), ("startedAt", identity?.StartedAtUtcTicks),
            ("previousPid", previous?.ProcessId), ("previousStartedAt", previous?.StartedAtUtcTicks)) == 1;
    }

    internal void RevokeAccess(string deviceId)
    {
        using var connection = _database.Open();
        Execute(connection, null, "UPDATE devices SET authorized = 0, pid = NULL, started_at = NULL WHERE id = $id;",
            ("id", deviceId));
    }
}
