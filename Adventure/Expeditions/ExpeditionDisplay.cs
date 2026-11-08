using Adventure.Entities.Worlds;
using Foxnet.Session;
using static Adventure.ConsoleMapBuffer;

namespace Adventure.Expeditions;

internal sealed class ExpeditionDisplay
{
    private const int HeaderHeight = 5;
    private Glyph[][] _previous = [];
    private int _width;
    private int _height;

    public static (int Width, int Height) Size() =>
        (Math.Min(Console.WindowWidth, Console.BufferWidth), Math.Min(Console.WindowHeight, Console.BufferHeight));

    public void Invalidate() => _previous = [];

    public void Draw(ExpeditionEngine engine, string message, bool preserveMessage)
    {
        var (width, height) = Size();
        var rows = Blank(width, height);
        var drone = engine.ActiveDrone;
        var info = engine.DroneInfo(drone.Id);
        var level = engine.Levels[engine.ViewPosition.LevelId];
        Put(rows, 0, $"Квадрант {level.Name} | {engine.State.Mode} | {drone.Id} {info.Name}", ConsoleColor.Cyan);
        var battery = drone.Battery;
        Put(rows, 1, $"Цвет: {info.Color} | Заряд: {battery?.Percent ?? 0:0.#}% " +
            $"({battery?.Energy ?? 0:0.##}/{battery?.Capacity ?? 0:0.##} GAh) | Лидар: " +
            (drone.LidarScanned && drone.Powered && engine.State.Mode != ControlMode.Laser ? "сканирует"
                : drone.Slots.ContainsKey(EquipmentSlot.Lidar) ? "установлен" : "нет"), ConsoleColor.White);
        Put(rows, 2, "Сообщение: " + message, ConsoleColor.White);
        var cargo = drone.Items.Select(kind => WorldContent.CreateItem(kind)!.Name)
            .Concat(drone.Cargo.Select(module => Equipment.Name(module.Kind)));
        Put(rows, 3, "Инвентарь: " + string.Join(", ", cargo), ConsoleColor.Gray);
        Put(rows, 4, "WASD · Tab режим · F парк · E действие · C заряд · Q/R инструменты · Esc выход", ConsoleColor.DarkGray);

        var focus = engine.ViewPosition;
        var map = level.Map;
        int mapWidth = Math.Min(width, map.Width);
        int mapHeight = Math.Min(Math.Max(0, height - HeaderHeight), map.Height);
        int left = Math.Clamp(focus.X - mapWidth / 2, 0, map.Width - mapWidth);
        int top = Math.Clamp(focus.Y - mapHeight / 2, 0, map.Height - mapHeight);
        for (int y = 0; y < mapHeight; y++)
        for (int x = 0; x < mapWidth; x++)
        {
            var cell = engine.ViewCell(new WorldPosition(level.Id, x + left, y + top));
            bool black = cell.DroneId != null && cell.Color == ConsoleColor.Black ||
                cell.Glyph == '≈' && cell.Color == ConsoleColor.Black;
            var background = black ? ConsoleColor.Gray
                : cell is { DroneId: not null, Laser: true } ? ConsoleColor.DarkCyan : ConsoleColor.Black;
            var color = cell.Laser && cell.DroneId == null ? ConsoleColor.Cyan : cell.Color;
            rows[y + HeaderHeight][x] = new Glyph(cell.Laser && cell.DroneId == null ? '+' : cell.Glyph, color, background);
        }
        if (engine.State.GameOver)
            Put(rows, 2, "GAME OVER · Все дроны разряжены.", ConsoleColor.Red);

        Present(rows, width, height, preserveMessage && !engine.State.GameOver);
    }

    public void Menu(string title, IReadOnlyList<string> lines, int selected, string message)
    {
        var (width, height) = Size();
        var rows = Blank(width, height);
        Put(rows, 0, title, ConsoleColor.Cyan);
        Put(rows, 1, "↑/↓ выбор · Enter подтвердить · Esc закрыть", ConsoleColor.Gray);
        Put(rows, 2, message, ConsoleColor.Yellow);
        int capacity = Math.Max(1, height - 4);
        int first = Math.Clamp(selected - capacity + 1, 0, Math.Max(0, lines.Count - capacity));
        for (int i = first; i < Math.Min(first + capacity, lines.Count); i++)
            Put(rows, 4 + i - first, (i == selected ? "› " : "  ") + lines[i],
                i == selected ? ConsoleColor.Cyan : ConsoleColor.White);
        Present(rows, width, height, false);
    }

    private static Glyph[][] Blank(int width, int height)
    {
        var rows = new Glyph[height][];
        for (int y = 0; y < height; y++)
        {
            rows[y] = new Glyph[width];
            Array.Fill(rows[y], new Glyph(' ', ConsoleColor.Gray, ConsoleColor.Black));
        }
        return rows;
    }

    private static void Put(Glyph[][] rows, int y, string text, ConsoleColor color)
    {
        if (y >= rows.Length)
            return;
        for (int x = 0; x < Math.Min(text.Length, rows[y].Length); x++)
            rows[y][x] = new Glyph(text[x], color, ConsoleColor.Black);
    }

    private void Present(Glyph[][] rows, int width, int height, bool preserveMessage)
    {
        if (_width != width || _height != height)
        {
            _width = width;
            _height = height;
            Invalidate();
        }

        for (int y = 0; y < rows.Length; y++)
        {
            var previous = y < _previous.Length ? _previous[y] : null;
            if (preserveMessage && y == 2)
            {
                rows[y] = [];
                continue;
            }

            var row = rows[y];
            if (previous?.Length != row.Length)
            {
                WriteRow(0, y, row);
                continue;
            }

            int x = 0;
            while (x < row.Length)
            {
                if (Equal(previous[x], row[x]))
                {
                    x++;
                    continue;
                }

                int start = x++;
                while (x < row.Length && !Equal(previous[x], row[x]))
                    x++;
                WriteRow(start, y, row[start..x]);
            }
        }
        _previous = rows;
    }

    private static bool Equal(Glyph left, Glyph right) =>
        left.Character == right.Character && left.Attributes == right.Attributes;
}
