namespace Adventure.Entities
{
    public enum Vector
    {
        Left = 1,
        Right = 2,
        Top = 3,
        Bottom = 4,
    }

    internal static class Directions
    {
        public static (int X, int Y) Offset(this Vector direction) => direction switch
        {
            Vector.Left => (-1, 0),
            Vector.Right => (1, 0),
            Vector.Top => (0, -1),
            Vector.Bottom => (0, 1),
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };

        public static Vector Opposite(this Vector direction) => direction switch
        {
            Vector.Left => Vector.Right,
            Vector.Right => Vector.Left,
            Vector.Top => Vector.Bottom,
            Vector.Bottom => Vector.Top,
            _ => throw new ArgumentOutOfRangeException(nameof(direction))
        };
    }
}
