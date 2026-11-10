using Adventure.Entities.Worlds;
using Foxnet.Session;

namespace Adventure.Expeditions;

public enum TerminalRole { Public, Operator, Administrator }

public sealed record TerminalAccount(string Login, string Password, TerminalRole Role);
public sealed record TerminalFile(string Id, string Folder, string Title, string Text,
    TerminalRole Role = TerminalRole.Public, long Appraisal = 0, string? Signal = null, string[]? RequiresOperations = null);
public sealed record TerminalOperation(string Id, string Title, string Report, TerminalRole Role = TerminalRole.Public,
    string[]? RequiresOperations = null, string[]? RequiresFiles = null, EquipmentKind[]? RequiresEquipment = null,
    WorldPosition? Destination = null, bool Repeatable = false);
public sealed record TerminalSystem(TerminalAccount[]? Accounts = null, TerminalFile[]? Files = null,
    TerminalOperation[]? Operations = null, string? DispatchAfter = null);

public sealed class TerminalProgress
{
    public string? Login { get; set; }
    public Dictionary<string, string> PasswordHashes { get; init; } = [];
}

public sealed record TerminalOption(string Title, FlightAction Action, string? Subject = null);
public sealed record TerminalPanel(string Title, IReadOnlyList<TerminalOption> Options);

internal static class TerminalValidator
{
    public static void Validate(IReadOnlyDictionary<Guid, LevelDefinition> levels)
    {
        var terminals = levels.Values.SelectMany(level => level.Objects.Select(obj => (level, obj)))
            .Where(entry => entry.obj.System != null).ToArray();
        var files = new HashSet<string>(StringComparer.Ordinal);
        var operations = new Dictionary<string, TerminalOperation>(StringComparer.Ordinal);
        foreach (var (level, obj) in terminals)
        {
            var system = obj.System!;
            var logins = new HashSet<string>(StringComparer.Ordinal);
            foreach (var account in system.Accounts ?? [])
                Require(Text(account.Login) && Text(account.Password) && logins.Add(account.Login) &&
                    Enum.IsDefined(account.Role) && account.Role != TerminalRole.Public, obj.Id);
            foreach (var file in system.Files ?? [])
                Require(Text(file.Id) && files.Add(file.Id) && Text(file.Title) && Text(file.Folder) && Text(file.Text) &&
                    Enum.IsDefined(file.Role) && file.Appraisal is >= 0 and <= 1_000_000 &&
                    (file.Signal == null || file.Appraisal > 0 &&
                        file.Signal is "restricted" or "operator-trace" or "identity-separation" or "automation"), obj.Id);
            foreach (var operation in system.Operations ?? [])
            {
                Require(Text(operation.Id) && operations.TryAdd(operation.Id, operation) && Text(operation.Title) &&
                    Text(operation.Report) && Enum.IsDefined(operation.Role) &&
                    (operation.RequiresEquipment ?? []).All(kind => Enum.IsDefined(kind)), obj.Id);
                if (operation.Destination is { } position)
                {
                    Require(operation.Repeatable && levels.TryGetValue(position.LevelId, out var destination) &&
                        destination.Underground != level.Underground && destination.Contains(position.X, position.Y) &&
                        WorldContent.CreateCell(destination.Rows[position.Y][position.X],
                            destination.Cells.FirstOrDefault(cell => cell.X == position.X && cell.Y == position.Y),
                            destination.Objects.Where(target => target.X == position.X && target.Y == position.Y)).CanEnter([]), obj.Id);
                }
            }
        }
        foreach (var (level, obj) in terminals)
        {
            var system = obj.System!;
            foreach (var file in system.Files ?? [])
                Require((file.RequiresOperations ?? []).All(operations.ContainsKey), obj.Id);
            foreach (var operation in system.Operations ?? [])
                Require((operation.RequiresFiles ?? []).All(files.Contains) &&
                    (operation.RequiresOperations ?? []).All(id => id != operation.Id && operations.ContainsKey(id)), obj.Id);
            Require(system.DispatchAfter == null || !level.Underground && operations.ContainsKey(system.DispatchAfter), obj.Id);
        }

        static bool Text(string value) => !string.IsNullOrWhiteSpace(value);
        static void Require(bool condition, string id)
        {
            if (!condition)
                throw new InvalidDataException($"Некорректное описание терминала {id}.");
        }
    }
}
