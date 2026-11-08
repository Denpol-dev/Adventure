using Adventure.Entities.Actors;
using Adventure.Entities.Characters;
using Foxnet.Session;
using Microsoft.Data.Sqlite;

namespace Adventure.Expeditions;

internal sealed class ExpeditionConsole(ExpeditionOwner owner)
{
    private readonly ExpeditionDisplay _display = new();
    private string _message = "";
    private bool _preserveMessage;
    private long _shownRevision;
    private (int Width, int Height) _shownSize;

    public void Run()
    {
        Console.Clear();
        Draw();
        while (true)
        {
            while (!Console.KeyAvailable)
            {
                Thread.Sleep(35);
                if (owner.Revision != _shownRevision || ExpeditionDisplay.Size() != _shownSize)
                    Draw();
            }

            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Escape)
                return;
            if (key == ConsoleKey.F)
            {
                InMenu(Fleet);
                Draw();
                continue;
            }

            FlightCommand? command = key switch
            {
                ConsoleKey.W => new(FlightAction.Move, FlightDirection.Top),
                ConsoleKey.S => new(FlightAction.Move, FlightDirection.Bottom),
                ConsoleKey.A => new(FlightAction.Move, FlightDirection.Left),
                ConsoleKey.D => new(FlightAction.Move, FlightDirection.Right),
                ConsoleKey.Tab => new(FlightAction.CycleMode),
                ConsoleKey.E => new(FlightAction.Interact),
                ConsoleKey.C => new(FlightAction.Charge),
                ConsoleKey.Q => new(FlightAction.UseTool, Slot: nameof(EquipmentSlot.LeftArm)),
                ConsoleKey.R => new(FlightAction.UseTool, Slot: nameof(EquipmentSlot.RightArm)),
                _ => null
            };
            if (command == null)
                continue;

            var result = RunCommand(command);
            if (result?.Reply.Menu == "station")
                InMenu(Station);
            else if (result?.Reply.Menu is string donorMenu && donorMenu.StartsWith("donor:", StringComparison.Ordinal))
                InMenu(() => Salvage(donorMenu["donor:".Length..]));
            if (result?.Interaction is { } interaction)
                ShowInteraction(interaction);
            Draw();
        }
    }

    private EngineResult? RunCommand(FlightCommand command)
    {
        _preserveMessage = false;
        try
        {
            var (result, _) = owner.Execute(command);
            _message = result.Reply.Message;
            return result;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or SqliteException or InvalidOperationException)
        {
            _message = exception.Message;
            return null;
        }
    }

    private void Draw() => owner.Read(engine =>
    {
        _shownRevision = owner.Revision;
        _shownSize = ExpeditionDisplay.Size();
        _display.Draw(engine, _message, _preserveMessage);
        return 0;
    });

    private void ShowInteraction(Interaction interaction)
    {
        if (interaction.Actor is Actor actor && Console.BufferHeight > Actor.MessageY)
        {
            Draw();
            Console.ForegroundColor = ConsoleColor.White;
            actor.Action();
            _preserveMessage = true;
            _display.Invalidate();
        }

        if (interaction.Result == InteractionResult.Completed)
        {
            InMenu(() => Pick("Передача данных завершена", ConsoleView.CompletionLines, ""));
            _preserveMessage = false;
        }
    }

    private void InMenu(Action run)
    {
        owner.SetMenuOpen(true);
        try
        {
            run();
        }
        finally
        {
            owner.SetMenuOpen(false);
        }
    }

    private void Fleet()
    {
        var drones = owner.Read(engine => engine.State.Drones.Values.Where(drone => drone.Discovered)
            .Select(drone =>
            {
                var info = engine.DroneInfo(drone.Id);
                var level = engine.Levels[drone.Position.LevelId].Name;
                var bay = engine.Definition.Station.Bays.Single(bay => bay.DroneId == drone.Id);
                string state = drone.Id == engine.ActiveDrone.Id ? " · выбран" : engine.CanSelect(drone) ? "" : " · недоступен";
                return (drone.Id, Label: $"{drone.Id} {info.Name} · {info.Color} · {drone.Battery?.Percent ?? 0:0.#}% " +
                    $"· {level} ({drone.Position.X}, {drone.Position.Y}) · {bay.Id}{state}");
            }).ToArray());
        int selected = Pick("Парк дронов", drones.Select(drone => drone.Label).ToArray(), "");
        if (selected >= 0)
            RunCommand(new FlightCommand(FlightAction.SelectDrone, Id: drones[selected].Id));
    }

    private void Station()
    {
        var slots = Enum.GetValues<EquipmentSlot>();
        while (true)
        {
            var (layout, cycles) = owner.Read(engine => (slots.Select(slot => $"{slot}: " +
                Describe(engine.ActiveDrone.Slots.GetValueOrDefault(slot))).ToArray(), engine.State.Station.ChargeCycles));
            int index = Pick($"Техстанция · оборудование · циклы зарядки: {cycles}", layout, _message);
            if (index < 0)
                return;

            var slot = slots[index];
            var options = owner.Read(engine => engine.ActiveDrone.Cargo.Where(module => Equipment.Fits(module.Kind, slot))
                .Select(module => (Label: "Установить " + Describe(module),
                    Command: new FlightCommand(FlightAction.Install, Id: module.Id, Slot: slot.ToString()))).ToList());
            if (slot != EquipmentSlot.Locomotion)
                options.Add(("Демонтировать", new FlightCommand(FlightAction.Remove, Slot: slot.ToString())));
            if (slot == EquipmentSlot.Battery)
                options.Add(("Зарядить аккумулятор", new FlightCommand(FlightAction.Charge)));
            index = Pick($"Техстанция · {slot}", options.Select(option => option.Label).ToArray(), "");
            if (index < 0)
                return;
            RunCommand(options[index].Command);
        }
    }

    private void Salvage(string donorId)
    {
        var name = owner.Read(engine => engine.Definition.Donors.Single(donor => donor.Id == donorId).Name);
        while (true)
        {
            var modules = owner.Read(engine => engine.State.Donors[donorId].ToArray());
            int index = Pick(name, modules.Select(Describe).ToArray(), _message);
            if (index < 0)
                return;
            RunCommand(new FlightCommand(FlightAction.Salvage, Id: modules[index].Id));
        }
    }

    private int Pick(string title, IReadOnlyList<string> options, string message)
    {
        int selected = 0;
        while (true)
        {
            var size = ExpeditionDisplay.Size();
            _display.Menu(title, options.Count > 0 ? options : ["Нет доступных элементов."], selected, message);
            while (!Console.KeyAvailable)
            {
                Thread.Sleep(35);
                var current = ExpeditionDisplay.Size();
                if (current != size)
                {
                    size = current;
                    _display.Menu(title, options.Count > 0 ? options : ["Нет доступных элементов."], selected, message);
                }
            }
            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.Escape:
                    return -1;
                case ConsoleKey.Enter when options.Count > 0:
                    return selected;
                case ConsoleKey.UpArrow when selected > 0:
                    selected--;
                    break;
                case ConsoleKey.DownArrow when selected + 1 < options.Count:
                    selected++;
                    break;
            }
        }
    }

    private static string Describe(ModuleState? module) => module == null ? "пусто" :
        $"{Equipment.Name(module.Kind)} [{module.Id}]" +
        (module.Capacity > 0 ? $" · {module.Percent:0.#}% · {module.Energy:0.##}/{module.Capacity:0.##} GAh" : "");
}
