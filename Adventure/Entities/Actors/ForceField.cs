using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class ForceField : Actor
    {
        public ForceField(string field)
        {
            Pass = field switch
            {
                "≈" => new ArtifactY66(),
                _ => new Key(),
            };
        }

        public override string Name { get; set; } = "Силовое поле";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = true;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Gray;
        public override Item? Inventory { get; set; } = null;
        public Item Pass { get; set; }

        public override void Action()
        {
            Console.SetCursorPosition(MessageX, MessageY);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            string message = "Сообщение: ";
            Console.WriteLine(message + "Хм, получилось пройти. И этот артефакт начинает немного вибрировать...");
        }
    }
}
