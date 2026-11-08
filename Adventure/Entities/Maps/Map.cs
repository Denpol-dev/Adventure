using Adventure.Entities.Actors;
using Adventure.Entities.Items;
using Adventure.Entities.Worlds;

namespace Adventure.Entities.Maps
{
    public sealed class Map
    {
        private readonly Cell[,] _cells;

        internal Map(LevelDefinition definition)
        {
            Width = definition.Width;
            Height = definition.Height;
            _cells = new Cell[Height, Width];

            var objects = definition.Objects.ToLookup(obj => (obj.X, obj.Y));
            var properties = definition.Cells.ToDictionary(cell => (cell.X, cell.Y));
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    _cells[y, x] = WorldContent.CreateCell(definition.Rows[y][x],
                        properties.GetValueOrDefault((x, y)), objects[(x, y)]);
                }
            }
        }

        public int Width { get; }
        public int Height { get; }

        public Cell this[int x, int y] => _cells[y, x];

        public bool Contains(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
    }

    public sealed record PlacedObject(string Id, Actor Actor);

    public sealed record Cell(char Symbol, Actor? Terrain, CellProperties? Properties, IReadOnlyList<PlacedObject> Objects)
    {
        public Actor? VisibleActor => Objects.FirstOrDefault()?.Actor ?? Terrain;
        public string Fill => VisibleActor?.Icon ?? Symbol.ToString();

        public bool CanEnter(IReadOnlyCollection<Item> inventory) =>
            !(Properties?.BlocksMovement ?? Terrain?.IsCollision ?? (Symbol == '◉')) &&
            Objects.All(obj => obj.Actor is ForceField field
                ? inventory.Any(item => item.GetType() == field.Pass.GetType())
                : !obj.Actor.IsCollision);
    }
}
