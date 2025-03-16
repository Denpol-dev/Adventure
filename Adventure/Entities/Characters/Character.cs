using Adventure.Entities.Actors;
using Adventure.Entities.Items;
using Adventure.Entities.Levels;
using Adventure.Entities.Maps;
using System.Text;

namespace Adventure.Entities.Characters
{
    public class Character
    {
        private readonly Program _program;
        public Character(Program program)
        {
            _program = program;
        }

        public int X { get; set; } = 0;
        public int Y { get; set; } = 0;
        public int HP { get; set; } = 100;
        public int MaxWeight { get; set; } = 10;
        public Inventory Inventory { get; set; } = new Inventory();

        public void SetCharacterOnMap(int x = 1, int y = 7)
        {
            X = x;
            Y = y;

            Console.SetCursorPosition(X, Y);
            DrawCharacter();
        }

        public void DoAction(string key, Map map)
        {
            Cell cell;
            switch (key)
            {
                case "W":
                    if (Y > 3)
                    {
                        if (CheckCollision(map, X, Y - 1))
                        {
                            EraseCharacter(map);
                            Console.SetCursorPosition(X, --Y);
                            DrawCharacter();
                        }
                    }
                    else
                    {
                        if (_program.Level.TopLevel is Level level)
                        {
                            _program.ChangeLevel(level, this);
                        }
                        else
                        {
                            ConnectionErrorMessage();
                        }
                    }
                    break;
                case "S":
                    if (Y < map.Height)
                    {
                        if (CheckCollision(map, X, Y + 1))
                        {
                            EraseCharacter(map);
                            Console.SetCursorPosition(X, ++Y);
                            DrawCharacter();
                        }
                    }
                    else
                    {
                        if (_program.Level.BottomLevel is Level level)
                        {
                            _program.ChangeLevel(level, this);
                        }
                        else
                        {
                            ConnectionErrorMessage();
                        }
                    }
                    break;
                case "A":
                    if (X > 0)
                    {
                        if (CheckCollision(map, X - 1, Y))
                        {
                            EraseCharacter(map);
                            Console.SetCursorPosition(--X, Y);
                            DrawCharacter();
                        }
                    }
                    else
                    {
                        if (_program.Level.LeftLevel is Level level)
                        {
                            _program.ChangeLevel(level, this);
                        }
                        else
                        {
                            ConnectionErrorMessage();
                        }
                    }
                    break;
                case "D":
                    if (X < map.Width)
                    {
                        if (CheckCollision(map, X + 1, Y))
                        {
                            EraseCharacter(map);
                            Console.SetCursorPosition(++X, Y);
                            DrawCharacter();
                        }
                    }
                    else
                    {
                        if (_program.Level.RightLevel is Level level)
                        {
                            _program.ChangeLevel(level, this);
                        }
                        else
                        {
                            ConnectionErrorMessage();
                        }
                    }
                    break;
                case "E":
                    cell = map.Cells.First(c => c.X == X && c.Y == Y);

                    if (cell.CellType.Actor.IsTakeble)
                    {
                        if (cell.CellType.Actor.Inventory is Item item)
                        {
                            if (Inventory.Items.Contains(item))
                            {
                                EmptyChestMessage();
                                break;
                            }
                            if ((Inventory.Items.Select(i => i.Weight).Sum() + item.Weight) > MaxWeight)
                            {
                                MaxWeightErrorMessage();
                            }
                            else
                            {
                                AddItemToInventory(item);
                                cell.CellType.Actor.Action();
                            }
                            break;
                        }
                    }
                    else
                    {
                        cell.CellType.Actor.Action();
                    }
                    break;
                default:
                    break;
            }
        }

        private static void EmptyChestMessage()
        {
            Console.SetCursorPosition(Actor.MessageX, Actor.MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

            string message = "Сообщение: ";
            Console.WriteLine(message + "Ящик пуст.");
        }

        private void AddItemToInventory(Item item)
        {
            Inventory.Items.Add(item);
            Console.SetCursorPosition(0, 1);

            var sb = new StringBuilder();
            var inventory = Inventory.Items;
            foreach (var i in inventory)
            {
                sb.Append(i.Name + ", ");
            }
            sb.Length -= 2;
            Console.WriteLine("Инвентарь: " + sb.ToString());
        }

        private static void MaxWeightErrorMessage()
        {
            Console.SetCursorPosition(Actor.MessageX, Actor.MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

            string message = "Сообщение: ";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message + "Вы не можете взять предмет. Превышен показатель веса.");
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void ConnectionErrorMessage()
        {
            Console.SetCursorPosition(Actor.MessageX, Actor.MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

            string message = "Сообщение: ";
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message + "Передвижение по заданным координатам запрещено; Соединение будет потеряно.");
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void DrawCharacter()
        {
            Console.Write("X");
        }

        private bool CheckCollision(Map map, int x, int y)
        {
            var actor = map.Cells.First(c => c.X == x && c.Y == y).CellType.Actor;
            return !actor.IsCollision;
        }

        private void EraseCharacter(Map map)
        {
            Console.SetCursorPosition(X, Y);
            var type = map.Cells.First(c => c.X == X && c.Y == Y).CellType;
            Console.ForegroundColor = type.Actor?.Color ?? ConsoleColor.Gray;
            Console.Write(type.Fill);
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
