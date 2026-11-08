using Adventure.Entities.Actors;
using Adventure.Entities.Levels;

namespace Adventure.Entities.Characters
{
    public sealed class Character
    {
        private readonly HashSet<string> _looted = [];
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

        public Interaction Interact() =>
            WorldInteraction.Apply(Level.Map[X, Y], Inventory.Items, MaxWeight, _looted);
    }

    public enum MoveResult { Moved, ChangedLevel, Blocked, NoNeighbor }

    public enum InteractionResult { None, Action, InventoryChanged, Completed, TerminalOff, MissingItem, EmptyChest, Overweight }

    public sealed record Interaction(InteractionResult Result, Actor? Actor = null);
}
