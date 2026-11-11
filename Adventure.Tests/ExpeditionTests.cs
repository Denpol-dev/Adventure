using Adventure.Entities;
using Adventure.Entities.Actors;
using Adventure.Entities.Characters;
using Adventure.Entities.Items;
using Adventure.Entities.Worlds;
using Adventure.Expeditions;
using Foxnet.Session;
using NUnit.Framework;

namespace Adventure.Tests;

public sealed class ExpeditionTests
{
    [Test]
    public void DefaultExpeditionHasSixteenUniqueDronesAndOneServiceStation()
    {
        var engine = new ExpeditionEngine(Load());
        var state = engine.State;
        Assert.Multiple(() =>
        {
            Assert.That(state.Drones, Has.Count.EqualTo(16));
            Assert.That(state.Drones.Keys, Is.Unique);
            Assert.That(engine.Definition.Drones.Select(drone => drone.Color), Is.Unique);
            Assert.That(engine.Definition.Station.Bays.Select(bay => bay.Id), Is.Unique);
            Assert.That(engine.Definition.Station.Bays.Select(bay => bay.DroneId), Is.EquivalentTo(state.Drones.Keys));
            Assert.That(engine.Definition.Donors, Has.Count.EqualTo(12));
            Assert.That(engine.Definition.Donors.Select(donor => donor.Id), Is.Unique);
            Assert.That(state.Drones.Values.Where(drone => drone.Discovered).Select(drone => drone.Id), Is.EqualTo(new[] { "DR-01" }));
            Assert.That(engine.ActiveDrone.Battery!.Percent, Is.EqualTo(28));
            Assert.That(state.Drones.Values.All(drone => drone.Powered &&
                drone.Slots[EquipmentSlot.Locomotion].Kind == EquipmentKind.Wheels), Is.True);
            Assert.That(state.Drones.Values.SelectMany(drone => drone.Slots.Values).Select(module => module.Id), Is.Unique);
        });
    }

    [Test]
    public void APersonalGateAllowsOnlyTheAssignedDroneAndAClosedGateConsumesNoEnergy()
    {
        var engine = new ExpeditionEngine(Load());
        Step(engine, FlightDirection.Top, 9);
        Step(engine, FlightDirection.Left, 4);
        Assert.That(engine.ActiveDrone.Position.X, Is.EqualTo(39));
        Step(engine, FlightDirection.Right, 4);
        decimal energy = engine.ActiveDrone.Battery!.Energy;
        Assert.That(engine.Apply(Move(FlightDirection.Right)).Reply.Applied, Is.False);
        Assert.That(engine.ActiveDrone.Battery.Energy, Is.EqualTo(energy));
        Assert.That(engine.ActiveDrone.Position.X, Is.EqualTo(43));
    }

    [Test]
    public void StationHasSixteenSeparatedThreeCellBaysAndAnOutdoorStart()
    {
        var engine = new ExpeditionEngine(Load());
        var station = engine.Definition.Station;
        var level = engine.Levels[station.Origin.LevelId];
        Assert.That((level.Name, level.Map.Width, level.Map.Height), Is.EqualTo(("N24S56", 100, 28)));
        Assert.That(engine.ActiveDrone.Position, Is.EqualTo(new WorldPosition(level.Id, 43, 25)));
        Assert.That(engine.OnStation, Is.False);
        Assert.That(level.Map[43, 24].Symbol, Is.EqualTo('▢'));

        foreach (var bay in station.Bays)
        {
            var terminal = station.At(bay.TerminalX, bay.ParkingY);
            var parking = station.At(bay.ParkingX, bay.ParkingY);
            var gate = station.At(bay.GateX, bay.GateY);
            Assert.Multiple(() =>
            {
                Assert.That(Math.Abs(terminal.X - gate.X), Is.EqualTo(2));
                Assert.That((terminal.Y, parking.Y, gate.Y), Is.EqualTo((parking.Y, parking.Y, parking.Y)));
                Assert.That((level.Map[terminal.X, terminal.Y].Symbol, level.Map[parking.X, parking.Y].Symbol,
                    level.Map[gate.X, gate.Y].Symbol), Is.EqualTo(('░', '▢', '≈')));
                Assert.That(level.Map[terminal.X, terminal.Y].VisibleActor, Is.TypeOf<Terminal>());
                Assert.That(engine.State.Drones.Values.Any(drone => drone.Position == parking), Is.False);
            });
            foreach (int x in new[] { terminal.X, parking.X, gate.X })
            {
                Assert.That(level.Map[x, parking.Y - 1].CanEnter([]), Is.False);
                Assert.That(level.Map[x, parking.Y + 1].CanEnter([]), Is.False);
            }
        }
    }

    [Test]
    public void InitialSurveyKnowsBay01WithinTheOrbitalRadiusAndShowsOnlyTheStartLive()
    {
        var engine = new ExpeditionEngine(Load());
        var station = engine.Definition.Station;
        var bay = station.Bays.Single(candidate => candidate.Id == "BAY-01");
        var parking = station.At(bay.ParkingX, bay.ParkingY);
        var explored = engine.State.Explored[parking.LevelId];
        var map = engine.Levels[parking.LevelId].Map;

        Assert.That(engine.Definition.Balance.ScanRadius, Is.EqualTo(10));
        Assert.That(engine.ViewCell(engine.ActiveDrone.Position),
            Is.EqualTo(new CellView(CellVisibility.Visible, 'X', ConsoleColor.Cyan, "DR-01", true)));
        Assert.That(engine.ViewCell(station.At(bay.TerminalX, bay.ParkingY)).Glyph, Is.EqualTo('░'));
        Assert.That(engine.ViewCell(station.At(bay.GateX, bay.GateY)).Glyph, Is.EqualTo('≈'));
        Assert.That(engine.ViewCell(parking).Visibility, Is.EqualTo(CellVisibility.Remembered));
        Assert.That(engine.ViewCell(parking with { Y = parking.Y - 10 }).Glyph, Is.EqualTo('▒'));
        Assert.That(engine.ViewCell(parking with { Y = parking.Y - 11 }).Visibility, Is.EqualTo(CellVisibility.Unknown));
        Assert.That(engine.ViewCell(parking with { X = parking.X + 3 }).Visibility, Is.EqualTo(CellVisibility.Unknown));
        Assert.That(explored.Keys.All(index =>
        {
            var position = new WorldPosition(parking.LevelId, index % map.Width, index / map.Width);
            int dx = position.X - parking.X;
            int dy = position.Y - parking.Y;
            return position == engine.ActiveDrone.Position || dx * dx + dy * dy <= 100;
        }), Is.True);
    }

    [Test]
    public void BayDiagnosticsAndDispatcherLogAreReachableReadOnlyTerminals()
    {
        var engine = new ExpeditionEngine(Load());
        Navigate(engine, "N24S56", 38, 16);
        var bay = engine.Apply(new FlightCommand(FlightAction.Interact));
        Assert.That(bay.Reply.Menu, Is.Null);
        Assert.That(((Terminal)bay.Interaction!.Actor!).Message, Is.EqualTo("Назначенный дрон: DR-01."));

        Navigate(engine, "N24S56", 56, 20);
        for (int i = 0; i < 2; i++)
        {
            var response = engine.Apply(new FlightCommand(FlightAction.Interact));
            Assert.That(response.Interaction!.Result, Is.EqualTo(InteractionResult.Action));
            Assert.That(response.Reply.Menu, Is.Null);
            var log = (Terminal)response.Interaction.Actor!;
            Assert.Multiple(() =>
            {
                Assert.That(log.Name, Is.EqualTo("HAESEI / DRONE CONTROL"));
                Assert.That(log.Icon, Is.EqualTo("░"));
                Assert.That(log.LoadItem, Is.Null);
                Assert.That(log.IsAlien, Is.False);
                Assert.That(log.CompletesGame, Is.False);
                Assert.That(log.Message, Does.Contain("Пак Минсу, старший диспетчер\n\nПарк: 16 дронов")
                    .And.Contain("Рабочий день просто замечательный»."));
            });
        }
        Assert.That(engine.ActiveDrone.Items, Is.Empty);
        Assert.That(engine.State.TerminalUses["haesei-04-dispatch-log"], Is.EqualTo(2));
    }

    [Test]
    public void ModesKeepIndependentPositionsAndRespectQuadrantEdges()
    {
        var engine = At("N24S55", 98, 8);
        var drone = engine.ActiveDrone;
        Step(engine, FlightDirection.Right);
        Assert.That(engine.State.Laser, Is.EqualTo(drone.Position));

        Apply(engine, FlightAction.CycleMode);
        Assert.That(engine.State.Mode, Is.EqualTo(ControlMode.Drone));
        var oldAim = engine.State.Laser;
        Step(engine, FlightDirection.Right);
        Assert.That(engine.Levels[drone.Position.LevelId].Name, Is.EqualTo("N24S56"));
        Assert.That((drone.Position.X, drone.Position.Y), Is.EqualTo((0, 8)));
        Assert.That(engine.State.Laser, Is.EqualTo(oldAim));

        Apply(engine, FlightAction.CycleMode);
        Assert.That(engine.State.Mode, Is.EqualTo(ControlMode.Laser));
        var oldDrone = drone.Position;
        decimal energy = drone.Battery!.Energy;
        Step(engine, FlightDirection.Right, 3);
        Assert.That(engine.State.Laser.X, Is.EqualTo(2));
        Assert.That(drone.Position, Is.EqualTo(oldDrone));
        Assert.That(drone.Battery!.Energy, Is.EqualTo(energy));

        Apply(engine, FlightAction.CycleMode);
        Assert.That(engine.State.Mode, Is.EqualTo(ControlMode.Combined));
        Assert.That(engine.State.Laser, Is.EqualTo(drone.Position));
    }

    [Test]
    public void OrbitalSurveyRespectsObstructionsAndKeepsPreviouslySurveyedCells()
    {
        var world = Load();
        var level = world.Levels.Single(level => level.Name == "N24S55");
        level.Cells.Add(new CellProperties { X = 42, Y = 14, BlocksScanning = true });
        var engine = At(world, "N24S55", 40, 14);
        var start = engine.ActiveDrone.Position;
        Assert.That(engine.ViewCell(start with { X = 42 }).Visibility, Is.EqualTo(CellVisibility.Visible));
        Assert.That(engine.ViewCell(start with { X = 43 }).Visibility, Is.EqualTo(CellVisibility.Unknown));
        Assert.That(engine.ViewCell(start with { X = 43 }).Glyph, Is.EqualTo(' '));

        Step(engine, FlightDirection.Left);
        Apply(engine, FlightAction.CycleMode);
        Apply(engine, FlightAction.CycleMode);
        Step(engine, FlightDirection.Left, 12);
        Assert.That(engine.ViewCell(start).Visibility, Is.EqualTo(CellVisibility.Remembered));
        Assert.That(engine.ViewCell(start).Glyph, Is.EqualTo('.'));
    }

    [Test]
    public void OrbitalRoofNeverRevealsInteriorButDroneCanMoveBlindAndBeRendered()
    {
        var engine = At("N23S56", 24, 12);
        Step(engine, FlightDirection.Right);
        var drone = engine.ActiveDrone;
        var inside = drone.Position with { X = 26 };
        Assert.Multiple(() =>
        {
            Assert.That(drone.Position.X, Is.EqualTo(25));
            Assert.That(engine.ViewCell(drone.Position).Glyph, Is.EqualTo('X'));
            Assert.That(engine.ViewCell(inside).Glyph, Is.EqualTo('▒'));
            Assert.That(drone.LidarScanned, Is.False);
        });

        Apply(engine, FlightAction.CycleMode);
        Apply(engine, FlightAction.CycleMode);
        Step(engine, FlightDirection.Right);
        Assert.That(engine.ViewCell(inside).Glyph, Is.EqualTo('▒'));
        Assert.That(engine.ViewCell(inside).DroneId, Is.Null);
    }

    [Test]
    public void InstalledLidarRevealsInteriorForAllDronesAndChargesForCompletedScans()
    {
        var world = Load();
        world.Levels.Single(level => level.Name == "N23S56").Cells.Add(
            new CellProperties { X = 27, Y = 12, BlocksScanning = true });
        var engine = new ExpeditionEngine(world);
        engine = Relocate(world, engine.State, engine.StationTerminal);
        engine.ActiveDrone.Cargo.Add(new ModuleState("test-lidar", EquipmentKind.Lidar, 0, 0));
        Apply(engine, FlightAction.Install, id: "test-lidar", slot: EquipmentSlot.Lidar);
        Assert.That(engine.ActiveDrone.LidarScanned, Is.False);

        engine = Relocate(world, engine.State, "N23S56", 24, 12);
        var drone = engine.ActiveDrone;
        decimal energy = drone.Battery!.Energy;
        Step(engine, FlightDirection.Right);
        var floor = drone.Position with { X = 26 };
        Assert.That(engine.ViewCell(floor).Glyph, Is.EqualTo('▢'));
        Assert.That(engine.ViewCell(floor with { X = 28 }).Visibility, Is.EqualTo(CellVisibility.Unknown));
        Assert.That(drone.Battery!.Energy, Is.EqualTo(energy - engine.Definition.Balance.WheelMove - engine.Definition.Balance.LidarScan));

        engine = Relocate(world, engine.State, engine.StationTerminal);
        Apply(engine, FlightAction.Remove, slot: EquipmentSlot.Lidar);
        Assert.That(engine.ViewCell(floor).Visibility, Is.EqualTo(CellVisibility.Remembered));
        Assert.That(engine.ViewCell(floor).Glyph, Is.EqualTo('▢'));
        Assert.That(engine.ActiveDrone.Cargo.Single().Id, Is.EqualTo("test-lidar"));
    }

    [Test]
    public void LaserCanFindAnotherDroneAfterTheSelectedDroneIsDepleted()
    {
        var engine = At("N23S55", 64, 12);
        var drone = engine.ActiveDrone;
        SetEnergy(drone, 0.01m);
        Step(engine, FlightDirection.Right);
        Assert.That(drone.Battery!.Energy, Is.Zero);
        Assert.That(engine.Apply(Move(FlightDirection.Right)).Reply.Applied, Is.False);
        Assert.That(engine.State.GameOver, Is.False);

        Apply(engine, FlightAction.CycleMode);
        Apply(engine, FlightAction.CycleMode);
        Step(engine, FlightDirection.Right, 8);
        Assert.That(engine.State.Drones["DR-02"].Discovered, Is.True);
        var stopped = drone.Position;
        Apply(engine, FlightAction.SelectDrone, id: "DR-02");
        Assert.That(engine.ActiveDrone.Id, Is.EqualTo("DR-02"));
        Assert.That(drone.Position, Is.EqualTo(stopped));
    }

    [Test]
    public void TheUndergroundRouteUsesLidarAndItsControlsRequireOrderedWork()
    {
        var engine = At("N24S56", 47, 26);
        Apply(engine, FlightAction.Salvage, id: "DN-01-01");
        Navigate(engine, "N24S56", 41, 21);
        Apply(engine, FlightAction.Install, "DN-01-01", EquipmentSlot.Lidar);
        Navigate(engine, "N23S57", 25, 8);
        var surface = engine.ActiveDrone.Position;
        Command(FlightAction.SignInTerminal, "deep-entrance", login: "shaft.eng", password: "depth-04");
        Assert.That(engine.Apply(new(FlightAction.OperateTerminal, Id: "deep-entrance", Slot: "shaft-down")).Reply.Applied,
            Is.False);
        Command(FlightAction.ReadTerminalFile, "deep-entrance", "shaft-inspection");
        Command(FlightAction.OperateTerminal, "deep-entrance", "shaft-unlock");
        Command(FlightAction.OperateTerminal, "deep-entrance", "shaft-down");
        Assert.That(engine.Levels[engine.ActiveDrone.Position.LevelId].Name, Is.EqualTo("U11"));
        Assert.That(engine.State.Laser, Is.EqualTo(surface));
        var near = engine.ActiveDrone.Position with { X = 11 };
        Assert.That(engine.ViewCell(near).Visibility, Is.EqualTo(CellVisibility.Visible));
        Apply(engine, FlightAction.CycleMode);
        Apply(engine, FlightAction.CycleMode);
        Assert.That(engine.ViewCell(near).Visibility, Is.EqualTo(CellVisibility.Remembered));
        Assert.That(engine.ViewPosition.LevelId, Is.EqualTo(surface.LevelId));
        Apply(engine, FlightAction.CycleMode);
        Assert.That(engine.ViewCell(near).Visibility, Is.EqualTo(CellVisibility.Visible));

        Navigate(engine, "U11", 50, 14);
        Command(FlightAction.OperateTerminal, "deep-distributor", "deep-power");
        Navigate(engine, "U21", 18, 14);
        Command(FlightAction.ReadTerminalFile, "deep-recorder", "experiment-sequence");
        Navigate(engine, "U12", 50, 13);
        Command(FlightAction.OperateTerminal, "deep-calibration", "deep-calibrate");
        Navigate(engine, "U22", 18, 14);
        Assert.That(engine.Apply(new(FlightAction.ReadTerminalFile, Id: "deep-central", Slot: "core-sample")).Reply.Applied,
            Is.False);
        engine.State.Operations.Add("orbit-link");
        Command(FlightAction.ReadTerminalFile, "deep-central", "core-sample");
        Command(FlightAction.ReadTerminalFile, "deep-central", "core-warnings");
        Command(FlightAction.OperateTerminal, "deep-central", "deep-passive");
        Assert.That(engine.Apply(new(FlightAction.OperateTerminal, Id: "deep-central", Slot: "deep-passive")).Reply.Applied,
            Is.False);
        Navigate(engine, "U11", 10, 14);
        Command(FlightAction.OperateTerminal, "deep-return", "shaft-up");
        Assert.That(engine.ActiveDrone.Position, Is.EqualTo(surface));

        void Command(FlightAction action, string id, string? slot = null, string? login = null, string? password = null)
        {
            var result = engine.Apply(new(action, Id: id, Slot: slot, Login: login, Password: password)).Reply;
            Assert.That(result.Applied, Is.True, result.Message);
        }
    }

    [Test]
    public void TheLastDepletedBatteryEndsTheExpedition()
    {
        var engine = At("N24S55", 40, 14);
        foreach (var drone in engine.State.Drones.Values)
            SetEnergy(drone, drone == engine.ActiveDrone ? 0.01m : 0);
        Step(engine, FlightDirection.Right);
        Assert.That(engine.State.GameOver, Is.True);
        Assert.That(engine.Apply(new FlightCommand(FlightAction.CycleMode)).Reply.Applied, Is.False);
    }

    [Test]
    public void StationSalvageAndSwapsPreserveOwnershipWeightAndBatteryCharge()
    {
        var world = Load();
        var engine = At(world, "N24S56", 47, 26);
        var donor = engine.Definition.Donors.Single(donor => donor.Id == "DN-01");
        var drone = engine.ActiveDrone;
        var initial = drone.Battery!;
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Interact)).Reply.Menu, Is.EqualTo("donor:DN-01"));
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Charge)).Reply.Applied, Is.False);
        Apply(engine, FlightAction.Salvage, id: donor.Components[1].Id);
        Assert.That(drone.Battery!.Energy, Is.EqualTo(initial.Energy - engine.Definition.Balance.ToolUse));
        Assert.That(drone.Cargo.Single().Energy, Is.EqualTo(190));
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Salvage, Id: donor.Components[1].Id)).Reply.Applied, Is.False);
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Install, Id: donor.Components[1].Id,
            Slot: nameof(EquipmentSlot.Battery))).Reply.Applied, Is.False);

        Navigate(engine, "N24S56", 41, 21);
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Interact)).Reply.Menu, Is.EqualTo("station"));
        decimal previousEnergy = drone.Battery.Energy;
        Apply(engine, FlightAction.Install, id: donor.Components[1].Id, slot: EquipmentSlot.Battery);
        Assert.That(drone.Battery!.Energy, Is.EqualTo(190));
        Assert.That(drone.Cargo.Single().Energy, Is.EqualTo(previousEnergy));
        Apply(engine, FlightAction.Charge);
        Assert.That(drone.Battery.Energy, Is.EqualTo(190 + engine.Definition.Balance.StationCharge));
        Apply(engine, FlightAction.Install, id: initial.Id, slot: EquipmentSlot.Battery);
        Assert.That(drone.Battery!.Energy, Is.EqualTo(previousEnergy));
        Assert.That(drone.Cargo.Single().Energy, Is.EqualTo(190 + engine.Definition.Balance.StationCharge));
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Remove, Slot: nameof(EquipmentSlot.Locomotion))).Reply.Applied, Is.False);
    }

    [Test]
    public void AReplacementThatWouldOverloadTheDroneKeepsBothModulesInPlace()
    {
        var engine = At("N24S56", 41, 21);
        var drone = engine.ActiveDrone;
        drone.Slots[EquipmentSlot.Battery] = new ModuleState("large", EquipmentKind.HighCapacityBattery, 1600, 200);
        drone.Cargo.Add(new ModuleState("small", EquipmentKind.Battery, 1000, 100));
        for (int i = 0; i < 4; i++)
            drone.Cargo.Add(new ModuleState($"body-{i}", EquipmentKind.BodyModule, 0, 0));
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Install, Id: "small", Slot: nameof(EquipmentSlot.Battery))).Reply.Applied,
            Is.False);
        Assert.That(drone.Battery!.Id, Is.EqualTo("large"));
        Assert.That(drone.Cargo.Count(module => module.Id == "small"), Is.EqualTo(1));
    }

    [Test]
    public void BiometricAccessIsAnEquipmentCapability()
    {
        var engine = At("N24S56", 41, 21);
        engine.ActiveDrone.Cargo.Add(new ModuleState("eye", EquipmentKind.BiometricEye, 0, 0));
        Assert.That(engine.ActiveDrone.HasBiometricAccess, Is.False);
        Apply(engine, FlightAction.Install, id: "eye", slot: EquipmentSlot.BiometricEye);
        Assert.That(engine.ActiveDrone.HasBiometricAccess, Is.True);
        Apply(engine, FlightAction.Remove, slot: EquipmentSlot.BiometricEye);
        Assert.That(engine.ActiveDrone.HasBiometricAccess, Is.False);
        Assert.That(engine.ActiveDrone.Cargo.Single().Id, Is.EqualTo("eye"));
    }

    [Test]
    public void StoryFieldsAndXd23ChainStillWorkAndChestCannotBeLootedAfterConversion()
    {
        var world = Load();
        var engine = At(world, "N23S56", 47, 11);
        Apply(engine, FlightAction.Interact);
        Assert.That(engine.ActiveDrone.Items, Contains.Item(ItemKind.HardDriveXD23));

        var field = world.Levels.Single(level => level.Name == "N23S57").Objects.First(obj => obj.Kind == ObjectKind.ForceField);
        engine = Relocate(world, engine.State, "N23S57", field.X, field.Y - 1);
        Assert.That(engine.Apply(Move(FlightDirection.Bottom)).Reply.Applied, Is.False);
        engine = Relocate(world, engine.State, "N23S57", 57, 2);
        Apply(engine, FlightAction.Interact);
        Assert.That(engine.ActiveDrone.Items, Contains.Item(ItemKind.ArtifactY66));
        engine = Relocate(world, engine.State, "N23S57", field.X, field.Y - 1);
        Step(engine, FlightDirection.Bottom);

        engine = Relocate(world, engine.State, "N23S57", 29, 15);
        Apply(engine, FlightAction.Interact);
        Assert.That(engine.ActiveDrone.Items, Contains.Item(ItemKind.HardDriveXD23Full));
        engine = Relocate(world, engine.State, "N23S56", 47, 11);
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Interact)).Interaction!.Result, Is.EqualTo(InteractionResult.EmptyChest));

        engine = Relocate(world, engine.State, "N24S58", 45, 15);
        Apply(engine, FlightAction.Interact);
        Assert.That(engine.ActiveDrone.Items, Contains.Item(ItemKind.UniversalPass));
        engine = Relocate(world, engine.State, "N25S57", 72, 22);
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Interact)).Interaction!.Result, Is.EqualTo(InteractionResult.Completed));
        Assert.That(engine.ActiveDrone.Items, Does.Contain(ItemKind.HardDriveXD23).And.Not.Contain(ItemKind.HardDriveXD23Full));
    }

    [Test]
    public void StarterCanCompleteTheExistingStoryFromTheStationWithoutTeleporting()
    {
        var engine = new ExpeditionEngine(Load());
        Navigate(engine, "N23S56", 47, 11);
        Apply(engine, FlightAction.Interact);
        Navigate(engine, "N23S57", 57, 2);
        Apply(engine, FlightAction.Interact);
        Navigate(engine, "N23S57", 29, 15);
        Apply(engine, FlightAction.Interact);
        Navigate(engine, "N24S58", 45, 15);
        Apply(engine, FlightAction.Interact);
        Navigate(engine, "N25S57", 72, 22);
        Assert.That(engine.Apply(new FlightCommand(FlightAction.Interact)).Interaction!.Result,
            Is.EqualTo(InteractionResult.Completed));
        Assert.That(engine.ActiveDrone.Powered, Is.True);
    }

    [Test]
    public void WorldExportKeepsStationLayoutAndInteriorMetadata()
    {
        var world = Load();
        var directory = Directory.CreateTempSubdirectory("foxnet-world-expedition-").FullName;
        try
        {
            var path = Path.Combine(directory, "world.json");
            WorldFiles.Save(path, world);
            var restored = WorldFiles.Load(path);
            var engine = new ExpeditionEngine(restored);
            Assert.That(engine.Definition.Station, Is.EqualTo(world.Expedition!.Station with { Bays = engine.Definition.Station.Bays }));
            Assert.That(engine.Definition.Station.Bays, Is.EqualTo(world.Expedition.Station.Bays));
            Assert.That(restored.Levels.Single(level => level.Name == "N24S56").Rows,
                Is.EqualTo(world.Levels.Single(level => level.Name == "N24S56").Rows));
            Assert.That(engine.Definition.Interiors, Is.EqualTo(world.Expedition.Interiors));
            Assert.That(engine.ActiveDrone.Id, Is.EqualTo("DR-01"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    internal static void Navigate(ExpeditionEngine engine, string levelName, int x, int y,
        Action<FlightDirection>? move = null)
    {
        var drone = engine.ActiveDrone;
        var target = new WorldPosition(engine.Levels.Values.Single(level => level.Name == levelName).Id, x, y);
        var start = drone.Position;
        var station = engine.Definition.Station;
        var items = new List<Item>();
        if (drone.Items.Contains(ItemKind.ArtifactY66))
            items.Add(new ArtifactY66());
        if (drone.Items.Contains(ItemKind.UniversalPass))
            items.Add(new UniversalPass());

        var visited = new Dictionary<WorldPosition, (WorldPosition Previous, FlightDirection Direction)>
        {
            [start] = default
        };
        var queue = new Queue<WorldPosition>();
        queue.Enqueue(start);
        (int X, int Y, Vector Vector, FlightDirection Direction)[] steps =
        [
            (-1, 0, Vector.Left, FlightDirection.Left), (1, 0, Vector.Right, FlightDirection.Right),
            (0, -1, Vector.Top, FlightDirection.Top), (0, 1, Vector.Bottom, FlightDirection.Bottom)
        ];
        while (queue.TryDequeue(out var position) && position != target)
        {
            foreach (var (dx, dy, vector, direction) in steps)
            {
                var level = engine.Levels[position.LevelId];
                int nextX = position.X + dx;
                int nextY = position.Y + dy;
                if (!level.Map.Contains(nextX, nextY))
                {
                    var nextLevel = level.GetNeighbor(vector);
                    if (nextLevel == null)
                        continue;
                    level = nextLevel;
                    nextX = dx switch { -1 => level.Map.Width - 1, 1 => 0, _ => nextX };
                    nextY = dy switch { -1 => level.Map.Height - 1, 1 => 0, _ => nextY };
                }
                var next = new WorldPosition(level.Id, nextX, nextY);
                if (level.Map.Contains(nextX, nextY) && Passable(next) && visited.TryAdd(next, (position, direction)))
                    queue.Enqueue(next);
            }
        }

        Assert.That(visited.ContainsKey(target), Is.True, $"No route to {levelName} ({x}, {y}).");
        var route = new Stack<FlightDirection>();
        for (var current = target; current != start; current = visited[current].Previous)
            route.Push(visited[current].Direction);
        foreach (var direction in route)
        {
            if (move == null)
                Step(engine, direction);
            else
                move(direction);
        }
        Assert.That(drone.Position, Is.EqualTo(target));

        bool Passable(WorldPosition position)
        {
            if (station.Contains(position) && station.Bays.FirstOrDefault(
                bay => station.At(bay.GateX, bay.GateY) == position) is { } gate)
                return gate.DroneId == drone.Id;
            return engine.Levels[position.LevelId].Map[position.X, position.Y].CanEnter(items);
        }
    }

    internal static WorldDefinition Load() => WorldFiles.Load(WorldFiles.DefaultPath);

    internal static ExpeditionEngine At(string level, int x, int y) => At(Load(), level, x, y);

    internal static ExpeditionEngine At(WorldDefinition world, string level, int x, int y) =>
        Relocate(world, new ExpeditionEngine(world).State, level, x, y);

    internal static ExpeditionEngine Relocate(WorldDefinition world, ExpeditionState state, string level, int x, int y) =>
        Relocate(world, state, new WorldPosition(world.Levels.Single(candidate => candidate.Name == level).Id, x, y));

    internal static ExpeditionEngine Relocate(WorldDefinition world, ExpeditionState state, WorldPosition position)
    {
        state.AwaitingFirstSurvey = false;
        state.Laser = position;
        state.Drones[state.SelectedDroneId].Position = position;
        return new ExpeditionEngine(world, state);
    }

    internal static FlightCommand Move(FlightDirection direction) => new(FlightAction.Move, direction);

    internal static void Step(ExpeditionEngine engine, FlightDirection direction, int count = 1)
    {
        for (int i = 0; i < count; i++)
        {
            var result = engine.Apply(Move(direction)).Reply;
            Assert.That(result.Applied, Is.True, result.Message);
        }
    }

    internal static void Apply(ExpeditionEngine engine, FlightAction action, string? id = null, EquipmentSlot? slot = null)
    {
        var result = engine.Apply(new FlightCommand(action, Id: id, Slot: slot?.ToString())).Reply;
        Assert.That(result.Applied, Is.True, result.Message);
    }

    internal static void SetEnergy(DroneState drone, decimal energy) =>
        drone.Slots[EquipmentSlot.Battery] = drone.Battery! with { Energy = energy };
}
