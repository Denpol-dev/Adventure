namespace Adventure.Entities.Items
{
    public class Key : Item
    {
        public override string Name { get; set; } = "Ключ";
        public override int Weight { get; set; } = 1;

        public override void Action()
        {
            Console.Write("Дверь открыта");
        }
    }
}
