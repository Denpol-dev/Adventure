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
        desktop.EnterWorkspace();
        desktop.SetAvatar([1, 2, 3]);
        var message = desktop.OpenConversation("unknown").Single();
        desktop.Documents.AddFromAttachment(message.Attachments.Single().EventId);
        var device = Scenario.LoadDefault().Devices.Single();
        Assert.That(desktop.Authorize(device.Id, device.Credentials.Login, device.Credentials.Password), Is.True);

        using (var connection = new SqliteConnection($"Data Source={Database};Pooling=False"))
        {
            connection.Open();
            using var sql = connection.CreateCommand();
            sql.CommandText = """
                DROP TABLE corporate_pressure;
                DROP TABLE world_settings;
                DROP TABLE notifications;
                DROP TABLE value_sales;
                DROP TABLE registered_values;
                DROP TABLE payment_requests;
                DROP TABLE contract_payments;
                DROP TABLE npc_ledger;
                DROP TABLE bank_entries;
                DROP TABLE npc_wallets;
                DROP TABLE player_wallet;
                DROP TABLE story_events;
                DROP TABLE story_flags;
                DROP TABLE independent_replies;
                DROP TABLE contact_history;
                DROP TABLE contact_facts;
                DROP TABLE contact_state;
                DROP TABLE expeditions;
                PRAGMA user_version = 2;
                """;
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
    public void DroppedCargoStaysAtItsCellAndKeepsItsIdentityAcrossDronesAndRestarts()
    {
        var world = ExpeditionTests.Load();
        var engine = ExpeditionTests.At(world, "N24S56", 47, 26);
        var donor = engine.Definition.Donors.Single(candidate => candidate.Id == "DN-01");
        var position = engine.ActiveDrone.Position;
        var other = engine.State.Drones["DR-02"];
        other.Discovered = true;
        other.Position = position with { X = 48 };
        Seed(world, engine.State);

        string crateId;
        ModuleState battery;
        using (var owner = new ExpeditionOwner(world, Database))
        {
            foreach (var component in donor.Components)
                Assert.That(owner.Execute(new(FlightAction.Salvage, Id: component.Id)).Result.Reply.Applied, Is.True);
            battery = owner.Read(runtime => runtime.ActiveDrone.Cargo.Single(module => module.Capacity > 0));
            var ids = donor.Components.Select(component => component.Id).ToArray();
            Assert.That(owner.Execute(new(FlightAction.Drop, Items: [ids[0], owner.Read(runtime =>
                runtime.ActiveDrone.Battery!.Id)])).Result.Reply.Applied, Is.False);
            Assert.That(owner.Execute(new(FlightAction.Drop, Items: ids)).Result.Reply.Applied, Is.True);
            crateId = owner.Read(runtime => runtime.CrateHere!.Id);
            Assert.That(owner.Execute(new(FlightAction.Interact)).Result.Reply.Menu, Is.EqualTo("targets"));
            Assert.That(owner.Execute(new(FlightAction.Take, Id: crateId, Items: [ids[0]])).Result.Reply.Applied, Is.True);
            Assert.That(owner.Execute(new(FlightAction.Drop, Items: [ids[0]])).Result.Reply.Applied, Is.True);
            Assert.That(owner.Read(runtime => runtime.State.DropCrates.Single().Id), Is.EqualTo(crateId));

            Assert.That(owner.Execute(ExpeditionTests.Move(FlightDirection.Left)).Result.Reply.Applied, Is.True);
            Assert.That(owner.Read(runtime => runtime.ViewCell(position).Glyph), Is.EqualTo('■'));
            Assert.That(owner.Execute(new(FlightAction.SelectDrone, Id: other.Id)).Result.Reply.Applied, Is.True);
            Assert.That(owner.Execute(new(FlightAction.Take, Id: crateId)).Result.Reply.Applied, Is.False);
            Assert.That(owner.Execute(ExpeditionTests.Move(FlightDirection.Left)).Result.Reply.Applied, Is.True);
            Assert.That(owner.Execute(new(FlightAction.Take, Id: crateId, Items: [ids[0]])).Result.Reply.Applied, Is.True);
        }

        using var restored = new ExpeditionOwner(world, Database);
        Assert.Multiple(() =>
        {
            Assert.That(restored.Read(runtime => runtime.CrateHere!.Id), Is.EqualTo(crateId));
            Assert.That(restored.Read(runtime => runtime.CrateHere!.Modules.Single()), Is.EqualTo(battery));
        });
        Assert.That(restored.Execute(new(FlightAction.Take, Id: crateId)).Result.Reply.Applied, Is.True);
        Assert.That(restored.Read(runtime => runtime.State.DropCrates), Is.Empty);
        Assert.That(restored.Read(runtime => runtime.ActiveDrone.Cargo), Does.Contain(battery));
        Assert.That(restored.Execute(new(FlightAction.Take, Id: crateId)).Result.Reply.Applied, Is.False);
    }

    [Test]
    public void TakeAllFillsRemainingCapacityWithoutMovingAnOverweightBattery()
    {
        var engine = ExpeditionTests.At("N24S56", 47, 26);
        var drone = engine.ActiveDrone;
        var donor = engine.Definition.Donors.Single(candidate => candidate.Id == "DN-01");
        foreach (var component in donor.Components)
            ExpeditionTests.Apply(engine, FlightAction.Salvage, id: component.Id);
        var ids = drone.Cargo.Select(module => module.Id).ToArray();
        Assert.That(engine.Apply(new(FlightAction.Drop, Items: ids)).Reply.Applied, Is.True);
        for (int index = 0; index < engine.CargoCapacity - 1; index++)
            drone.Goods.Add(new CargoItem($"key-{index}", ItemKind.Key));
        var crate = engine.CrateHere!;
        Assert.That(engine.Apply(new(FlightAction.Take, Id: crate.Id, Items: ids)).Reply.Applied, Is.False);
        Assert.That(crate.Modules, Has.Count.EqualTo(2));
        Assert.That(engine.Apply(new(FlightAction.Take, Id: crate.Id)).Reply.Applied, Is.True);
        Assert.That(engine.CargoWeight, Is.EqualTo(engine.CargoCapacity));
        Assert.That(crate.Modules.Single().Id, Is.EqualTo(donor.Components[1].Id));
    }

    [Test]
    public void RestartRestoresFleetModeEquipmentDonorsAndSurveyWithoutCreatingANewRun()
    {
        var world = ExpeditionTests.Load();
        var engine = ExpeditionTests.At(world, "N24S56", 47, 26);
        string battery = engine.Definition.Donors.First().Components[1].Id;
        ExpeditionTests.Apply(engine, FlightAction.Salvage, id: battery);
        engine = ExpeditionTests.Relocate(world, engine.State, engine.StationTerminal);
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
    public void StartingSurveyPersistsOnRestartAndDoesNotRunAgain()
    {
        var world = ExpeditionTests.Load();
        var bay = world.Expedition!.Station.Bays.Single(candidate => candidate.Id == "BAY-01");
        var parking = world.Expedition.Station.At(bay.ParkingX, bay.ParkingY);
        int initialCells;
        using (var owner = new ExpeditionOwner(world, Database))
        {
            Assert.That(owner.WasRestored, Is.False);
            initialCells = owner.Status().ExploredCells;
        }

        long movedRevision;
        string surveyed;
        using (var owner = new ExpeditionOwner(world, Database))
        {
            Assert.That(owner.WasRestored, Is.True);
            Assert.That(owner.Revision, Is.EqualTo(1));
            Assert.That(owner.Status().ExploredCells, Is.EqualTo(initialCells));
            Assert.That(owner.Read(engine => engine.ViewCell(parking).Visibility), Is.EqualTo(CellVisibility.Remembered));
            Assert.That(owner.Execute(ExpeditionTests.Move(FlightDirection.Top)).Result.Reply.Applied, Is.True);
            Assert.That(owner.Read(engine => engine.State.AwaitingFirstSurvey), Is.False);
            Assert.That(owner.Status().ExploredCells, Is.GreaterThan(initialCells));
            movedRevision = owner.Revision;
            surveyed = owner.Read(engine => JsonSerializer.Serialize(engine.State.Explored));
        }

        using var restored = new ExpeditionOwner(world, Database);
        Assert.That(restored.Revision, Is.EqualTo(movedRevision));
        Assert.That(restored.Read(engine => engine.State.AwaitingFirstSurvey), Is.False);
        Assert.That(restored.Read(engine => JsonSerializer.Serialize(engine.State.Explored)), Is.EqualTo(surveyed));
    }

    [Test]
    public void LegacyLayoutMovesOnlyTheDroneCaughtInNewConstructionAndKeepsProgress()
    {
        var world = ExpeditionTests.Load();
        var engine = new ExpeditionEngine(world);
        var oldState = engine.State;
        Guid oldLevel = world.Levels.Single(level => level.Name == "N23S55").Id;
        Guid newLevel = world.Levels.Single(level => level.Name == "N24S56").Id;
        var oldStarter = new WorldPosition(oldLevel, 29, 2);
        var reachableHall = new WorldPosition(newLevel, 43, 21);
        oldState.Drones["DR-01"].Position = oldStarter;
        oldState.Drones["DR-06"].Position = reachableHall;
        var selected = oldState.Drones["DR-07"];
        selected.Discovered = true;
        selected.Position = new WorldPosition(newLevel, 38, 16);
        selected.LegacyItems = [ItemKind.HardDriveXD23Full];
        ExpeditionTests.SetEnergy(selected, 17.4m);
        oldState.SelectedDroneId = selected.Id;
        oldState.Laser = selected.Position;
        oldState.Explored.Clear();
        oldState.Explored.Add(oldLevel, new() { [229] = new ObservedCell('▢', ConsoleColor.Gray, true) });
        oldState.TerminalUses.Add("existing-terminal", 3);
        oldState.Station.ChargeCycles = 2;
        oldState.Donors["DN-01"].RemoveAt(0);
        oldState.Looted.Add("existing-chest");
        var legacy = JsonSerializer.SerializeToNode(oldState)!.AsObject();
        legacy.Remove(nameof(ExpeditionState.LayoutRevision));
        legacy.Remove(nameof(ExpeditionState.AwaitingFirstSurvey));
        using (var store = new ExpeditionStore(Database))
            store.Save(world.Id, 0, JsonSerializer.SerializeToUtf8Bytes(legacy));

        WorldPosition migratedPosition;
        using (var owner = new ExpeditionOwner(world, Database))
        {
            Assert.That(owner.Revision, Is.EqualTo(2));
            var migrated = owner.Read(runtime => runtime.State);
            migratedPosition = migrated.Drones[selected.Id].Position;
            Assert.Multiple(() =>
            {
                Assert.That(migrated.LayoutRevision, Is.EqualTo(world.Expedition!.LayoutRevision));
                Assert.That(migrated.Drones["DR-01"].Position, Is.EqualTo(oldStarter));
                Assert.That(migrated.Drones["DR-06"].Position, Is.EqualTo(reachableHall));
                Assert.That(migratedPosition, Is.EqualTo(new WorldPosition(newLevel, 36, 16)));
                Assert.That(migrated.Laser, Is.EqualTo(migratedPosition));
                Assert.That(owner.Read(runtime => runtime.OnStation), Is.False);
                Assert.That(migrated.SelectedDroneId, Is.EqualTo(selected.Id));
                Assert.That(migrated.Drones[selected.Id].Battery!.Energy, Is.EqualTo(17.4m));
                Assert.That(migrated.Drones[selected.Id].Items, Is.EqualTo(selected.LegacyItems));
                Assert.That(migrated.Explored.Keys, Is.EqualTo(new[] { oldLevel }));
                Assert.That(migrated.Explored[oldLevel][229], Is.EqualTo(oldState.Explored[oldLevel][229]));
                Assert.That(migrated.TerminalUses["existing-terminal"], Is.EqualTo(3));
                Assert.That(migrated.Station.ChargeCycles, Is.EqualTo(2));
                Assert.That(migrated.Donors["DN-01"], Is.EqualTo(oldState.Donors["DN-01"]));
                Assert.That(migrated.Looted, Contains.Item("existing-chest"));
            });
        }

        using var restored = new ExpeditionOwner(world, Database);
        Assert.That(restored.Revision, Is.EqualTo(2));
        Assert.That(restored.Read(runtime => runtime.ActiveDrone.Position), Is.EqualTo(migratedPosition));
    }

    [Test]
    public void AnUnsupportedSavedLayoutDoesNotReplaceTheSave()
    {
        var world = ExpeditionTests.Load();
        var state = new ExpeditionEngine(world).State;
        state.LayoutRevision = world.Expedition!.LayoutRevision + 1;
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(state);
        using (var store = new ExpeditionStore(Database))
            store.Save(world.Id, 0, payload);

        Assert.Throws<InvalidDataException>(() => new ExpeditionOwner(world, Database));

        using var reopened = new ExpeditionStore(Database);
        Assert.That(reopened.Load(world.Id)!.Revision, Is.EqualTo(1));
        Assert.That(reopened.Load(world.Id)!.Payload, Is.EqualTo(payload));
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
            Assert.That(feed.Current.Drones.Single(drone => drone.Selected).Position.X, Is.EqualTo(44));
            revision = feed.Current.Revision;

            var invalid = await client.RequestAsync(new(ExpeditionRequestKind.Command, new FlightCommand(FlightAction.Move)), timeout.Token);
            Assert.That(invalid.Result!.Applied, Is.False);
            Assert.That(invalid.Status!.Revision, Is.EqualTo(revision));
        }

        using var reopened = new ExpeditionOwner(world, Database);
        await using var newServer = new ExpeditionPipeServer(reopened, Database);
        var status = (await client.RequestAsync(new(ExpeditionRequestKind.Status), timeout.Token)).Status!;
        Assert.That(status.Revision, Is.EqualTo(revision));
        Assert.That(status.Drones.Single(drone => drone.Selected).Position.X, Is.EqualTo(44));
    }

    private void Seed(WorldDefinition world, ExpeditionState state)
    {
        using var store = new ExpeditionStore(Database);
        store.Save(world.Id, 0, JsonSerializer.SerializeToUtf8Bytes(state));
    }
}
