using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Adventure
{
    internal static class ConsoleMapBuffer
    {
        private const int StandardOutput = -11;

        [StructLayout(LayoutKind.Sequential)]
        internal readonly struct Glyph(char symbol, ConsoleColor foreground, ConsoleColor background)
        {
            public readonly ushort Character = symbol;
            public readonly ushort Attributes = (ushort)((int)foreground | ((int)background << 4));
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct Coordinates(short x, short y)
        {
            public readonly short X = x;
            public readonly short Y = y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Region
        {
            public short Left;
            public short Top;
            public short Right;
            public short Bottom;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int standardHandle);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WriteConsoleOutputW(IntPtr output, [In] Glyph[] buffer, Coordinates size,
            Coordinates origin, ref Region region);

        public static void WriteRow(int x, int y, Glyph[] glyphs)
        {
            var region = new Region
            {
                Left = checked((short)x),
                Top = checked((short)y),
                Right = checked((short)(x + glyphs.Length - 1)),
                Bottom = checked((short)y)
            };

            if (!WriteConsoleOutputW(GetStdHandle(StandardOutput), glyphs, new(checked((short)glyphs.Length), 1),
                    new(0, 0), ref region))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
    }
}
