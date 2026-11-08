using Adventure.Entities.Worlds;
using Adventure.Expeditions;
using Foxnet.Session;
using Microsoft.Data.Sqlite;
using NUnit.Framework;
using System.Text.Json;

namespace Adventure.Tests;

public sealed class ExpeditionIntegrationTests
{
    private string _directory = null!;
    private string Database => Path.Combine(_directory, "session.db");

    [SetUp]
    public void Setup() => _directory = Directory.CreateTempSubdirectory("foxnet-expedition-tests-").FullName;

    [TearDown]
    public void Cleanup() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void MigrationAndExpeditionWritesPreserveAnExistingDesktopAccount()
    {
        var desktop = new LocalSession(Database, Scenario.LoadDefault());
        desktop.Initialize();
        desktop.CreateAccount("Pilot-17", "test-password");
        desktop.SetAvatar([1, 2, 3]);
        var message = desktop.OpenConversation("unknown").Single();
        desktop.Documents.AddFromAttachment(message.Attachments.Single().EventId);
        var device = Scenario.LoadDefault().Devices.Single();
        Assert.That(desktop.Authorize(device.Id, device.Credentials.Login, device.Credentials.Password), Is.True);

        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open();
            using var sql = connection.CreateCommand();
            sql.CommandText = "DROP TABLE expeditions; PRAGMA user_version = 2;";
            sql.ExecuteNonQuery();
        }

        using (var owner = new ExpeditionOwner(ExpeditionTests.Load(), Database))
            Assert.That(owner.Execute(ExpeditionTests.Move(FlightDirection.Right)).Result.Reply.Applied, Is.True);

        desktop.Initialize();
        Assert.Multiple(() =>
        {
            Assert.That(desktop.GetProfile().Nickname, Is.EqualTo("Pilot-17"));
            Assert.That(desktop.GetProfile().Avatar, Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(desktop.CheckPassword("test-password"), Is.EqualTo(PasswordResult.Accepted));
            Assert.That(desktop.GetConversations().Single().UnreadCount, Is.Zero);
            Assert.That(desktop.Documents.List(), Has.Count.EqualTo(1));
            Assert.That(desktop.GetDevices().Single().Authorized, Is.True);
        });
    }

    [Test]
    public void RestartRestoresFleetModeEquipmentDonorsAndSurveyWithoutCreatingANewRun()
    {
        var world = ExpeditionTests.Load();
        var engine = ExpeditionTests.At(world, "N23S55", 36, 26);
        string battery = engine.Definition.Donors.First().Components[1].Id;
        ExpeditionTests.Apply(engine, FlightAction.Salvage, id: battery);
        engine = ExpeditionTests.Relocate(world, engine.State, "N23S55", 35, 24);
        string originalBattery = engine.ActiveDrone.Battery!.Id;
        ExpeditionTests.Apply(engine, FlightAction.Install, battery, EquipmentSlot.Battery);
        ExpeditionTests.Apply(engine, FlightAction.Charge);
        ExpeditionTests.Apply(engine, FlightAction.Install, originalBattery, EquipmentSlot.Battery);
        engine = ExpeditionTests.Relocate(world, engine.State, "N23S56", 47, 11);
        ExpeditionTests.Apply(engine, FlightAction.Interact);
        engine = ExpeditionTests.Relocate(world, engine.State, "N23S57", 29, 15);
        ExpeditionTests.Apply(engine, FlightAction.Interact);
        engine = ExpeditionTests.Relocate(world, engine.State, "N23S55", 71, 12);
        ExpeditionTests.Step(engine, FlightDirection.Right);
        ExpeditionTests.Apply(engine, FlightAction.SelectDrone, id: "DR-02");
        Seed(world, engine.State);

        ExpeditionStatus expected;
        string snapshot;
        using (var owner = new ExpeditionOwner(world, Database))
        {
            Assert.Throws<IOException>(() => new ExpeditionOwner(world, Database));
            Assert.That(owner.Execute(new FlightCommand(FlightAction.CycleMode)).Result.Reply.Applied, Is.True);
            expected = owner.Status();
            snapshot = owner.Read(runtime => JsonSerializer.Serialize(runtime.State));
        }

        using var restored = new ExpeditionOwner(world, Database);
        Assert.That(restored.WasRestored, Is.True);
        Assert.That(restored.Status().Revision, Is.EqualTo(expected.Revision));
        Assert.That(restored.Status().SelectedDroneId, Is.EqualTo("DR-02"));
        Assert.That(restored.Status().Mode, Is.EqualTo(ControlMode.Drone));
        Assert.That(restored.Read(runtime => JsonSerializer.Serialize(runtime.State)), Is.EqualTo(snapshot));
        Assert.That(restored.Read(runtime => runtime.State.Drones["DR-01"].Cargo.Single().Energy), Is.EqualTo(265));
        Assert.That(restored.Read(runtime => runtime.State.Station.ChargeCycles), Is.EqualTo(1));
        Assert.That(restored.Read(runtime => runtime.State.Station.DeliveredEnergy), Is.EqualTo(75));
        Assert.That(restored.Read(runtime => runtime.State.Donors["DN-01"].Select(module => module.Id)), Does.Not.Contain(battery));
        Assert.That(restored.Read(runtime => runtime.State.TerminalUses.Values.Single()), Is.EqualTo(1));
        Assert.That(restored.Status().ExploredCells, Is.GreaterThan(0));
    }

    [Test]
    public void EndingTheExpeditionIsPersistent()
    {
        var world = ExpeditionTests.Load();
        var engine = ExpeditionTests.At(world, "N24S55", 40, 14);
        foreach (var drone in engine.State.Drones.Values)
            ExpeditionTests.SetEnergy(drone, drone == engine.ActiveDrone ? 0.01m : 0);
        Seed(world, engine.State);
        using (var owner = new ExpeditionOwner(world, Database))
            Assert.That(owner.Execute(ExpeditionTests.Move(FlightDirection.Right)).Status.GameOver, Is.True);

        using var restored = new ExpeditionOwner(world, Database);
        Assert.That(restored.Status().GameOver, Is.True);
        Assert.That(restored.Execute(new FlightCommand(FlightAction.Charge)).Result.Reply.Applied, Is.False);
    }

    [Test]
    public async Task NamedPipeReturnsCommittedResultsPushesUpdatesAndReconnects()
    {
        var world = ExpeditionTests.Load();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var client = new ExpeditionPipeClient(Database);
        long revision;
        using (var owner = new ExpeditionOwner(world, Database))
        {
            await using var server = new ExpeditionPipeServer(owner, Database);
            var initial = (await client.RequestAsync(new(ExpeditionRequestKind.Initialize), timeout.Token)).Status!;
            await using var feed = client.WatchAsync(timeout.Token).GetAsyncEnumerator();
            Assert.That(await feed.MoveNextAsync(), Is.True);
            Assert.That(feed.Current.Revision, Is.EqualTo(initial.Revision));

            var reply = await client.RequestAsync(new(ExpeditionRequestKind.Command,
                ExpeditionTests.Move(FlightDirection.Right)), timeout.Token);
            Assert.That(reply.Result!.Applied, Is.True);
            Assert.That(reply.Status!.Revision, Is.EqualTo(initial.Revision + 1));
            Assert.That(await feed.MoveNextAsync(), Is.True);
            Assert.That(feed.Current.Drones.Single(drone => drone.Selected).Position.X, Is.EqualTo(30));
            revision = feed.Current.Revision;

            var invalid = await client.RequestAsync(new(ExpeditionRequestKind.Command, new FlightCommand(FlightAction.Move)), timeout.Token);
            Assert.That(invalid.Result!.Applied, Is.False);
            Assert.That(invalid.Status!.Revision, Is.EqualTo(revision));
        }

        using var reopened = new ExpeditionOwner(world, Database);
        await using var newServer = new ExpeditionPipeServer(reopened, Database);
        var status = (await client.RequestAsync(new(ExpeditionRequestKind.Status), timeout.Token)).Status!;
        Assert.That(status.Revision, Is.EqualTo(revision));
        Assert.That(status.Drones.Single(drone => drone.Selected).Position.X, Is.EqualTo(30));
    }

    private void Seed(WorldDefinition world, ExpeditionState state)
    {
        using var store = new ExpeditionStore(Database);
        store.Save(world.Id, 0, JsonSerializer.SerializeToUtf8Bytes(state));
    }
}
