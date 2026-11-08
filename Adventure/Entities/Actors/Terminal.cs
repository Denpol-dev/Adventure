using Adventure.Entities.Items;

namespace Adventure.Entities.Actors
{
    public class Terminal : Actor
    {
        public override string Name { get; set; } = "...";
        public override string Icon { get; set; } = "░";
        public override bool IsTakeble { get; set; } = false;
        public override bool IsCollision { get; set; } = false;
        public override ConsoleColor Color { get; set; } = ConsoleColor.Magenta;
        public override Item? Inventory { get; set; } = null;
        public Item? LoadItem { get; set; } = null;
        public string Message { get; set; } = "...";
        public bool IsAlien { get; set; }
        public bool CompletesGame { get; set; }

        public override void Action()
        {
            if (CompletesGame)
            {
                ConsoleView.ClearLine(MessageY);
                Console.Write("Загрузка данных на орбиту... ---------->");
                Thread.Sleep(300);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... =--------->");
                Thread.Sleep(200);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... ==-------->");
                Thread.Sleep(500);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... ===------->");
                Thread.Sleep(600);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... ====------>");
                Thread.Sleep(300);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... =====----->");
                Thread.Sleep(100);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... ======---->");
                Thread.Sleep(200);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... =======--->");
                Thread.Sleep(300);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... ========-->");
                Thread.Sleep(300);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... =========->");
                Thread.Sleep(1500);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Загрузка данных на орбиту... ==========>");
                Thread.Sleep(3000);
                ConsoleView.ClearLine(MessageY);

                Console.Write("Данные успешно загружены. ");
            }
            else
            {
                if (IsAlien)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("▄▄▄▄ █▌▐ ▄▌▄ ▄▀▄ ▄▀▀ ▀▄▀▄ █▌█ ▄▄▀ ▄▌▄");
                    Thread.Sleep(500);
                    ConsoleView.ClearLine(MessageY);


                    Console.Write("▄▄▄▀ ▄▀▀ ▐▌▄ ▄▄▀ █▄█ ▖▗▖ ▄▄▀ █▄█ ▄▌▄▌ ▐▌▄▀");
                    Thread.Sleep(500);
                    ConsoleView.ClearLine(MessageY);


                    Console.Write("▄▌▄ ▄▄▄▄ ▄▀▄▀▄ ▐▌▄ ▀▄▀ ▄▄▀ ▖▗▖ ▄▀▄▌");
                    Thread.Sleep(500);
                    ConsoleView.ClearLine(MessageY);


                    Console.Write("▖▗▘▝ ▄▀▄ ▄▌▄ ▄▌▄ ▄▌▄▌ ▄▀▄▀▄▀");
                    Thread.Sleep(500);
                    ConsoleView.ClearLine(MessageY);


                    Console.Write("▄▄▄ ▄▌▄▌ ▄▄▄▀ ▄▄▄▄ ▖▗▖ ▄▌▄ ▄▀▄▄ ▄▄▄▌");
                    Thread.Sleep(500);
                    ConsoleView.ClearLine(MessageY);


                    Console.Write("▄▀▄▀ ▄▀▄ ▀▀▀ ▄▀▀ ▀▄▀▄ ▄▀▄▀ ▄▀▐ ▀▄▀▄");
                    Thread.Sleep(1000);
                    ConsoleView.ClearLine(MessageY);

                    Console.ForegroundColor = ConsoleColor.White;

                    Console.Write("Попытка перевода...");
                    Thread.Sleep(2000);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Перевод выполнен успешно.");
                    Thread.Sleep(1000);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Перевод: Обнаружен приемлемый носитель данных. Выполняю загрузку.");
                    Thread.Sleep(5000);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ---------->");
                    Thread.Sleep(300);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =--------->");
                    Thread.Sleep(200);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ==-------->");
                    Thread.Sleep(500);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ===------->");
                    Thread.Sleep(600);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ====------>");
                    Thread.Sleep(300);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =====----->");
                    Thread.Sleep(100);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ======---->");
                    Thread.Sleep(200);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =======--->");
                    Thread.Sleep(300);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ========-->");
                    Thread.Sleep(300);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. =========->");
                    Thread.Sleep(1500);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Внимание! На жесткий диск XD23 записаны данные. Анализ входящих данных. ==========>");
                    Thread.Sleep(300);
                    ConsoleView.ClearLine(MessageY);

                    Console.Write("Сообщение: Данные проанализированы. Опасность не обнаружена.");
                }
                else
                {
                    ConsoleView.ClearLine(MessageY);
                    string message = Name + ": ";
                    Console.Write(message + Message);
                }
            }
        }
    }
}
