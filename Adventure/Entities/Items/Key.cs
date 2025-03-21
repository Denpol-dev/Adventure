﻿namespace Adventure.Entities.Items
{
    public class Key : Item
    {
        public override string Name { get; set; } = "Ключ";
        public override int Weight { get; set; } = 1;
        public override bool IsPlotItem { get; set; } = false;
        public override bool IsUsed { get; set; } = false;

        public override void Action()
        {
            Console.SetCursorPosition(0, 2);
            Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
            string message = "Сообщение: ";
            Console.Write(message + "Дверь открыта");
        }
    }
}
