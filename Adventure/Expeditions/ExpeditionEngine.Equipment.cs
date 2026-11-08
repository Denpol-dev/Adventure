using Foxnet.Session;

namespace Adventure.Expeditions;

public sealed partial class ExpeditionEngine
{
    private EngineResult Charge()
    {
        var drone = ActiveDrone;
        if (!CanService || drone.Battery is not { } battery)
            return Reject("Для зарядки нужен аккумулятор и доступ к станции.");
        if (battery.Energy == battery.Capacity)
            return Reject("Аккумулятор уже заряжен.");

        decimal energy = battery.Energy + Math.Min(battery.Capacity - battery.Energy, Definition.Balance.StationCharge);
        drone.Slots[EquipmentSlot.Battery] = battery with { Energy = energy };
        State.Station.ChargeCycles++;
        State.Station.DeliveredEnergy += energy - battery.Energy;
        return Accept($"Заряд: {energy:0.##}/{battery.Capacity:0.##} GAh.");
    }

    private EngineResult Install(string? id, string? slotName)
    {
        if (!CanService)
            return Reject("Установка оборудования доступна только на ремонтной станции.");
        var drone = ActiveDrone;
        if (!TrySlot(slotName, out var slot) || drone.Cargo.Find(module => module.Id == id) is not { } next ||
            !Equipment.Fits(next.Kind, slot))
            return Reject("Модуль несовместим со слотом или отсутствует в инвентаре.");

        var previous = drone.Slots.GetValueOrDefault(slot);
        int change = previous == null ? -Equipment.Weight(next.Kind) : Equipment.Weight(previous.Kind) - Equipment.Weight(next.Kind);
        if (Weight(drone) + change > _definitions[drone.Id].MaxWeight)
            return Reject("Недостаточно грузоподъёмности для замены модуля.");

        drone.Cargo.Remove(next);
        if (previous != null)
            drone.Cargo.Add(previous);
        drone.Slots[slot] = next;
        State.Station.ModuleChanges++;
        if (slot == EquipmentSlot.Lidar)
            ScanWithLidar(drone);
        return Accept($"Установлено: {Equipment.Name(next.Kind)}.");
    }

    private EngineResult Remove(string? slotName)
    {
        if (!CanService)
            return Reject("Демонтаж доступен только на ремонтной станции.");
        var drone = ActiveDrone;
        if (!TrySlot(slotName, out var slot) || !drone.Slots.TryGetValue(slot, out var module))
            return Reject("Слот пуст или не существует.");
        if (slot == EquipmentSlot.Locomotion)
            return Reject("Ходовую часть можно только заменить.");
        if (Weight(drone) + Equipment.Weight(module.Kind) > _definitions[drone.Id].MaxWeight)
            return Reject("Недостаточно грузоподъёмности для демонтажа.");

        drone.Slots.Remove(slot);
        drone.Cargo.Add(module);
        State.Station.ModuleChanges++;
        if (slot == EquipmentSlot.Lidar)
            drone.LidarScanned = false;
        return Accept($"Снято: {Equipment.Name(module.Kind)}.");
    }

    private EngineResult Salvage(string? id)
    {
        var drone = ActiveDrone;
        if (State.Mode == ControlMode.Laser || !drone.Powered ||
            !_donors.TryGetValue(drone.Position, out var donor))
            return Reject("Разборка доступна только рядом с донором при управлении дроном.");

        var components = State.Donors[donor.Id];
        if (components.Find(component => component.Id == id) is not { } module)
            return Reject("Деталь уже снята или отсутствует.");
        if (Weight(drone) + Equipment.Weight(module.Kind) > _definitions[drone.Id].MaxWeight)
            return Reject("Недостаточно грузоподъёмности.");
        if (!Spend(drone, Definition.Balance.ToolUse))
            return Reject("Недостаточно энергии для разборки.");

        components.Remove(module);
        drone.Cargo.Add(module);
        return Accept($"Извлечено: {Equipment.Name(module.Kind)}.");
    }

    private EngineResult UseTool(string? slotName)
    {
        var drone = ActiveDrone;
        if (State.Mode == ControlMode.Laser || !TrySlot(slotName, out var slot) ||
            !drone.Slots.TryGetValue(slot, out var tool) || tool.Kind is not (EquipmentKind.Gripper or EquipmentKind.Hammer))
            return Reject("Инструмент не установлен или недоступен.");
        if (!Spend(drone, Definition.Balance.ToolUse))
            return Reject("Недостаточно энергии для инструмента.");
        return Accept($"Активирован инструмент: {Equipment.Name(tool.Kind)}.");
    }

    private bool CanService => State.Mode != ControlMode.Laser && OnStation;

    private static bool TrySlot(string? name, out EquipmentSlot slot) =>
        Enum.TryParse(name, ignoreCase: true, out slot) && Enum.IsDefined(slot);

    private static bool Spend(DroneState drone, decimal amount)
    {
        if (drone.Battery is not { Energy: > 0 } battery)
            return false;
        drone.Slots[EquipmentSlot.Battery] = battery with { Energy = Math.Max(0, battery.Energy - amount) };
        return true;
    }

    private void ScanWithLidar(DroneState drone) =>
        drone.LidarScanned = drone.Slots.ContainsKey(EquipmentSlot.Lidar) && _map.Interior(drone.Position) != null &&
            Spend(drone, Definition.Balance.LidarScan);
}
