using Adventure.Entities.Worlds;
using Foxnet.Session;
using System.Text.Json.Serialization;

namespace Adventure.Expeditions;

public sealed record ModuleState(string Id, EquipmentKind Kind, decimal Capacity, decimal Energy)
{
    [JsonIgnore] public decimal Percent => Capacity > 0 ? Energy / Capacity * 100 : 0;
}

public sealed record CargoItem(string Id, ItemKind Kind);

public sealed record DropCrate(string Id, WorldPosition Position)
{
    public List<CargoItem> Items { get; init; } = [];
    public List<ModuleState> Modules { get; init; } = [];
}

public sealed class DroneState
{
    public required string Id { get; init; }
    public required WorldPosition Position { get; set; }
    public bool Discovered { get; set; }
    public bool LidarScanned { get; set; }
    public Dictionary<EquipmentSlot, ModuleState> Slots { get; init; } = [];
    public List<ModuleState> Cargo { get; init; } = [];
    public List<CargoItem> Goods { get; init; } = [];
    [JsonPropertyName("Items"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<ItemKind>? LegacyItems { get; set; }

    [JsonIgnore] public IEnumerable<ItemKind> Items => Goods.Select(item => item.Kind);

    [JsonIgnore] public ModuleState? Battery => Slots.GetValueOrDefault(EquipmentSlot.Battery);
    [JsonIgnore] public bool Powered => Battery?.Energy > 0;
    [JsonIgnore] public bool HasBiometricAccess => Slots.ContainsKey(EquipmentSlot.BiometricEye);
}

public enum CellVisibility { Unknown, Remembered, Visible }

public sealed record ObservedCell(char Glyph, ConsoleColor Color, bool Detail);

public sealed record CellView(CellVisibility Visibility, char Glyph, ConsoleColor Color, string? DroneId, bool Laser);

public sealed class StationState
{
    public int ChargeCycles { get; set; }
    public decimal DeliveredEnergy { get; set; }
    public int ModuleChanges { get; set; }
}

public sealed class ExpeditionState
{
    public required Guid WorldId { get; init; }
    public int LayoutRevision { get; set; }
    public bool AwaitingFirstSurvey { get; set; }
    public required string SelectedDroneId { get; set; }
    public required WorldPosition Laser { get; set; }
    public ControlMode Mode { get; set; } = ControlMode.Combined;
    public bool GameOver { get; set; }
    public StationState Station { get; init; } = new();
    public required Dictionary<string, DroneState> Drones { get; init; }
    public required Dictionary<string, List<ModuleState>> Donors { get; init; }
    public Dictionary<Guid, Dictionary<int, ObservedCell>> Explored { get; init; } = [];
    public HashSet<string> Looted { get; init; } = [];
    public Dictionary<string, int> TerminalUses { get; init; } = [];
    public List<DropCrate> DropCrates { get; init; } = [];
    public Dictionary<string, TerminalProgress> Terminals { get; init; } = [];
    public HashSet<string> ReadFiles { get; init; } = [];
    public HashSet<string> ExtractedFiles { get; init; } = [];
    public HashSet<string> Operations { get; init; } = [];
}
