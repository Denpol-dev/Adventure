namespace Adventure.Entities.Items
{
    public abstract class Item
    {
        public abstract string Name { get; set; }
        public abstract int Weight { get; set; }
        public abstract void Action();
    }
}
