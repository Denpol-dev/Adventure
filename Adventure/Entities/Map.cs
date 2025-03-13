using Adventure.Entities.Actors;

namespace Adventure.Entities
{
    public class Map
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public List<Cell> Cells { get; set; } = [];
    }

    public class Cell
    {
        public int X { get; set; }
        public int Y { get; set; }
        public CellType CellType { get; set; } = new CellType();
    }

    public class CellType
    {
        public string Fill { get; set; } = ".";
        public Actor Actor { get; set; } = new Sand();
    }
}
