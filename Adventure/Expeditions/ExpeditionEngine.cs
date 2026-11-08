using Adventure.Entities;
using Adventure.Entities.Actors;
using Adventure.Entities.Characters;
using Adventure.Entities.Items;
using Adventure.Entities.Levels;
using Adventure.Entities.Worlds;
using Foxnet.Session;

namespace Adventure.Expeditions;

public sealed record EngineResult(FlightResult Reply, Interaction? Interaction = null);

public sealed partial class ExpeditionEngine
{
    private readonly ExpeditionMap _map;
    private readonly Dictionary<string, DroneDefinition> _definitions;
    private readonly Dictionary<WorldPosition, DonorDefinition> _donors;
    private readonly Dictionary<WorldPosition, ObservedCell> _visible = [];

    public ExpeditionEngine(WorldDefinition world, ExpeditionState? state = null)
    {
        Definition = world.Expedition ?? throw new InvalidDataException("У мира отсутствует описание экспедиции.");
        _map = new ExpeditionMap(new InitLevels(world), Definition);
        _definitions = Definition.Drones.ToDictionary(drone => drone.Id);
        _donors = Definition.Donors.ToDictionary(donor => donor.Position);
        State = state ?? CreateState(world.Id);
        ValidateState(world.Id);
        RefreshVisibility(state == null);
    }

    public ExpeditionDefinition Definition { get; }
    public ExpeditionState State { get; }
    public DroneState ActiveDrone => State.Drones[State.SelectedDroneId];
    public WorldPosition ViewPosition => State.Mode == ControlMode.Laser ? State.Laser : ActiveDrone.Position;
    public WorldPosition StationTerminal => _map.Terminal;
    public IReadOnlyDictionary<Guid, Level> Levels => _map.Levels;

    public DroneDefinition DroneInfo(string id) => _definitions[id];

    public bool CanSelect(DroneState drone) =>
        drone.Discovered && (drone.Powered || _map.OnStation(drone.Position));

    public bool OnStation => _map.OnStation(ActiveDrone.Position);

    public EngineResult Apply(FlightCommand command)
    {
        if (State.GameOver)
            return Reject("Экспедиция завершена. Все дроны разряжены.");

        var result = command.Action switch
        {
            FlightAction.Move => Move(command.Direction),
            FlightAction.CycleMode => CycleMode(),
            FlightAction.SelectDrone => SelectDrone(command.Id),
            FlightAction.Interact => Interact(),
            FlightAction.Charge => Charge(),
            FlightAction.Install => Install(command.Id, command.Slot),
            FlightAction.Remove => Remove(command.Slot),
            FlightAction.Salvage => Salvage(command.Id),
            FlightAction.UseTool => UseTool(command.Slot),
            _ => Reject("Неизвестная команда.")
        };
        if (!result.Reply.Applied)
            return result;

        State.GameOver = State.Drones.Values.All(drone => !drone.Powered &&
            !(drone.Battery == null && _map.OnStation(drone.Position) && drone.Cargo.Any(module => module.Energy > 0)));
        RefreshVisibility(remember: true);
        return result;
    }

    public ExpeditionStatus Status(long revision) => new(State.WorldId, revision, State.Mode, State.SelectedDroneId,
        State.Laser, State.Drones.Values.Where(drone => drone.Discovered).Select(drone =>
            new DroneStatus(drone.Id, _definitions[drone.Id].Name, drone.Position, drone.Battery?.Percent ?? 0,
                drone.Id == State.SelectedDroneId)).ToArray(),
        State.Explored.Values.Sum(level => level.Count), State.GameOver);

    private EngineResult Move(FlightDirection? input)
    {
        Vector? direction = input switch
        {
            FlightDirection.Left => Vector.Left,
            FlightDirection.Right => Vector.Right,
            FlightDirection.Top => Vector.Top,
            FlightDirection.Bottom => Vector.Bottom,
            _ => null
        };
        if (direction is not Vector step)
            return Reject("Неизвестное направление.");

        if (State.Mode == ControlMode.Laser)
        {
            if (!_map.TryStep(State.Laser, step, out var aim))
                return Reject("Точка наведения за пределами доступных квадрантов.");
            State.Laser = aim;
            return Accept();
        }

        var drone = ActiveDrone;
        if (!drone.Powered)
            return Reject("Дрон разряжен. Переключите режим или выберите другой аппарат.");
        if (!_map.TryStep(drone.Position, step, out var next))
            return Reject("Передвижение по заданным координатам запрещено; Соединение будет потеряно.");
        if (!_map.CanEnter(next, drone.Id, StoryItems(drone)))
            return Reject("Проход закрыт.");

        var locomotion = drone.Slots[EquipmentSlot.Locomotion];
        decimal cost = locomotion.Kind == EquipmentKind.Legs ? Definition.Balance.LegMove : Definition.Balance.WheelMove;
        if (!Spend(drone, cost))
            return Reject("Недостаточно энергии для перемещения.");

        drone.Position = next;
        ScanWithLidar(drone);
        if (State.Mode == ControlMode.Combined)
            State.Laser = next;
        foreach (var encountered in State.Drones.Values.Where(candidate => candidate.Position == next))
            encountered.Discovered = true;
        return Accept();
    }

    private EngineResult CycleMode()
    {
        State.Mode = State.Mode switch
        {
            ControlMode.Drone => ControlMode.Laser,
            ControlMode.Laser => ControlMode.Combined,
            _ => ControlMode.Drone
        };
        if (State.Mode == ControlMode.Combined)
            State.Laser = ActiveDrone.Position;
        return Accept();
    }

    private EngineResult SelectDrone(string? id)
    {
        if (id == null || !State.Drones.TryGetValue(id, out var drone) || !CanSelect(drone))
            return Reject("Дрон недоступен для подключения.");
        if (id == State.SelectedDroneId)
            return Reject("Этот дрон уже выбран.");

        State.SelectedDroneId = id;
        if (State.Mode == ControlMode.Combined)
            State.Laser = drone.Position;
        return Accept($"Выбран {id}.");
    }

    private EngineResult Interact()
    {
        if (State.Mode == ControlMode.Laser)
            return Reject("Выберите режим управления дроном.");
        var drone = ActiveDrone;
        if (drone.Position == _map.Terminal)
            return new(new FlightResult(false, "", "station"));
        if (!drone.Powered)
            return Reject("Дрон разряжен.");
        if (_donors.TryGetValue(drone.Position, out var donor))
            return new(new FlightResult(false, "", "donor:" + donor.Id));

        var cell = _map.CellAt(drone.Position);
        var items = StoryItems(drone);
        int capacity = _definitions[drone.Id].MaxWeight - drone.Cargo.Sum(module => Equipment.Weight(module.Kind));
        var interaction = WorldInteraction.Apply(cell, items, capacity, State.Looted);
        bool changed = interaction.Result is InteractionResult.InventoryChanged or InteractionResult.Completed;
        if (changed)
            drone.Items = items.Select(WorldContent.GetItemKind).ToList();
        if (interaction.Actor is Terminal && (changed || interaction.Result == InteractionResult.Action))
        {
            string id = cell.Objects[0].Id;
            State.TerminalUses[id] = State.TerminalUses.GetValueOrDefault(id) + 1;
            changed = true;
        }
        return new(new FlightResult(changed, WorldInteraction.Message(interaction.Result),
            Outcome: interaction.Result.ToString()), interaction);
    }

    private ExpeditionState CreateState(Guid worldId)
    {
        var drones = Definition.Drones.ToDictionary(definition => definition.Id, definition =>
        {
            var drone = new DroneState
            {
                Id = definition.Id, Position = definition.Position, Discovered = definition.Id == Definition.StartDroneId
            };
            Add(EquipmentSlot.Locomotion, EquipmentKind.Wheels);
            Add(EquipmentSlot.LeftArm, EquipmentKind.Gripper);
            Add(EquipmentSlot.RightArm, EquipmentKind.Hammer);
            Add(EquipmentSlot.Battery, EquipmentKind.Battery, definition.BatteryPercent);
            return drone;

            void Add(EquipmentSlot slot, EquipmentKind kind, decimal percent = 0) =>
                drone.Slots.Add(slot, CreateModule(new ModuleDefinition($"{definition.Id}/{slot}", kind, percent)));
        });
        return new ExpeditionState
        {
            WorldId = worldId,
            SelectedDroneId = Definition.StartDroneId,
            Laser = drones[Definition.StartDroneId].Position,
            Drones = drones,
            Donors = Definition.Donors.ToDictionary(donor => donor.Id,
                donor => donor.Components.Select(CreateModule).ToList())
        };
    }

    private ModuleState CreateModule(ModuleDefinition module)
    {
        decimal capacity = Equipment.Capacity(module.Kind, Definition.Balance);
        return new ModuleState(module.Id, module.Kind, capacity, capacity * (module.BatteryPercent / 100));
    }

    private void ValidateState(Guid worldId)
    {
        if (State.WorldId != worldId || !Enum.IsDefined(State.Mode) || !_map.IsValid(State.Laser) ||
            !State.Drones.Keys.ToHashSet().SetEquals(_definitions.Keys) ||
            !State.Drones.TryGetValue(State.SelectedDroneId, out var selected) || !selected.Discovered ||
            !State.Donors.Keys.ToHashSet().SetEquals(Definition.Donors.Select(donor => donor.Id)))
            throw new InvalidDataException("Сохранение не соответствует определению экспедиции.");

        var moduleIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (id, drone) in State.Drones)
        {
            if (id != drone.Id || !_map.IsValid(drone.Position) || !drone.Slots.ContainsKey(EquipmentSlot.Locomotion) ||
                drone.Slots.Any(pair => !Equipment.Fits(pair.Value.Kind, pair.Key)) ||
                drone.Items.Any(item => !Enum.IsDefined(item)) ||
                Weight(drone) > _definitions[id].MaxWeight)
                throw new InvalidDataException($"Сохранение содержит некорректное состояние дрона {id}.");

            foreach (var module in drone.Slots.Values.Concat(drone.Cargo))
                ValidateModule(module);
        }

        foreach (var module in State.Donors.Values.SelectMany(modules => modules))
            ValidateModule(module);

        foreach (var (levelId, cells) in State.Explored)
        {
            if (!Levels.TryGetValue(levelId, out var level) ||
                cells.Any(pair => pair.Key < 0 || pair.Key >= (long)level.Map.Width * level.Map.Height ||
                    !Enum.IsDefined(pair.Value.Color)))
                throw new InvalidDataException("Сохранение содержит некорректные данные разведки.");
        }

        void ValidateModule(ModuleState module)
        {
            if (string.IsNullOrWhiteSpace(module.Id) || !moduleIds.Add(module.Id) || !Enum.IsDefined(module.Kind) ||
                module.Capacity < 0 || module.Energy < 0 || module.Energy > module.Capacity ||
                Equipment.Fits(module.Kind, EquipmentSlot.Battery) != (module.Capacity > 0))
                throw new InvalidDataException("Сохранение содержит некорректный экземпляр оборудования.");
        }
    }

    private static List<Item> StoryItems(DroneState drone) => drone.Items.Select(kind => WorldContent.CreateItem(kind)!).ToList();

    private static int Weight(DroneState drone) =>
        drone.Cargo.Sum(module => Equipment.Weight(module.Kind)) + StoryItems(drone).Sum(item => item.Weight);

    private static EngineResult Accept(string message = "") => new(new FlightResult(true, message));
    private static EngineResult Reject(string message) => new(new FlightResult(false, message));
}
