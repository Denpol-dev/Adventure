namespace Adventure.Entities.Items
{
    public class HardDriveXD23 : Item
    {
        public override string Name { get; set; } = "Жесткий диск XD23";
        public override int Weight { get; set; } = 1;

        public override void Action()
        {
            Console.SetCursorPosition(0, 2);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            string message = "Сообщение: ";
            Console.Write(message + "Данные выгружены");
        }
    }
}
