namespace Adventure.Entities.Items
{
    public class Key : Item
    {
        private string name;
        public override string Name
        {
            get => name;
            set => name = value;
        }

        private int weight;
        public override int Weight
        {
            get => weight;
            set => weight = value;
        }

        public override void Action()
        {
            Console.Write("Дверь открыта");
        }
    }
}
