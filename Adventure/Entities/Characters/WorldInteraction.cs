using Adventure.Entities.Actors;
using Adventure.Entities.Items;
using Adventure.Entities.Maps;

namespace Adventure.Entities.Characters;

internal static class WorldInteraction
{
    public static string Message(InteractionResult result) => result switch
    {
        InteractionResult.EmptyChest => "Ящик пуст.",
        InteractionResult.Overweight => "Вы не можете взять предмет. Превышен показатель веса.",
        InteractionResult.TerminalOff => "Терминал отключен",
        InteractionResult.MissingItem => "Невозможно считать данные. Нет подходящего носителя.",
        _ => ""
    };

    public static Interaction Apply(Cell cell, List<Item> inventory, int maxWeight, ISet<string> looted)
    {
        var placed = cell.Objects.FirstOrDefault();
        var actor = cell.VisibleActor;
        if (actor is Terminal { LoadItem: not null } terminal)
        {
            int index = inventory.FindIndex(item => item.GetType() == terminal.LoadItem.GetType());
            if (index < 0)
            {
                if (terminal.CompletesGame)
                    return new(InteractionResult.None);

                bool hasOutput = inventory.Any(item => item.GetType() == terminal.Inventory?.GetType());
                return new(hasOutput ? InteractionResult.TerminalOff : InteractionResult.MissingItem);
            }

            inventory[index] = terminal.Inventory!;
            return new(terminal.CompletesGame ? InteractionResult.Completed : InteractionResult.InventoryChanged, terminal);
        }

        if (placed != null && actor is { IsTakeble: true, Inventory: { } item })
        {
            if (looted.Contains(placed.Id))
                return new(InteractionResult.EmptyChest);
            if (inventory.Sum(existing => existing.Weight) + item.Weight > maxWeight)
                return new(InteractionResult.Overweight);

            inventory.Add(item);
            looted.Add(placed.Id);
            return new(InteractionResult.InventoryChanged, actor);
        }

        return actor == null ? new(InteractionResult.None) : new(InteractionResult.Action, actor);
    }
}
