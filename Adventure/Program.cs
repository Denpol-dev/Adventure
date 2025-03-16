using Adventure.Entities.Characters;
using Adventure.Entities.Levels;
using Adventure;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Adventure
{
    public class Program
    {
        #region Import dll

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleDisplayMode(IntPtr ConsoleOutput, uint Flags, out COORD NewScreenBufferDimensions);
        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            public short X;
            public short Y;

            public COORD(short X, short Y)
            {
                this.X = X;
                this.Y = Y;
            }
        }

        #endregion

        public Level Level { get; set; }
        public Character Character { get; set; }

        public void ChangeLevel(Level level, Character character)
        {
            Level = level;
            Console.Clear();
            Console.BackgroundColor = level.LevelColor;
            level.LoadMap(character);
            int x = character.X;
            int y = character.Y;
            character.SetCharacterOnMap(x, y);
        }

        public static void Main()
        {
            IntPtr hConsole = GetStdHandle(-11);
            SetConsoleDisplayMode(hConsole, 1, out COORD b1);
            var program = new Program();
            SetConsoleOutputCP(932);
            SetConsoleCP(932);
            Console.OutputEncoding = Encoding.Unicode;

            Prologue.PrologueStart(true);
            Console.CursorVisible = false;
            Game.StartGame(program);
        }
    }
}