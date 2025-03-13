namespace Adventure.Entities.Characters
{
    public class Character
    {
        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public int HP { get; set; } = 100;
        public Inventory Inventory { get; set; } = new Inventory();

        public void SetCharacterOnMap(Map map)
        {
            X = 1;
            Y = 7;

            Console.SetCursorPosition(X, Y);
            DrawCharacter();
        }

        public void DoAction(string key, Map map)
        {
            Cell cell;
            switch (key)
            {
                case "W":
                    if (Y > 1)
                    {
                        Console.SetCursorPosition(X, Y);
                        cell = map.Cells.First(c => c.X == X && c.Y == Y);
                        EraseCharacter(cell.CellType);

                        Console.SetCursorPosition(X, --Y);
                        DrawCharacter();
                    }
                    else
                    {

                    }
                    break;
                case "S":
                    if (Y < map.Height)
                    {
                        Console.SetCursorPosition(X, Y);
                        cell = map.Cells.First(c => c.X == X && c.Y == Y);
                        EraseCharacter(cell.CellType);

                        Console.SetCursorPosition(X, ++Y);
                        DrawCharacter();
                    }
                    break;
                case "A":
                    if (X > 0)
                    {
                        Console.SetCursorPosition(X, Y);
                        cell = map.Cells.First(c => c.X == X && c.Y == Y);
                        EraseCharacter(cell.CellType);

                        Console.SetCursorPosition(--X, Y);
                        DrawCharacter();
                    }
                    break;
                case "D":
                    if (X < map.Width)
                    {
                        Console.SetCursorPosition(X, Y);
                        cell = map.Cells.First(c => c.X == X && c.Y == Y);
                        EraseCharacter(cell.CellType);

                        Console.SetCursorPosition(++X, Y);
                        DrawCharacter();
                    }
                    break;
                case "E":
                    cell = map.Cells.First(c => c.X == X && c.Y == Y);
                    cell.CellType.Actor.Action();
                    break;
                default:
                    break;
            }
        }

        private static void DrawCharacter()
        {
            Console.ForegroundColor = ConsoleColor.Black;
            Console.Write("X");
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void EraseCharacter(CellType type)
        {
            Console.ForegroundColor = type.Actor?.Color ?? ConsoleColor.Black;
            Console.Write(type.Fill);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
