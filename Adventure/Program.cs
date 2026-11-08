using Adventure.Entities.Levels;
using Adventure.Entities.Worlds;
using System.Text;

namespace Adventure
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length > 1)
            {
                Console.Error.WriteLine("Использование: Adventure [путь к world.json]");
                return 1;
            }

            InitLevels levels;
            try
            {
                levels = new InitLevels(WorldFiles.Load(args.Length == 1 ? args[0] : WorldFiles.DefaultPath));
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Prologue.PrologueStart(false, true);
            Console.CursorVisible = false;
            try
            {
                Game.StartGame(levels);
            }
            finally
            {
                Console.CursorVisible = true;
                Console.ResetColor();
            }
            return 0;
        }
    }
}
