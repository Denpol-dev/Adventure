using Adventure.Entities.Worlds;
using Foxnet.Session;

namespace Adventure.Expeditions;

public sealed record CargoEntry(string Id, string Group, string Name, int Weight, string Detail);
public sealed record InteractionTarget(string Id, string Name);

public sealed partial class ExpeditionEngine
{
    public int CargoCapacity => _definitions[ActiveDrone.Id].MaxWeight;
    public int CargoWeight => Weight(ActiveDrone);
    public DropCrate? CrateHere => State.DropCrates.Find(crate => crate.Position == ActiveDrone.Position);

    public IReadOnlyList<CargoEntry> Inventory() => Describe(ActiveDrone.Goods, ActiveDrone.Cargo);

    public IReadOnlyList<CargoEntry> CrateContents(string id) =>
        CrateHere is { } crate && crate.Id == id ? Describe(crate.Items, crate.Modules) : [];

    public IReadOnlyList<InteractionTarget> InteractionTargets()
    {
        if (State.Mode == ControlMode.Laser)
            return [];
        var drone = ActiveDrone;
        var targets = new List<InteractionTarget>();
        if (drone.Position == _map.Terminal)
            targets.Add(new("station", "Терминал обслуживания"));
        if (!drone.Powered)
            return targets;
        if (_donors.TryGetValue(drone.Position, out var donor))
            targets.Add(new("donor:" + donor.Id, donor.Name));
        if (CrateHere is { } crate)
            targets.Add(new("crate:" + crate.Id, "Ящик со сброшенным грузом"));
        targets.AddRange(_map.CellAt(drone.Position).Objects.Where(obj => obj.Id != Definition.Station.ServiceTerminalId)
            .Select(obj => new InteractionTarget("object:" + obj.Id, obj.Actor.Name ?? obj.Id)));
        return targets;
    }

    private EngineResult Drop(IReadOnlyList<string>? ids)
    {
        var drone = ActiveDrone;
        if (State.Mode == ControlMode.Laser || !drone.Powered)
            return Reject("Для выгрузки нужен активный дрон.");
        if (ids == null || !TrySelectCargo(drone.Goods, drone.Cargo, ids, out var items, out var modules))
            return Reject("Часть выбранного груза больше недоступна.");

        var crate = CrateHere;
        if (crate == null)
        {
            crate = new DropCrate(Guid.NewGuid().ToString("N"), drone.Position);
            State.DropCrates.Add(crate);
        }
        crate.Items.AddRange(items);
        crate.Modules.AddRange(modules);
        foreach (var item in items)
            drone.Goods.Remove(item);
        foreach (var module in modules)
            drone.Cargo.Remove(module);
        return Accept($"Сброшено предметов: {ids.Count}.");
    }

    private EngineResult Take(string? crateId, IReadOnlyList<string>? ids)
    {
        var drone = ActiveDrone;
        if (State.Mode == ControlMode.Laser || !drone.Powered || CrateHere is not { } crate || crate.Id != crateId)
            return Reject("Ящик недоступен.");

        int remaining = CargoCapacity - CargoWeight;
        if (ids == null)
        {
            var fitting = new List<string>();
            foreach (var entry in Describe(crate.Items, crate.Modules).OrderBy(entry => entry.Weight))
            {
                if (entry.Weight > remaining)
                    break;
                fitting.Add(entry.Id);
                remaining -= entry.Weight;
            }
            ids = fitting;
            remaining = CargoCapacity - CargoWeight;
        }
        if (ids.Count == 0)
            return Reject("В инвентаре нет места для груза из ящика.");
        if (!TrySelectCargo(crate.Items, crate.Modules, ids, out var items, out var modules))
            return Reject("Часть выбранного груза больше недоступна.");
        if (items.Sum(ItemWeight) + modules.Sum(module => Equipment.Weight(module.Kind)) > remaining)
            return Reject("Недостаточно грузоподъёмности для выбранного груза.");

        drone.Goods.AddRange(items);
        drone.Cargo.AddRange(modules);
        foreach (var item in items)
            crate.Items.Remove(item);
        foreach (var module in modules)
            crate.Modules.Remove(module);
        if (crate.Items.Count + crate.Modules.Count == 0)
            State.DropCrates.Remove(crate);
        return Accept($"Подобрано предметов: {ids.Count}.");
    }

    private static bool TrySelectCargo(List<CargoItem> sourceItems, List<ModuleState> sourceModules,
        IReadOnlyList<string> ids, out CargoItem[] items, out ModuleState[] modules)
    {
        var selected = ids.ToHashSet(StringComparer.Ordinal);
        items = sourceItems.Where(item => selected.Contains(item.Id)).ToArray();
        modules = sourceModules.Where(module => selected.Contains(module.Id)).ToArray();
        return selected.Count > 0 && selected.Count == ids.Count && items.Length + modules.Length == ids.Count;
    }

    private static IReadOnlyList<CargoEntry> Describe(List<CargoItem> items, List<ModuleState> modules) =>
        items.Select(item =>
        {
            var definition = WorldContent.CreateItem(item.Kind)!;
            return new CargoEntry(item.Id, "item:" + item.Kind, definition.Name, definition.Weight,
                definition.IsPlotItem ? "сюжетный предмет" : "");
        }).Concat(modules.Select(module => new CargoEntry(module.Id,
            module.Capacity > 0 ? module.Id : "module:" + module.Kind,
            Equipment.Name(module.Kind), Equipment.Weight(module.Kind),
            module.Capacity > 0 ? $"{module.Energy:0.##}/{module.Capacity:0.##} GAh · {module.Id}" : "съёмный модуль"))).ToArray();

    private static int ItemWeight(CargoItem item) => WorldContent.CreateItem(item.Kind)!.Weight;

    private void RestoreLegacyCargo()
    {
        foreach (var drone in State.Drones.Values)
        {
            if (drone.LegacyItems is not { } items)
                continue;
            if (drone.Goods.Count > 0)
                throw new InvalidDataException("Сохранение содержит две версии одного инвентаря.");
            for (int index = 0; index < items.Count; index++)
                drone.Goods.Add(new CargoItem($"legacy:{drone.Id}:{index}", items[index]));
            drone.LegacyItems = null;
        }
    }
}
