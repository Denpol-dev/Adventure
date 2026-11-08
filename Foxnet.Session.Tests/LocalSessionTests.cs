using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System.Text;

namespace Foxnet.Session.Tests;

public sealed class LocalSessionTests
{
    private string _directory = null!;
    private string _database = null!;

    [SetUp]
    public void CreateSessionDirectory()
    {
        _directory = Directory.CreateTempSubdirectory("foxnet-session-test-").FullName;
        _database = Path.Combine(_directory, "session.db");
    }

    [TearDown]
    public void RemoveSessionDirectory() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void FirstSignInCreatesOneProfileWithTheChosenLogin()
    {
        var session = Open();
        Assert.That(session.GetSignInState(), Is.EqualTo(new SignInState(SignInMode.FirstSignIn)));
        Assert.Throws<InvalidDataException>(() => session.GetProfile());
        Assert.Throws<ArgumentException>(() => session.CreateAccount("Pilot", ""));
        Assert.That(session.GetSignInState().Mode, Is.EqualTo(SignInMode.FirstSignIn));

        session.CreateAccount("  Pilot Vega  ", "first-password");
        var first = session.GetProfile();
        var reopened = Open();
        Assert.Multiple(() =>
        {
            Assert.That(first.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(first.Nickname, Is.EqualTo("Pilot Vega"));
            Assert.That(first.Avatar, Is.Null);
            Assert.That(reopened.GetProfile(), Is.EqualTo(first));
            Assert.That(reopened.GetSignInState().Mode, Is.EqualTo(SignInMode.Password));
            Assert.That(reopened.CheckPassword("first-password"), Is.EqualTo(PasswordResult.Accepted));
            Assert.Throws<InvalidOperationException>(() => reopened.CreateAccount("Another Pilot", "another-password"));
            Assert.That(reopened.GetProfile(), Is.EqualTo(first));
        });
    }

    [Test]
    public void NicknameAndAvatarSurviveRestartsAndDefaultAvatarCanBeRestored()
    {
        var session = Open();
        session.CreateAccount("Initial Pilot", "first-password");
        var original = session.GetProfile();
        byte[] avatar = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aNa4AAAAASUVORK5CYII=");
        session.Rename("  Pilot Vega  ");
        session.SetAvatar(avatar);

        var reopened = Open();
        var profile = reopened.GetProfile();
        Assert.Multiple(() =>
        {
            Assert.That(profile.Id, Is.EqualTo(original.Id));
            Assert.That(profile.Nickname, Is.EqualTo("Pilot Vega"));
            Assert.That(profile.Avatar, Is.EqualTo(avatar));
        });

        Assert.Throws<ArgumentException>(() => reopened.Rename(" "));
        reopened.SetAvatar(null);
        Assert.That(Open().GetProfile(), Is.EqualTo(new PlayerProfile(original.Id, "Pilot Vega", null)));
    }

    [Test]
    public void ThreeWrongPasswordsRequireRecoveryEvenAfterRestart()
    {
        var session = Open();
        session.CreateAccount("Orbit-01", "original-password");
        session.Rename("Orbit-02");

        Assert.That(session.CheckPassword("wrong"), Is.EqualTo(PasswordResult.Incorrect));
        Assert.That(Open().CheckPassword("wrong"), Is.EqualTo(PasswordResult.Incorrect));
        Assert.That(Open().CheckPassword("wrong"), Is.EqualTo(PasswordResult.RecoveryRequired));

        var reopened = Open();
        Assert.That(reopened.GetSignInState().Mode, Is.EqualTo(SignInMode.Recovery));
        Assert.That(reopened.CheckPassword("original-password"), Is.EqualTo(PasswordResult.RecoveryRequired));
        Assert.Throws<ArgumentException>(() => reopened.RecoverAccount("Orbit-01", "new-password", "new-password"));
        Assert.Throws<ArgumentException>(() => reopened.RecoverAccount("Orbit-02", "new-password", "different"));
        Assert.That(Open().GetSignInState().Mode, Is.EqualTo(SignInMode.Recovery));

        reopened.RecoverAccount("Orbit-02", "new-password", "new-password");
        var recovered = Open();
        Assert.That(recovered.GetSignInState().Mode, Is.EqualTo(SignInMode.Password));
        Assert.That(recovered.CheckPassword("original-password"), Is.EqualTo(PasswordResult.Incorrect));
        Assert.That(recovered.CheckPassword("new-password"), Is.EqualTo(PasswordResult.Accepted));
    }

    [Test]
    public void CorrectPasswordResetsFailedAttemptsAndRecoveryIsUnavailableBeforeTheLimit()
    {
        var session = Open();
        session.CreateAccount("Relay-42", "password");
        Assert.That(session.CheckPassword("wrong"), Is.EqualTo(PasswordResult.Incorrect));
        Assert.That(session.CheckPassword("wrong"), Is.EqualTo(PasswordResult.Incorrect));
        Assert.Throws<InvalidOperationException>(() => session.RecoverAccount("Relay-42", "new", "new"));
        Assert.That(session.CheckPassword("password"), Is.EqualTo(PasswordResult.Accepted));

        var reopened = Open();
        Assert.That(reopened.CheckPassword("wrong"), Is.EqualTo(PasswordResult.Incorrect));
        Assert.That(reopened.CheckPassword("wrong"), Is.EqualTo(PasswordResult.Incorrect));
        Assert.That(reopened.GetSignInState().Mode, Is.EqualTo(SignInMode.Password));
    }

    [Test]
    public void ExistingSessionKeepsTheProfileHistoryAndDocumentsWhenAddingAPassword()
    {
        var session = Open();
        session.CreateAccount("Old Nickname", "temporary");
        byte[] avatar = [1, 2, 3];
        session.SetAvatar(avatar);
        var profileId = session.GetProfile().Id;
        var message = session.OpenConversation("unknown").Single();
        Assert.That(session.SelectReply(message.Id, message.Replies.Single().Id), Is.True);
        var document = session.Documents.AddFromAttachment(message.Attachments.Single().EventId)!;
        using (var connection = new SqliteConnection($"Data Source={_database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DROP TABLE player_account; PRAGMA user_version = 1;";
            command.ExecuteNonQuery();
        }

        var migrated = Open();
        Assert.That(migrated.GetSignInState(), Is.EqualTo(new SignInState(SignInMode.FirstSignIn, "Old Nickname")));
        migrated.CreateAccount("Chosen Login", "new-password");

        var reopened = Open();
        var profile = reopened.GetProfile();
        Assert.Multiple(() =>
        {
            Assert.That(profile.Id, Is.EqualTo(profileId));
            Assert.That(profile.Nickname, Is.EqualTo("Chosen Login"));
            Assert.That(profile.Avatar, Is.EqualTo(avatar));
            Assert.That(reopened.GetConversations().Single().UnreadCount, Is.Zero);
            Assert.That(reopened.OpenConversation("unknown"), Has.Count.EqualTo(2));
            Assert.That(reopened.Documents.Get(document.Info.EventId)!.Content, Is.EqualTo(document.Content));
            Assert.That(reopened.CheckPassword("new-password"), Is.EqualTo(PasswordResult.Accepted));
        });
    }

    [Test]
    public void UnknownMessageIsDeliveredOnceAndReadStatusSurvivesRestart()
    {
        var session = Open();
        var conversation = session.GetConversations().Single();
        Assert.That(conversation.Nickname, Is.EqualTo("unknown"));
        Assert.That(conversation.UnreadCount, Is.EqualTo(1));

        var incoming = session.OpenConversation(conversation.Id).Single();
        var credentials = Scenario.LoadDefault().Devices.Single().Credentials;
        Assert.Multiple(() =>
        {
            Assert.That(incoming.Outgoing, Is.False);
            Assert.That(incoming.Text, Does.Contain(credentials.Login).And.Contain(credentials.Password));
            Assert.That(incoming.Attachments, Has.Count.EqualTo(1));
            Assert.That(incoming.Replies, Has.Count.EqualTo(1));
        });

        var reopened = Open();
        Assert.That(reopened.GetConversations().Single().UnreadCount, Is.Zero);
        Assert.That(reopened.OpenConversation(conversation.Id).Select(message => message.Id), Is.EqualTo(new[] { incoming.Id }));
    }

    [Test]
    public void ReplyIsPersistedAndCannotBeSelectedAgain()
    {
        var session = Open();
        var option = session.OpenConversation("unknown").Single().Replies.Single();
        Assert.That(session.SelectReply(option.MessageId, "not-a-reply"), Is.False);
        Assert.That(session.SelectReply(option.MessageId, option.Id), Is.True);

        var reopened = Open();
        Assert.That(reopened.SelectReply(option.MessageId, option.Id), Is.False);
        var history = reopened.OpenConversation("unknown");
        Assert.Multiple(() =>
        {
            Assert.That(history, Has.Count.EqualTo(2));
            Assert.That(history[0].Replies, Is.Empty);
            Assert.That(history[1].Outgoing, Is.True);
            Assert.That(history[1].Text, Is.EqualTo(option.Text));
            Assert.That(reopened.GetConversations().Single().LastMessage, Is.EqualTo(option.Text));
        });
    }

    [Test]
    public void ConfiguredReplyCanDeliverAMessageAndUnlockADeviceInTheSameAction()
    {
        var baseScenario = Scenario.LoadDefault();
        var seed = baseScenario.Messages.Single();
        var device = baseScenario.Devices.Single() with { InitiallyAvailable = false };
        var scenario = baseScenario with
        {
            Devices = [device],
            Messages =
            [
                seed with { Replies = [new ReplyDefinition("ok", "Принято.", "follow-up", device.Id)] },
                new IncomingDefinition("follow-up", "unknown", false, null, "Получено.", [], [])
            ]
        };
        var session = Open(scenario);
        Assert.That(session.GetDevices(), Is.Empty);
        Assert.That(session.SelectReply(seed.Id, "ok"), Is.True);

        var reopened = Open(scenario);
        Assert.Multiple(() =>
        {
            Assert.That(reopened.GetDevices().Single().Id, Is.EqualTo(device.Id));
            Assert.That(reopened.GetConversations().Single().UnreadCount, Is.EqualTo(1));
            Assert.That(reopened.OpenConversation("unknown").Select(message => message.Text).Skip(1),
                Is.EqualTo(new[] { "Принято.", "Получено." }));
            Assert.That(reopened.SelectReply(seed.Id, "ok"), Is.False);
        });
    }

    [Test]
    public void AttachmentAndTerminalDocumentsAreAddedOnlyOncePerEvent()
    {
        var session = Open();
        Assert.That(session.Documents.List(), Is.Empty);
        var attachment = session.OpenConversation("unknown").Single().Attachments.Single();
        var first = session.Documents.AddFromAttachment(attachment.EventId)!;
        var second = session.Documents.AddFromAttachment(attachment.EventId)!;
        var terminal = new ReceivedDocument("terminal-xd23", "Журнал", "report.json", DocumentFormat.Json,
            "Терминал N23S57", Encoding.UTF8.GetBytes("""{"signal":"foxnet"}"""));
        Assert.That(session.Documents.Add(terminal), Is.True);
        Assert.That(session.Documents.Add(terminal with { Title = "Повторная доставка" }), Is.False);

        var reopened = Open();
        Assert.Multiple(() =>
        {
            Assert.That(reopened.Documents.List(), Has.Count.EqualTo(2));
            Assert.That(first.Info, Is.EqualTo(second.Info));
            Assert.That(first.Info.Source, Is.EqualTo("Сообщение от unknown"));
            Assert.That(first.Info.Format, Is.EqualTo(DocumentFormat.Markdown));
            Assert.That(Encoding.UTF8.GetString(first.Content), Does.Contain("# Доступ к N910VT"));
            Assert.That(reopened.Documents.Get(terminal.EventId)!.Content, Is.EqualTo(terminal.Content));
            Assert.That(reopened.Documents.Get(terminal.EventId)!.Info.Title, Is.EqualTo(terminal.Title));
        });
    }

    [Test]
    public void CorrectCredentialsUnlockN910vtAcrossRestarts()
    {
        var scenario = Scenario.LoadDefault();
        var definition = scenario.Devices.Single();
        var session = Open(scenario);
        Assert.That(session.GetDevices().Single().Name, Is.EqualTo("N910VT"));
        Assert.That(session.Authorize(definition.Id, definition.Credentials.Login, "wrong"), Is.False);
        Assert.That(Open(scenario).GetDevices().Single().Authorized, Is.False);

        Assert.That(session.Authorize(definition.Id, definition.Credentials.Login, definition.Credentials.Password), Is.True);
        var reloaded = Open(scenario).GetDevices().Single();
        Assert.That(reloaded.Authorized, Is.True);
        Assert.That(reloaded.Connected, Is.False);
    }

    [Test]
    public void ConnectingRepeatedlyAndReopeningDesktopReuseTheSameRunningProcess()
    {
        var (session, deviceId) = AuthorizedSession();
        var host = new FakeConsole();
        var controller = new DeviceConnectionService(session, host);
        var devices = Enumerable.Range(0, 8).AsParallel().Select(_ => controller.Connect(deviceId)).ToArray();
        Assert.That(devices.Select(device => device.Connection).Distinct().Count(), Is.EqualTo(1));
        Assert.That(host.LaunchCount, Is.EqualTo(1));

        var reopened = new DeviceConnectionService(Open(), host);
        Assert.That(reopened.GetDevices().Single().Connected, Is.True);
        Assert.That(reopened.Connect(deviceId).Connection, Is.EqualTo(devices[0].Connection));
        Assert.That(host.LaunchCount, Is.EqualTo(1));
        reopened.Disconnect(deviceId);
        Assert.That(Open().GetDevices().Single().Connected, Is.False);
        Assert.That(host.StopCount, Is.EqualTo(1));
    }

    [Test]
    public void ClosingConsoleExternallyClearsTheProcessButKeepsAuthorization()
    {
        var (session, deviceId) = AuthorizedSession();
        var host = new FakeConsole();
        var controller = new DeviceConnectionService(session, host);
        controller.Connect(deviceId);
        host.Running = null;

        var reloaded = new DeviceConnectionService(Open(), host);
        var reconciled = reloaded.GetDevices().Single();
        Assert.That(reconciled.Connected, Is.False);
        Assert.That(reconciled.Authorized, Is.True);
        Assert.That(reloaded.Connect(deviceId).Connected, Is.True);
        Assert.That(host.LaunchCount, Is.EqualTo(2));

        reloaded.RevokeAccess(deviceId);
        Assert.That(Open().GetDevices().Single().Authorized, Is.False);
        Assert.That(host.Running, Is.Null);
        Assert.Throws<InvalidOperationException>(() => reloaded.Connect(deviceId));
    }

    [Test]
    public void FailedProcessStartDoesNotSaveAFakeConnection()
    {
        var (session, deviceId) = AuthorizedSession();
        var controller = new DeviceConnectionService(session, new FakeConsole { Fail = true });
        Assert.Throws<FileNotFoundException>(() => controller.Connect(deviceId));
        Assert.That(Open().GetDevices().Single().Connected, Is.False);
        Assert.That(Open().GetDevices().Single().Authorized, Is.True);
    }

    [Test]
    public void CorruptDatabaseIsReportedAndItsContentsArePreserved()
    {
        byte[] damaged = Encoding.UTF8.GetBytes("not a database");
        File.WriteAllBytes(_database, damaged);

        Assert.Throws<SqliteException>(() => Open());
        Assert.That(File.ReadAllBytes(_database), Is.EqualTo(damaged));
    }

    [Test]
    public void ADatabaseFromAnUnsupportedSchemaVersionIsRejected()
    {
        Open();
        using (var connection = new SqliteConnection($"Data Source={_database};Pooling=False"))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 7;";
            command.ExecuteNonQuery();
        }
        Assert.That(Assert.Throws<InvalidDataException>(() => Open())!.Message, Does.Contain("7"));
    }

    private LocalSession Open(Scenario? scenario = null)
    {
        var session = new LocalSession(_database, scenario ?? Scenario.LoadDefault());
        session.Initialize();
        return session;
    }

    private (LocalSession Session, string DeviceId) AuthorizedSession()
    {
        var scenario = Scenario.LoadDefault();
        var device = scenario.Devices.Single();
        var session = Open(scenario);
        Assert.That(session.Authorize(device.Id, device.Credentials.Login, device.Credentials.Password), Is.True);
        return (session, device.Id);
    }

    private sealed class FakeConsole : IConsoleHost
    {
        public ConsoleIdentity? Running { get; set; }
        public int LaunchCount { get; private set; }
        public int StopCount { get; private set; }
        public bool Fail { get; init; }

        public ConsoleIdentity Launch()
        {
            if (Fail)
                throw new FileNotFoundException("Adventure.exe");
            return Running = new ConsoleIdentity(++LaunchCount, DateTime.UtcNow.Ticks);
        }

        public bool IsRunning(ConsoleIdentity identity) => identity == Running;

        public void Stop(ConsoleIdentity identity)
        {
            if (Running != identity)
                return;
            StopCount++;
            Running = null;
        }
    }
}
