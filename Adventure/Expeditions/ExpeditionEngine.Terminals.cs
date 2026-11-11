using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Adventure.Entities.Worlds;
using Foxnet.Session;

namespace Adventure.Expeditions;

public sealed partial class ExpeditionEngine
{
    public TerminalPanel? Terminal(string id)
    {
        if (!TryTerminal(id, out var obj, out var system))
            return null;
        var role = Role(id, system);
        var login = State.Terminals.GetValueOrDefault(id)?.Login;
        var options = new List<TerminalOption>();
        if (system.Accounts is { Length: > 0 })
            options.Add(new("Ввести учётные данные", FlightAction.SignInTerminal));
        if (login != null)
            options.Add(new("Завершить сеанс", FlightAction.SignOutTerminal));
        if (role == TerminalRole.Administrator)
            options.Add(new("Сбросить пароль по точному логину", FlightAction.ResetTerminalPassword));
        foreach (var file in (system.Files ?? []).Where(file => CanRead(file, role)))
        {
            options.Add(new($"{file.Folder} / {file.Title}", FlightAction.ReadTerminalFile, file.Id));
            if (file.Appraisal > 0 && State.ReadFiles.Contains(file.Id) && !State.ExtractedFiles.Contains(file.Id))
                options.Add(new($"Извлечь в FOXNET · {file.Title}", FlightAction.ExtractEvidence, file.Id));
        }
        foreach (var operation in (system.Operations ?? []).Where(operation => role >= operation.Role))
        {
            if (operation.Repeatable || !State.Operations.Contains(operation.Id))
                options.Add(new(operation.Title, FlightAction.OperateTerminal, operation.Id));
        }
        if (system.DispatchAfter != null && State.Operations.Contains(system.DispatchAfter) && role >= TerminalRole.Operator)
            options.Add(new("Загрузить ценность в орбитальный отсек", FlightAction.DispatchCargo));
        return new($"{obj.Name ?? id} · {login ?? "гостевой доступ"}", options);
    }

    public IReadOnlyList<CargoEntry> DispatchableCargo() =>
        Inventory().Where(entry => ActiveDrone.Cargo.Any(module => module.Id == entry.Id) ||
            ActiveDrone.Goods.Any(item => item.Id == entry.Id && !WorldContent.CreateItem(item.Kind)!.IsPlotItem)).ToArray();

    private EngineResult SignInTerminal(string? id, string? login, string? password)
    {
        if (!TryTerminal(id, out _, out var system) || string.IsNullOrEmpty(password) ||
            (system.Accounts ?? []).FirstOrDefault(account => account.Login == login) is not { } account)
            return Reject("Терминал недоступен или учётные данные не приняты.");
        var progress = State.Terminals.GetValueOrDefault(id!);
        bool valid = progress?.PasswordHashes.GetValueOrDefault(account.Login) is { } hash
            ? hash == PasswordHash(id!, account.Login, password) : account.Password == password;
        if (!valid)
            return Reject("Учётные данные не приняты.");
        (State.Terminals[id!] = progress ?? new TerminalProgress()).Login = account.Login;
        return Accept("Авторизация выполнена.");
    }

    private EngineResult SignOutTerminal(string? id)
    {
        if (!TryTerminal(id, out _, out _) || State.Terminals.GetValueOrDefault(id!) is not { Login: not null } progress)
            return Reject("Нет активного сеанса.");
        progress.Login = null;
        return Accept("Сеанс завершён.");
    }

    private EngineResult ResetTerminalPassword(string? id, string? login, string? password)
    {
        if (!TryTerminal(id, out _, out var system) || Role(id!, system) != TerminalRole.Administrator)
            return Reject("Требуются полномочия администратора.");
        if ((system.Accounts ?? []).FirstOrDefault(account => account.Login == login) is not { } account ||
            string.IsNullOrWhiteSpace(password) || password.Length > 96)
            return Reject("Введите точный логин и новый пароль (до 96 символов).");
        State.Terminals[id!].PasswordHashes[account.Login] = PasswordHash(id!, account.Login, password);
        return Accept("Пароль изменён.");
    }

    private EngineResult ReadTerminalFile(string? id, string? fileId, bool extract)
    {
        if (!TryTerminal(id, out var obj, out var system) ||
            (system.Files ?? []).FirstOrDefault(file => file.Id == fileId) is not { } file || !CanRead(file, Role(id!, system)))
            return Reject("Файл недоступен.");
        if (!extract)
        {
            bool firstRead = State.ReadFiles.Add(file.Id);
            return new(new FlightResult(firstRead, "", Outcome: file.Text));
        }
        if (file.Appraisal == 0 || !State.ReadFiles.Contains(file.Id))
            return Reject("Сначала прочитайте доступный материал.");
        if (!State.ExtractedFiles.Add(file.Id))
            return Reject("Документ уже находится в библиотеке FOXNET.");
        return new(new FlightResult(true, "Документ передан в библиотеку FOXNET."),
            Export: new EvidenceExport(file.Id, file.Title, obj.Name ?? obj.Id, file.Text, file.TradeAppraisal,
                file.Authenticity >= 60 && file.Relevance >= 60 ? file.Signal : null));
    }

    private EngineResult OperateTerminal(string? id, string? operationId)
    {
        if (!TryTerminal(id, out _, out var system) ||
            (system.Operations ?? []).FirstOrDefault(operation => operation.Id == operationId) is not { } operation)
            return Reject("Команда недоступна с текущим уровнем допуска.");
        if (OperationBlock(id!, system, operation) is { } message)
            return Reject(message);
        var equipment = operation.RequiresEquipment ?? [];
        if (operation.Destination is { } target && !_map.CanEnter(target, ActiveDrone.Id, StoryItems(ActiveDrone)))
            return Reject("Точка перехода недоступна.");
        if (equipment.Any(kind => kind is EquipmentKind.Hammer or EquipmentKind.Gripper) &&
            !Spend(ActiveDrone, Definition.Balance.ToolUse))
            return Reject("Недостаточно энергии для операции.");

        State.Operations.Add(operation.Id);
        if (operation.Destination is { } destination)
        {
            ActiveDrone.Position = destination;
            ScanWithLidar(ActiveDrone);
            FollowDrone();
        }
        return Accept(operation.Report);
    }

    private string? OperationBlock(string id, TerminalSystem system, TerminalOperation operation)
    {
        if (Role(id, system) < operation.Role)
            return "Команда недоступна с текущим уровнем допуска.";
        if (!operation.Repeatable && State.Operations.Contains(operation.Id))
            return "Операция уже выполнена.";
        if (!(operation.RequiresFiles ?? []).All(State.ReadFiles.Contains) ||
            !(operation.RequiresOperations ?? []).All(State.Operations.Contains))
            return "Проверьте технические журналы и завершите предыдущие операции.";
        var equipment = operation.RequiresEquipment ?? [];
        return equipment.All(kind => ActiveDrone.Slots.Values.Any(module => module.Kind == kind))
            ? null : "Нужно оборудование: " + string.Join(", ", equipment.Select(Equipment.Name)) + ".";
    }

    private EngineResult DispatchCargo(string? terminalId, string? itemId)
    {
        if (!TryTerminal(terminalId, out _, out var system) || system.DispatchAfter == null ||
            !State.Operations.Contains(system.DispatchAfter) || Role(terminalId!, system) < TerminalRole.Operator)
            return Reject("Орбитальная доставка недоступна.");
        if (DispatchableCargo().FirstOrDefault(entry => entry.Id == itemId) is not { } cargo)
            return Reject("Эта ценность отсутствует в грузовом отсеке или не принимается лифтом.");

        var module = ActiveDrone.Cargo.Find(module => module.Id == cargo.Id);
        var item = ActiveDrone.Goods.Find(item => item.Id == cargo.Id);
        var payload = module != null ? JsonSerializer.SerializeToUtf8Bytes(module) : JsonSerializer.SerializeToUtf8Bytes(item!);
        long appraisal = module == null ? cargo.Weight * 240L : (cargo.Weight * 165L) + (long)(module.Percent * 4);
        if (module != null)
            ActiveDrone.Cargo.Remove(module);
        else
            ActiveDrone.Goods.Remove(item!);
        string condition = module is { Capacity: > 0 } ? $"{module.Energy:0.##}/{module.Capacity:0.##} GAh" : cargo.Detail;
        return new(new FlightResult(true, "Ценность доставлена на орбиту. Сделка доступна в HAESEI Bank."),
            Export: new OrbitalCargoExport(cargo.Id, cargo.Name, cargo.Weight, condition, appraisal, payload));
    }

    private bool TryTerminal(string? id, out ObjectDefinition obj, out TerminalSystem system)
    {
        obj = null!;
        system = null!;
        if (id == null || State.Mode == ControlMode.Laser || !ActiveDrone.Powered ||
            !_terminals.TryGetValue(id, out var found) || !_map.CellAt(ActiveDrone.Position).Objects.Any(obj => obj.Id == id))
            return false;
        obj = found;
        system = found.System!;
        return true;
    }

    private TerminalRole Role(string id, TerminalSystem system) =>
        (system.Accounts ?? []).FirstOrDefault(account => account.Login == State.Terminals.GetValueOrDefault(id)?.Login)?.Role
        ?? TerminalRole.Public;

    private bool CanRead(TerminalFile file, TerminalRole role) =>
        role >= file.Role && (file.RequiresOperations ?? []).All(State.Operations.Contains);

    private static string PasswordHash(string id, string login, string password) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{id}\0{login}\0{password}")));

    private void ValidateTerminalState()
    {
        var files = _terminals.Values.SelectMany(obj => obj.System!.Files ?? []).ToDictionary(file => file.Id);
        var operations = _terminals.Values.SelectMany(obj => obj.System!.Operations ?? []).Select(operation => operation.Id).ToHashSet();
        if (State.ReadFiles.Any(id => !files.ContainsKey(id)) || !State.ExtractedFiles.IsSubsetOf(State.ReadFiles) ||
            State.ExtractedFiles.Any(id => files[id].Appraisal <= 0) || !State.Operations.IsSubsetOf(operations))
            throw new InvalidDataException("Сохранение содержит неизвестный файл или операцию терминала.");
        foreach (var (id, progress) in State.Terminals)
        {
            if (!_terminals.TryGetValue(id, out var terminal) ||
                progress.Login != null && Role(id, terminal.System!) == TerminalRole.Public ||
                progress.PasswordHashes.Any(pair => !(terminal.System!.Accounts ?? []).Any(account => account.Login == pair.Key) ||
                    pair.Value.Length != 64 || pair.Value.Any(character => !Uri.IsHexDigit(character))))
                throw new InvalidDataException("Сохранение содержит недействительный сеанс терминала.");
        }
    }
}
