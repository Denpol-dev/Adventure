using Adventure.Entities.Characters;
using Adventure.Entities.Levels;

namespace Adventure
{
    public class Game
    {
        public static void StartGame(Program program)
        {
            Console.Clear();

            var levels = new InitLevels();

            program.Level = levels.Level11;
            var level = program.Level;
            Console.BackgroundColor = level.LevelColor;
            program.Character = new Character(program);
            level.LoadMap(program.Character);

            var character = program.Character;
            character.SetCharacterOnMap();

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);

                program.Character.DoAction(key.Key.ToString(), program.Level.GetMap());
            }
            while (key.Key != ConsoleKey.Escape);
        }
    }
}
