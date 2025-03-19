using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Terminal : Actor
    {
        public Terminal(string terminal)
        {
            switch (terminal)
            {
                case "1":
                    Message = "Не волнуйся, я скоро буду дома, дорогая. Нам не рассказывают, что произошло, но мы вывозим отсюда практически все, что под руку попадается. Через 40 минут погрузка, а там каких-то два года и я в родной системе.";
                    Name = "Терминал 2547";
                    break;
                case "◬":
                    Message = "Данные загружены на диск.";
                    Name = "Инопланетный терминал";
                    Inventory = new HardDriveXD23Full();
                    LoadItem = new HardDriveXD23();
                    IsAlien = true;
                    break;
                case "◮":
                    Message = "Данные загружены.";
                    Name = "Терминал межпланетарной связи";
                    LoadItem = new HardDriveXD23Full();
                    break;
                default:
                    Message = "...";
                    Name = "...";
                    break;
            }
        }

        public override string Name { get; set; }
        public override string Icon { get; set; } = "░";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Magenta;
        public override Item? Inventory { get; set; } = null;
        public Item? LoadItem { get; set; } = null;
        public string Message { get; set; }
        public bool IsAlien { get; set; } = false;

        public override void Action()
        {
            if (LoadItem is HardDriveXD23Full)
            {
                Console.Write("Загрузка данных на орбиту... ---------->");
                Thread.Sleep(300);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... =--------->");
                Thread.Sleep(200);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... ==-------->");
                Thread.Sleep(500);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... ===------->");
                Thread.Sleep(600);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... ====------>");
                Thread.Sleep(300);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... =====----->");
                Thread.Sleep(100);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... ======---->");
                Thread.Sleep(200);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... =======--->");
                Thread.Sleep(300);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... ========-->");
                Thread.Sleep(300);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... =========->");
                Thread.Sleep(1500);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Загрузка данных на орбиту... ==========>");
                Thread.Sleep(3000);
                Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                Console.Write("Данные успешно загружены. ");
            }
            else
            {
                if (IsAlien)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.SetCursorPosition(0, 2);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("▄▄▄▄ █▌▐ ▄▌▄ ▄▀▄ ▄▀▀ ▀▄▀▄ █▌█ ▄▄▀ ▄▌▄");
                    Thread.Sleep(500);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");


                    Console.Write("▄▄▄▀ ▄▀▀ ▐▌▄ ▄▄▀ █▄█ ▖▗▖ ▄▄▀ █▄█ ▄▌▄▌ ▐▌▄▀");
                    Thread.Sleep(500);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");


                    Console.Write("▄▌▄ ▄▄▄▄ ▄▀▄▀▄ ▐▌▄ ▀▄▀ ▄▄▀ ▖▗▖ ▄▀▄▌");
                    Thread.Sleep(500);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");


                    Console.Write("▖▗▘▝ ▄▀▄ ▄▌▄ ▄▌▄ ▄▌▄▌ ▄▀▄▀▄▀");
                    Thread.Sleep(500);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");


                    Console.Write("▄▄▄ ▄▌▄▌ ▄▄▄▀ ▄▄▄▄ ▖▗▖ ▄▌▄ ▄▀▄▄ ▄▄▄▌");
                    Thread.Sleep(500);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");


                    Console.Write("▄▀▄▀ ▄▀▄ ▀▀▀ ▄▀▀ ▀▄▀▄ ▄▀▄▀ ▄▀▐ ▀▄▀▄");
                    Thread.Sleep(1000);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.ForegroundColor = ConsoleColor.White;

                    Console.Write("Попытка перевода...");
                    Thread.Sleep(2000);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Перевод выполнен успешно.");
                    Thread.Sleep(1000);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Перевод: Обнаружен приемлемый носитель данных. Выполняю загрузку.");
                    Thread.Sleep(5000);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ---------->");
                    Thread.Sleep(300);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =--------->");
                    Thread.Sleep(200);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ==-------->");
                    Thread.Sleep(500);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ===------->");
                    Thread.Sleep(600);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ====------>");
                    Thread.Sleep(300);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =====----->");
                    Thread.Sleep(100);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ======---->");
                    Thread.Sleep(200);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =======--->");
                    Thread.Sleep(300);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ========-->");
                    Thread.Sleep(300);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =========->");
                    Thread.Sleep(1500);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ==========>");
                    Thread.Sleep(300);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");

                    Console.Write("Сообщение: Данные проанализированы. Опасность не обнаружена.");
                }
                else
                {
                    Console.SetCursorPosition(0, 2);
                    Console.Write("\r" + new string(' ', Console.BufferWidth) + "\r");
                    string message = Name + ": ";
                    Console.Write(message + Message);
                }
            }
        }
    }
}
