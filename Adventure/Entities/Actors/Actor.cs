namespace Adventure.Entities.Actors
{
    public abstract class Actor
    {
        public abstract string Name { get; set; }
        public abstract bool IsTakeble { get; set; }
        public abstract void Action();
    }
}
