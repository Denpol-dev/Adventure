using System.Security.Cryptography;
using System.Text;

namespace Foxnet.Session;

public readonly record struct WorldPosition(Guid LevelId, int X, int Y);

public enum ControlMode { Drone, Laser, Combined }

public enum FlightAction { Move, CycleMode, SelectDrone, Interact, Charge, Install, Remove, Salvage, UseTool, Drop, Take }

public enum FlightDirection { Left, Right, Top, Bottom }

public sealed record FlightCommand(FlightAction Action, FlightDirection? Direction = null, string? Id = null,
    string? Slot = null, IReadOnlyList<string>? Items = null);

public sealed record FlightResult(bool Applied, string Message, string? Menu = null, string? Outcome = null);

public sealed record DroneStatus(string Id, string Name, WorldPosition Position, decimal ChargePercent, bool Selected)
{
    public bool Connected => Selected && ChargePercent > 0;
}

public sealed record ExpeditionStatus(Guid WorldId, long Revision, ControlMode Mode, string SelectedDroneId,
    WorldPosition Laser, IReadOnlyList<DroneStatus> Drones, int ExploredCells, bool GameOver);

public enum ExpeditionRequestKind { Initialize, Status, Command, Watch }

public sealed record ExpeditionRequest(ExpeditionRequestKind Kind, FlightCommand? Command = null);

public sealed record ExpeditionReply(ExpeditionStatus? Status, FlightResult? Result = null, string? Error = null);

public static class ExpeditionAddress
{
    public static string DefaultDatabasePath
    {
        get
        {
            string? directory = Environment.GetEnvironmentVariable("FOXNET_DATA_DIR");
            directory = string.IsNullOrWhiteSpace(directory)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FOXNET")
                : Path.GetFullPath(directory, AppContext.BaseDirectory);
            return Path.Combine(directory, "session.db");
        }
    }

    public static string PipeName(string databasePath)
    {
        var fullPath = Path.GetFullPath(databasePath);
        if (OperatingSystem.IsWindows())
            fullPath = fullPath.ToUpperInvariant();
        return "foxnet-expedition-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath)))[..24];
    }
}
