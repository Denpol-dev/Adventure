using Adventure.Entities;
using Adventure.Entities.Characters;
using Adventure.Entities.Levels;

namespace Adventure
{
    public static class Game
    {
        public static void StartGame(InitLevels levels)
        {
            var character = new Character(levels);
            ConsoleView.DrawLevel(character);

            while (true)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Escape)
                    return;
                if (key == ConsoleKey.E)
                {
                    ConsoleView.ShowInteraction(character, character.Interact());
                    continue;
                }

                Vector? direction = key switch
                {
                    ConsoleKey.W => Vector.Top,
                    ConsoleKey.S => Vector.Bottom,
                    ConsoleKey.A => Vector.Left,
                    ConsoleKey.D => Vector.Right,
                    _ => null
                };
                if (direction is not Vector movement)
                    continue;

                int previousX = character.X;
                int previousY = character.Y;
                switch (character.Move(movement))
                {
                    case MoveResult.Moved:
                        ConsoleView.MoveCharacter(character, previousX, previousY);
                        break;
                    case MoveResult.ChangedLevel:
                        ConsoleView.DrawLevel(character);
                        break;
                    case MoveResult.NoNeighbor:
                        ConsoleView.ShowConnectionError();
                        break;
                }
            }
        }
    }
}
