using Adventure.Entities.Actors;
using Adventure.Entities.Characters;
using Adventure.Entities.Maps;

namespace Adventure
{
    internal static class ConsoleView
    {
        private const int MapTop = 3;
        private static readonly Border Border = new();

        public static void DrawLevel(Character character)
        {
            var level = character.Level;
            Console.BackgroundColor = level.Color;
            Console.Clear();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Квадрант " + level.Name);
            WriteInventory(character);
            Console.WriteLine("Сообщение: ");

            var map = level.Map;
            var row = new ConsoleMapBuffer.Glyph[map.Width + 1];
            row[^1] = new(Border.Icon[0], Border.Color, level.Color);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                    row[x] = MapGlyph(map[x, y], level.Color);

                ConsoleMapBuffer.WriteRow(0, y + MapTop, row);
            }
            Array.Fill(row, new('_', Border.Color, level.Color));
            row[^1] = new('~', Border.Color, level.Color);
            ConsoleMapBuffer.WriteRow(0, map.Height + MapTop, row);
            DrawCharacter(character);
        }

        public static void MoveCharacter(Character character, int previousX, int previousY)
        {
            var level = character.Level;
            ConsoleMapBuffer.WriteRow(previousX, previousY + MapTop, [MapGlyph(level.Map[previousX, previousY], level.Color)]);
            DrawCharacter(character);
        }

        private static void DrawCharacter(Character character) =>
            ConsoleMapBuffer.WriteRow(character.X, character.Y + MapTop,
                [new('X', ConsoleColor.White, character.Level.Color)]);

        private static ConsoleMapBuffer.Glyph MapGlyph(Cell cell, ConsoleColor background) =>
            new(cell.Fill[0], cell.VisibleActor?.Color ?? ConsoleColor.Gray, background);

        public static void ShowInteraction(Character character, Interaction interaction)
        {
            switch (interaction.Result)
            {
                case InteractionResult.Action:
                    interaction.Actor!.Action();
                    break;
                case InteractionResult.InventoryChanged:
                    if (interaction.Actor is Terminal)
                    {
                        interaction.Actor.Action();
                        RefreshInventory(character);
                    }
                    else
                    {
                        RefreshInventory(character);
                        interaction.Actor!.Action();
                    }
                    break;
                case InteractionResult.Completed:
                    RefreshInventory(character);
                    interaction.Actor!.Action();
                    FinalMessage(character.Level.Map);
                    break;
                case InteractionResult.TerminalOff:
                    ShowMessage("Терминал отключен");
                    break;
                case InteractionResult.MissingItem:
                    ShowMessage("Невозможно считать данные. Нет подходящего носителя.");
                    break;
                case InteractionResult.EmptyChest:
                    ShowMessage("Ящик пуст.");
                    break;
                case InteractionResult.Overweight:
                    ShowMessage("Вы не можете взять предмет. Превышен показатель веса.", ConsoleColor.Red);
                    break;
            }
        }

        public static void ShowConnectionError() =>
            ShowMessage("Передвижение по заданным координатам запрещено; Соединение будет потеряно.", ConsoleColor.Red);

        private static void ShowMessage(string message, ConsoleColor color = ConsoleColor.White)
        {
            ClearLine(Actor.MessageY);
            Console.ForegroundColor = color;
            Console.WriteLine("Сообщение: " + message);
            Console.ForegroundColor = ConsoleColor.White;
        }

        private static void RefreshInventory(Character character)
        {
            ClearLine(1);
            WriteInventory(character);
        }

        public static void ClearLine(int y)
        {
            Console.SetCursorPosition(0, y);
            Console.Write(new string(' ', Console.BufferWidth));
            Console.SetCursorPosition(0, y);
        }

        private static void WriteInventory(Character character) =>
            Console.WriteLine("Инвентарь: " + string.Join(", ", character.Inventory.Items.Select(item => item.Name)));

        internal static readonly string[] CompletionLines =
        [
            "+--------------------------------------+",
            "|-------------ПОЗДРАВЛЯЮ!--------------|",
            "|-----------ВЫ ПРОШЛИ ИГРУ!------------|",
            "|-ВЫ МОЖЕТЕ ИССЛЕДОВАТЬ КАРТУ ЕЩЕ РАЗ,-|",
            "|-------ЛИБО СРАЗУ ЗАКРЫТЬ ИГРУ.-------|",
            "|---СПАСИБО, ЧТО ИГРАЛИ В МОЮ ИГРУ!----|",
            "|-----ВАШ ДЕНИС KOTONETR ПОЛУРОТОВ-----|",
            "+--------------------------------------+"
        ];

        private static void FinalMessage(Map map)
        {
            var lines = CompletionLines;
            int left = Math.Max(0, (map.Width - lines[0].Length) / 2);
            int top = MapTop + Math.Max(0, (map.Height - lines.Length) / 2);
            Console.ForegroundColor = ConsoleColor.Green;
            for (int y = 0; y < lines.Length; y++)
            {
                Console.SetCursorPosition(left, top + y);
                Console.Write(lines[y]);
            }
            Console.ForegroundColor = ConsoleColor.White;
        }
    }
}
