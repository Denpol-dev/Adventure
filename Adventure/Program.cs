using Adventure.Entities.Characters;
using Adventure.Entities.Levels;
using Adventure;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Adventure.Entities;

namespace Adventure
{
    public class Program
    {
        #region Import dll

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);

        #endregion

        public Level Level { get; set; }
        public Character Character { get; set; }

        public void ChangeLevel(Level level, Character character, Vector vector)
        {
            Level = level;
            Console.Clear();
            Console.BackgroundColor = level.LevelColor;
            level.LoadMap(character);
            int x = 0;
            int y = 0;
            switch (vector)
            {
                case Vector.Left:
                    x = Level.GetMap().Width;
                    y = character.Y;
                    break;
                case Vector.Right:
                    x = 0;
                    y = character.Y;
                    break;
                case Vector.Top:
                    x = character.X;
                    y = Level.GetMap().Height;
                    break;
                case Vector.Bottom:
                    x = character.X;
                    y = 3;
                    break;
                default:
                    break;
            }
            character.SetCharacterOnMap(x, y);
        }

        public static void Main()
        {
            var program = new Program();
            SetConsoleOutputCP(932);
            SetConsoleCP(932);
            Console.OutputEncoding = Encoding.Unicode;

            Prologue.PrologueStart(true, true);
            Console.CursorVisible = false;
            Game.StartGame(program);
        }
    }
}