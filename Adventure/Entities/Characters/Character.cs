using Adventure.Entities.Actors;
using Adventure.Entities.Levels;

namespace Adventure.Entities.Characters
{
    public sealed class Character
    {
        public Character(InitLevels levels)
        {
            Level = levels.FirstLevel;
            X = levels.Start.X;
            Y = levels.Start.Y;
        }

        public Level Level { get; private set; }
        public int X { get; private set; }
        public int Y { get; private set; }
        public int HP { get; set; } = 100;
        public int MaxWeight { get; set; } = 10;
        public Inventory Inventory { get; } = new();

        public MoveResult Move(Vector direction)
        {
            var (dx, dy) = direction.Offset();
            int x = X + dx;
            int y = Y + dy;
            var destination = Level;
            if (!Level.Map.Contains(x, y))
            {
                if (Level.GetNeighbor(direction) is not Level neighbor)
                    return MoveResult.NoNeighbor;

                destination = neighbor;
                x = direction switch
                {
                    Vector.Left => neighbor.Map.Width - 1,
                    Vector.Right => 0,
                    _ => X
                };
                y = direction switch
                {
                    Vector.Top => neighbor.Map.Height - 1,
                    Vector.Bottom => 0,
                    _ => Y
                };
            }

            if (!destination.Map.Contains(x, y) || !destination.Map[x, y].CanEnter(Inventory.Items))
                return MoveResult.Blocked;

            var result = destination == Level ? MoveResult.Moved : MoveResult.ChangedLevel;
            Level = destination;
            X = x;
            Y = y;
            return result;
        }

        public Interaction Interact()
        {
            var actor = Level.Map[X, Y].VisibleActor;
            if (actor is Terminal { LoadItem: not null } terminal)
            {
                int index = Inventory.Items.FindIndex(item => item.GetType() == terminal.LoadItem.GetType());
                if (index < 0)
                {
                    if (terminal.CompletesGame)
                        return new(InteractionResult.None);

                    bool hasOutput = Inventory.Items.Any(item => item.GetType() == terminal.Inventory?.GetType());
                    return new(hasOutput ? InteractionResult.TerminalOff : InteractionResult.MissingItem);
                }

                Inventory.Items[index] = terminal.Inventory!;
                return new(terminal.CompletesGame ? InteractionResult.Completed : InteractionResult.InventoryChanged, terminal);
            }

            if (actor is { IsTakeble: true, Inventory: not null })
            {
                var item = actor.Inventory;
                if (Inventory.Items.Contains(item))
                    return new(InteractionResult.EmptyChest);
                if (Inventory.Items.Sum(existing => existing.Weight) + item.Weight > MaxWeight)
                    return new(InteractionResult.Overweight);

                Inventory.Items.Add(item);
                return new(InteractionResult.InventoryChanged, actor);
            }

            return actor == null ? new(InteractionResult.None) : new(InteractionResult.Action, actor);
        }
    }

    public enum MoveResult { Moved, ChangedLevel, Blocked, NoNeighbor }

    public enum InteractionResult { None, Action, InventoryChanged, Completed, TerminalOff, MissingItem, EmptyChest, Overweight }

    public sealed record Interaction(InteractionResult Result, Actor? Actor = null);
}
