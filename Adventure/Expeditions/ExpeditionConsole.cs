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
    private int _menuDepth;
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
            if (key == ConsoleKey.I)
            {
                InMenu(Inventory);
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
            HandleInteraction(result);
            Draw();
        }
    }

    private void HandleInteraction(EngineResult? result)
    {
        if (result?.Reply.Menu is { } menu)
            InMenu(() => OpenInteractionMenu(menu));
        if (result?.Interaction is { } interaction)
            ShowInteraction(interaction);
    }

    private void OpenInteractionMenu(string menu)
    {
        if (menu == "targets")
        {
            var targets = owner.Read(engine => engine.InteractionTargets());
            int index = Pick("Объекты на клетке", targets.Select(target => target.Name).ToArray(), "");
            if (index < 0)
                return;
            var result = RunCommand(new(FlightAction.Interact, Id: targets[index].Id));
            if (result?.Reply.Menu is { } next)
                OpenInteractionMenu(next);
            else if (result?.Interaction is { } interaction)
                ShowInteraction(interaction);
        }
        else if (menu == "station")
            Station();
        else if (menu.StartsWith("donor:", StringComparison.Ordinal))
            Salvage(menu["donor:".Length..]);
        else if (menu.StartsWith("crate:", StringComparison.Ordinal))
            OpenCrate(menu["crate:".Length..]);
    }

    private void Inventory()
    {
        var marks = new Dictionary<string, int>();
        while (true)
        {
            var (id, weight, capacity, cargo) = owner.Read(engine => (engine.ActiveDrone.Id,
                engine.CargoWeight, engine.CargoCapacity, engine.Inventory()));
            var groups = cargo.GroupBy(item => item.Group).Select(group => group.ToArray()).ToArray();
            int marked = marks.Values.Sum();
            var options = groups.Select(group => FormatGroup(group) +
                $" · выбрано {marks.GetValueOrDefault(group[0].Group)}").ToList();
            if (marked > 0)
                options.Add($"Выбросить выбранное ({marked})");
            int index = Pick($"{id} · груз {weight}/{capacity} кг · свободно {capacity - weight} кг", options, _message);
            if (index < 0)
                return;
            if (index == groups.Length)
            {
                if (Pick($"Выбросить {marked} ед. груза на текущую клетку?", ["Отмена", "Выбросить"], "") == 1)
                {
                    var ids = groups.SelectMany(group => group.Take(marks.GetValueOrDefault(group[0].Group)))
                        .Select(item => item.Id).ToArray();
                    if (RunCommand(new(FlightAction.Drop, Items: ids))?.Reply.Applied == true)
                        marks.Clear();
                }
                continue;
            }
            var selected = groups[index];
            int count = Quantity(selected, includeZero: true);
            if (count >= 0)
                marks[selected[0].Group] = count;
        }
    }

    private void OpenCrate(string id)
    {
        while (true)
        {
            var (free, contents) = owner.Read(engine =>
                (engine.CargoCapacity - engine.CargoWeight, engine.CrateContents(id)));
            if (contents.Count == 0)
                return;
            var groups = contents.GroupBy(item => item.Group).Select(group => group.ToArray()).ToArray();
            var options = new[] { "Забрать всё, что помещается" }.Concat(groups.Select(FormatGroup)).ToArray();
            int index = Pick($"Грузовой ящик · свободно {free} кг", options, _message);
            if (index < 0)
                return;
            if (index == 0)
            {
                RunCommand(new(FlightAction.Take, Id: id));
                continue;
            }
            var selected = groups[index - 1];
            int count = selected.Length == 1 ? 1 : Quantity(selected, includeZero: false);
            if (count > 0)
                RunCommand(new(FlightAction.Take, Id: id, Items: selected.Take(count).Select(item => item.Id).ToArray()));
        }
    }

    private int Quantity(CargoEntry[] group, bool includeZero)
    {
        int minimum = includeZero ? 0 : 1;
        int index = Pick(group[0].Name, Enumerable.Range(minimum, group.Length - minimum + 1)
            .Select(count => count == 0 ? "Снять отметку" : $"{count} ед. · {count * group[0].Weight} кг").ToArray(), "");
        return index < 0 ? -1 : index + minimum;
    }

    private static string FormatGroup(CargoEntry[] group) =>
        $"{group[0].Name} ×{group.Length} · {group[0].Weight} кг/ед. · {group.Length * group[0].Weight} кг" +
        (group[0].Detail.Length == 0 ? "" : " · " + group[0].Detail);

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
        if (interaction is { Result: InteractionResult.Action, Actor: Terminal { LoadItem: null } terminal } &&
            terminal.Message.Contains('\n'))
        {
            InMenu(() => ReadJournal(terminal));
            return;
        }

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

    private void ReadJournal(Terminal terminal)
    {
        int top = 0;
        while (true)
        {
            var size = ExpeditionDisplay.Size();
            var lines = WrapJournal(terminal.Message, Math.Max(1, size.Width));
            int page = Math.Max(1, size.Height - ExpeditionDisplay.JournalHeaderHeight);
            int last = Math.Max(0, lines.Count - page);
            top = Math.Clamp(top, 0, last);
            _display.Journal(terminal.Name, lines, top, size);

            while (!Console.KeyAvailable && ExpeditionDisplay.Size() == size)
                Thread.Sleep(35);
            if (ExpeditionDisplay.Size() != size || !Console.KeyAvailable)
                continue;

            switch (Console.ReadKey(true).Key)
            {
                case ConsoleKey.Escape: return;
                case ConsoleKey.UpArrow: top--; break;
                case ConsoleKey.DownArrow: top++; break;
                case ConsoleKey.PageUp: top -= page; break;
                case ConsoleKey.PageDown: top += page; break;
                case ConsoleKey.Home: top = 0; break;
                case ConsoleKey.End: top = last; break;
            }
        }
    }

    private static List<string> WrapJournal(string text, int width)
    {
        var lines = new List<string>();
        foreach (string paragraph in text.ReplaceLineEndings("\n").Split('\n'))
        {
            string remaining = paragraph;
            while (remaining.Length > width)
            {
                int space = remaining.LastIndexOf(' ', width);
                int split = space > 0 ? space : width;
                lines.Add(remaining[..split]);
                remaining = remaining[split..].TrimStart();
            }
            lines.Add(remaining);
        }
        return lines;
    }

    private void InMenu(Action run)
    {
        if (_menuDepth++ == 0)
            owner.SetMenuOpen(true);
        try
        {
            run();
        }
        finally
        {
            if (--_menuDepth == 0)
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
