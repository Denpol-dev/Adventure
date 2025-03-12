namespace Adventure.Entities.Actors
{
    public class Chest : Actor
    {
        private string name = "Сундук";
        public override string Name
        {
            get => name;
            set => name = value;
        }

        private bool isTakeble = false;
        public override bool IsTakeble
        {
            get => isTakeble;
            set => isTakeble = value;
        }

        public override void Action()
        {
            Console.Write("Сундук открыт");
        }
    }
}
