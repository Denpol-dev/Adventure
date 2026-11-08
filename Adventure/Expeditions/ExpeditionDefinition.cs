using Foxnet.Session;

namespace Adventure.Expeditions;

public enum EquipmentSlot { Lidar, BiometricEye, Locomotion, BodyA, BodyB, LeftArm, RightArm, Battery }

public enum EquipmentKind { Lidar, BiometricEye, Wheels, Legs, BodyModule, Gripper, Hammer, Battery, HighCapacityBattery }

public enum DroneKind { Worker, Cargo }

public sealed record ExpeditionDefinition(string StartDroneId, ExpeditionBalance Balance, StationDefinition Station,
    List<DroneDefinition> Drones, List<DonorDefinition> Donors, List<InteriorArea> Interiors, int LayoutRevision = 0);

public sealed record ExpeditionBalance(int ScanRadius, decimal WheelMove, decimal LegMove, decimal LidarScan,
    decimal ToolUse, decimal StationCharge, decimal BatteryCapacity, decimal HighCapacityBatteryCapacity);

public sealed record StationDefinition(string Id, WorldPosition Origin, int Width, int Height,
    string ServiceTerminalId, List<BayDefinition> Bays)
{
    public bool Contains(WorldPosition position) => position.LevelId == Origin.LevelId &&
        position.X >= Origin.X && position.X - Origin.X < Width &&
        position.Y >= Origin.Y && position.Y - Origin.Y < Height;

    public WorldPosition At(int x, int y) => Origin with { X = Origin.X + x, Y = Origin.Y + y };

    internal static bool IsStationGlyph(char glyph) => glyph is '▢' or '+' or '-' or '|' or '▮' or '≈' or '░';
}

public sealed record BayDefinition(string Id, string DroneId, int GateX, int GateY, int ParkingX, int ParkingY)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public int TerminalX => ParkingX + (ParkingX - GateX);
}

public sealed record DroneDefinition(string Id, string Name, DroneKind Kind, WorldPosition Position, ConsoleColor Color,
    int MaxWeight, decimal BatteryPercent);

public sealed record DonorDefinition(string Id, string Name, WorldPosition Position, List<ModuleDefinition> Components);

public sealed record ModuleDefinition(string Id, EquipmentKind Kind, decimal BatteryPercent = 0);

public sealed record InteriorArea(string BuildingId, string RoomId, WorldPosition Origin, int Width, int Height);

public static class Equipment
{
    public static bool Fits(EquipmentKind kind, EquipmentSlot slot) => kind switch
    {
        EquipmentKind.Lidar => slot == EquipmentSlot.Lidar,
        EquipmentKind.BiometricEye => slot == EquipmentSlot.BiometricEye,
        EquipmentKind.Wheels or EquipmentKind.Legs => slot == EquipmentSlot.Locomotion,
        EquipmentKind.BodyModule => slot is EquipmentSlot.BodyA or EquipmentSlot.BodyB,
        EquipmentKind.Gripper or EquipmentKind.Hammer => slot is EquipmentSlot.LeftArm or EquipmentSlot.RightArm,
        EquipmentKind.Battery or EquipmentKind.HighCapacityBattery => slot == EquipmentSlot.Battery,
        _ => false
    };

    public static int Weight(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Legs or EquipmentKind.HighCapacityBattery => 3,
        EquipmentKind.Wheels or EquipmentKind.Battery or EquipmentKind.Hammer or EquipmentKind.BodyModule => 2,
        _ => 1
    };

    public static string Name(EquipmentKind kind) => kind switch
    {
        EquipmentKind.Lidar => "Лидар",
        EquipmentKind.BiometricEye => "Биометрический глаз",
        EquipmentKind.Wheels => "Колёсная база",
        EquipmentKind.Legs => "Ножная база",
        EquipmentKind.BodyModule => "Модуль корпуса",
        EquipmentKind.Gripper => "Механический захват",
        EquipmentKind.Hammer => "Молот",
        EquipmentKind.Battery => "Обычный аккумулятор",
        EquipmentKind.HighCapacityBattery => "Аккумулятор повышенной ёмкости",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    public static decimal Capacity(EquipmentKind kind, ExpeditionBalance balance) => kind switch
    {
        EquipmentKind.Battery => balance.BatteryCapacity,
        EquipmentKind.HighCapacityBattery => balance.HighCapacityBatteryCapacity,
        _ => 0
    };
}
