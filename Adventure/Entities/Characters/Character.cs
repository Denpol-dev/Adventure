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
                            _program.ChangeLevel(level, this, Vector.Top);
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
                            _program.ChangeLevel(level, this, Vector.Bottom);
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
                            _program.ChangeLevel(level, this, Vector.Left);
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
                            _program.ChangeLevel(level, this, Vector.Right);
                        }
                        else
                        {
                            ConnectionErrorMessage();
                        }
                    }
                    break;
                case "E":
                    {
                        cell = map.Cells.First(c => c.X == X && c.Y == Y);

                        if (cell.CellType.Actor is Terminal terminal)
                        {
                            if (terminal.LoadItem is Item itemLoad)
                            {
                                if (itemLoad is HardDriveXD23Full hdd)
                                {
                                    if (Inventory.Items.Select(i => i.Name).Contains(hdd.Name))
                                    {
                                        DeleteItemFromInventory(hdd);
                                        AddItemToInventory(new HardDriveXD23());
                                        cell.CellType.Actor.Action();
                                        FinalMessage(map);
                                    }
                                }
                                else
                                {
                                    if (Inventory.Items.Select(i => i.Name).Contains(itemLoad.Name))
                                    {
                                        if (cell.CellType.Actor.Inventory is Item item)
                                        {
                                            cell.CellType.Actor.Action();
                                            DeleteItemFromInventory(itemLoad);
                                            AddItemToInventory(item);
                                        }
                                    }
                                    else
                                    {
                                        if (terminal.Inventory is Item item)
                                        {
                                            if (Inventory.Items.Select(i => i.Name).Contains(item.Name))
                                            {
                                                TerminalErrorMessage();
                                            }
                                            else
                                            {
                                                LoadErrorMessage();
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                cell.CellType.Actor.Action();
                            }
                        }
                        else
                        {
                            if (cell.CellType.Actor?.IsTakeble ?? false)
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
                                else
                                {
                                    cell.CellType.Actor?.Action();
                                }
                            }
                            else
                            {
                                cell.CellType.Actor?.Action();
                            }
                        }

                        break;
                    }
                default:
                    break;
            }
        }

        private static void LoadErrorMessage()
        {
            Console.SetCursorPosition(Actor.MessageX, Actor.MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

            string message = "Сообщение: ";
            Console.WriteLine(message + "Невозможно считать данные. Нет подходящего носителя.");
        }

        private static void TerminalErrorMessage()
        {
            Console.SetCursorPosition(Actor.MessageX, Actor.MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

            string message = "Сообщение: ";
            Console.WriteLine(message + "Терминал отключен");
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
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

            var sb = new StringBuilder();
            var inventory = Inventory.Items;
            foreach (var i in inventory)
            {
                sb.Append(i.Name + ", ");
            }
            sb.Length -= 2;
            Console.WriteLine("Инвентарь: " + sb.ToString());
        }

        private void DeleteItemFromInventory(Item item)
        {
            Inventory.Items.RemoveAll(i => i.Name == item.Name);
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

        private bool CheckCollision(Map map, int x, int y)
        {
            var cellType = map.Cells.First(c => c.X == x && c.Y == y).CellType;
            var actor = cellType.Actor;

            if (actor is ForceField field)
            {
                if (CheckInventory(field.Pass))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return !actor?.IsCollision ?? false;
            }
        }

        private bool CheckInventory(Item checkItem)
        {
            foreach (var item in Inventory.Items)
            {
                if (checkItem.GetType() == item.GetType())
                {
                    return true;
                }
            }
            return false;
        }

        private void FinalMessage(Map map)
        {
            Console.ForegroundColor = ConsoleColor.Green;

            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2) - 4);
            Console.Write("+--------------------------------------+");
            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2) - 3);
            Console.Write("|-------------ПОЗДРАВЛЯЮ!--------------|");
            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2) - 2);
            Console.Write("|-----------ВЫ ПРОШЛИ ИГРУ!------------|");
            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2) - 1);
            Console.Write("|-ВЫ МОЖЕТЕ ИССЛЕДОВАТЬ КАРТУ ЕЩЕ РАЗ,-|");
            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2));
            Console.Write("|-------ЛИБО СРАЗУ ЗАКРЫТЬ ИГРУ.-------|");
            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2) + 1);
            Console.Write("|---СПАСИБО, ЧТО ИГРАЛИ В МОЮ ИГРУ!----|");
            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2) + 2);
            Console.Write("|-----ВАШ ДЕНИС KOTONETR ПОЛУРОТОВ-----|");
            Console.SetCursorPosition((map.Width / 2) - 20, (map.Height / 2) + 3);
            Console.Write("+--------------------------------------+");

            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void DrawCharacter()
        {
            Console.Write("X");
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
