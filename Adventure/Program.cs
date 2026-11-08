using Adventure.Entities.Levels;
using Adventure.Entities.Worlds;
using Adventure.Expeditions;
using Foxnet.Session;
using Microsoft.Data.Sqlite;
using System.Text;

namespace Adventure
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            string? worldPath = null;
            string? sessionPath = null;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--session" && sessionPath == null && i + 1 < args.Length)
                    sessionPath = args[++i];
                else if (!args[i].StartsWith("--", StringComparison.Ordinal) && worldPath == null)
                    worldPath = args[i];
                else
                {
                    Console.Error.WriteLine("Использование: Adventure [путь к world.json] [--session путь к session.db]");
                    return 1;
                }
            }

            try
            {
                var world = WorldFiles.Load(worldPath ?? WorldFiles.DefaultPath);
                Console.InputEncoding = Encoding.UTF8;
                Console.OutputEncoding = Encoding.UTF8;

                if (world.Expedition == null)
                {
                    Prologue.PrologueStart(true, true);
                    RunWithHiddenCursor(() => Game.StartGame(new InitLevels(world)));
                }
                else
                {
                    sessionPath ??= ExpeditionAddress.DefaultDatabasePath;
                    using var expedition = new ExpeditionOwner(world, sessionPath);
                    await using var server = new ExpeditionPipeServer(expedition, sessionPath);
                    Prologue.PrologueStart(!expedition.WasRestored, true);
                    RunWithHiddenCursor(new ExpeditionConsole(expedition).Run);
                }
                return 0;
            }
            catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException
                                              or ArgumentException or SqliteException or InvalidOperationException)
            {
                Console.Error.WriteLine(exception.Message);
                return 1;
            }
        }

        private static void RunWithHiddenCursor(Action run)
        {
            Console.CursorVisible = false;
            try
            {
                run();
            }
            finally
            {
                Console.CursorVisible = true;
                Console.ResetColor();
            }
        }
    }
}
