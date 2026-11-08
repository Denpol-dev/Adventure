using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class ForceField : Actor
    {
        public ForceField(Item pass)
        {
            Pass = pass;
        }

        public override string Name { get; set; } = "Силовое поле";
        public override string Icon { get; set; } = "≈";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = true;
        public override ConsoleColor Color { get; set; } = ConsoleColor.White;
        public override Item? Inventory { get; set; } = null;
        public Item Pass { get; set; }

        public override void Action()
        {
            ConsoleView.ClearLine(MessageY);
            string message = "Сообщение: ";
            Console.WriteLine(message + "Хм, получилось пройти. И этот артефакт начинает немного вибрировать...");
        }
    }
}
