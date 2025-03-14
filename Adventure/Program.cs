﻿using Adventure.Entities.Characters;
using Adventure.Entities.Levels;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace Adventure
{
    public class Program
    {
        #region Import dll

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(int handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleOutputCP(uint wCodePageID);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleCP(uint wCodePageID);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetConsoleDisplayMode(IntPtr ConsoleOutput, uint Flags, out COORD NewScreenBufferDimensions);
        [StructLayout(LayoutKind.Sequential)]
        public struct COORD
        {
            public short X;
            public short Y;

            public COORD(short X, short Y)
            {
                this.X = X;
                this.Y = Y;
            }
        }

        #endregion

        public Level Level { get; set; }
        public Character Character { get; set; }

        public void ChangeLevel(Level level, Character character)
        {
            Level = level;
            Console.Clear();
            Console.BackgroundColor = level.LevelColor;
            level.LoadMap();
            int x = character.X;
            int y = character.Y;
            character.SetCharacterOnMap(x, y);
        }

        public static void Main()
        {
            IntPtr hConsole = GetStdHandle(-11);
            SetConsoleDisplayMode(hConsole, 1, out COORD b1);
            var program = new Program();
            SetConsoleOutputCP(932);
            SetConsoleCP(932);
            Console.OutputEncoding = Encoding.Unicode;

            Prologue();
            Console.CursorVisible = false;
            StartGame(program);
        }


        public static void Prologue()
        {
            string FoxNetLogo = "███████╗ ██████╗ ██╗  ██╗███╗   ██╗███████╗████████╗\r\n██╔════╝██╔═══██╗╚██╗██╔╝████╗  ██║██╔════╝╚══██╔══╝\r\n█████╗  ██║   ██║ ╚███╔╝ ██╔██╗ ██║█████╗     ██║   \r\n██╔══╝  ██║   ██║ ██╔██╗ ██║╚██╗██║██╔══╝     ██║   \r\n██║     ╚██████╔╝██╔╝ ██╗██║ ╚████║███████╗   ██║   \r\n╚═╝      ╚═════╝ ╚═╝  ╚═╝╚═╝  ╚═══╝╚══════╝   ╚═╝   ";
            string FoxNetJapLogo = "           .       .                   .       .      .     .      .\r\n          .    .         .    .            .     ______\r\n      .           .             .               ////////\r\n                .    .   ________   .  .      /////////     .    .\r\n           .            |.____.  /\\        ./////////    .\r\n    .                 .//      \\/  |\\     /////////\r\n       .       .    .//          \\ |  \\ /////////       .     .   .\r\n                    ||フォックスネット|///////// .     .\r\n     .    .         ||           | |//`,/////                .\r\n             .       \\\\        ./ //  /  \\/   .\r\n  .                    \\\\.___./ //\\` '   ,_\\     .     .\r\n          .           .     \\ //////\\ , /   \\                 .    .\r\n                       .    ///////// \\|  '  |    .\r\n      .        .          ///////// .   \\ _ /          .\r\n                        /////////                              .\r\n                 .   ./////////     .     .\r\n         .           --------   .                  ..             .\r\n  .               .        .         .                       .\r\n                        ________________________\r\n____________------------                        -------------_________";

            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(FoxNetJapLogo);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("フォックスネットワーク社の長距離通信システムへようこそ!");
            Loading("言語パックを読み込んでいます", true, 100);
            Loading("ネイティブスピーカーの言語を判定します", true, 100);
            Loading("母国語への翻訳が進行中です", true, 100);

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine(FoxNetLogo);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Произведено переключение на язык носителя.");
            Console.WriteLine("Вас приветствует Система Дальней Связи компании FoxNet.");
            Console.WriteLine("Вы желаете подключиться к последнему выбранному планетарному объекту?");
            Console.WriteLine("y/n: ");
            var key = Console.ReadKey();
            if (key.Key.ToString() == "Y")
            {
                Console.Write(new string(' ', Console.WindowWidth));
                Loading("Активация узла дальней связи", true);
                Loading("Подключение к спутнику N910VT", true);
                Loading("Проверка состояния", false);
                Console.WriteLine("Выявлены многочисленные ошибки. Общее состояние спутника оценивается в 63%. Начинается проверка всех систем.");

                Loading("   Проверка блока питания и солнечных панелей", true);
                Loading("   Проверка маневровых двигателей", false);
                Loading("   Проверка передающего устройства", true);
                Loading("   Проверка систем управления планетарным объектом", true);
                Loading("   Проверка систем слежения", false);

                Loading("   Проверка оптических систем", false);
                Console.WriteLine("      Выявлены ошибки в центральном узле оптической системе спутника...");

                Loading("      Попытка исправления ошибок", false);

                Console.WriteLine("   Переход на резервные системы.");
                Console.WriteLine();

                Console.WriteLine("   Проверка тепловизионных систем");
                Loading("      Проверка центрального узла", true);
                Loading("      Проверка тепловизора", false);
                Console.WriteLine();

                Console.WriteLine("   Проверка радиолокационных систем");
                Loading("      Проверка центрального узла", true);
                Loading("      Проверка передатчика", true);
                Loading("      Проверка антенны", false);
                Console.WriteLine();

                Console.WriteLine("   Проверка систем лазерного дальномера");
                Loading("      Проверка центрального узла", true);
                Loading("      Проверка импульсного лазера", true);
                Loading("      Проверка детектора излучения", true);
                Loading("      Тестовый запуск", true);
                Loading("      Попытка переориентации лазерного дальномера на задачи слежения", true);

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ВНИМАНИЕ! WARNING! 注意!");
                Console.WriteLine("Системы, поддерживающие визуальный контроль за объектом неисправны. \r\nПопытка переориентации лазерного дальномера на задачи слежения была УСПЕШНА. \r\nПри переходе на использование лазерного дальномера в качестве системы слежения изображение будет выводиться в терминал в виде альтернативного изображения. \r\nВы подтверждаете данный переход?");
                Console.WriteLine("y/n: ");
                key = Console.ReadKey();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                if (key.Key.ToString() == "Y")
                {
                    Loading("Переход на лазерный дальномер", true);
                    Console.WriteLine();
                    Console.WriteLine("Подключение выполнено!");
                    Thread.Sleep(50);
                    Console.WriteLine("Установлено альтернативное управление планетарным объектом");
                    Console.WriteLine("Команды для управления: ");
                    Thread.Sleep(50);
                    Console.WriteLine("W: Вверх по Y");
                    Thread.Sleep(50);
                    Console.WriteLine("S: Вниз по Y");
                    Thread.Sleep(50);
                    Console.WriteLine("A: Влево по X");
                    Thread.Sleep(50);
                    Console.WriteLine("D: Направо по X");
                    Thread.Sleep(50);
                    Console.WriteLine("E: Выполнить действие");
                    Console.WriteLine();
                    Console.WriteLine("Продолжить?");
                    Console.WriteLine("y/n: ");
                    key = Console.ReadKey();
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.White;
                    if (key.Key.ToString() == "Y")
                    {
                        Loading("Загрузка альтернативного изображения", true);
                        Console.Clear();
                    }
                    else
                    {
                        Console.WriteLine("До свидания!");
                        Process.GetCurrentProcess().Kill();
                    }
                }
                else
                {
                    Console.WriteLine("До свидания!");
                    Process.GetCurrentProcess().Kill();
                }
            }
            else
            {
                Console.WriteLine("До свидания!");
                Process.GetCurrentProcess().Kill();
            }
        }

        public static void Loading(string message, bool isSuccess, int sleep = 400)
        {
            var random = new Random();
            for (int x = 0; x < random.Next(1, 4); x++)
            {
                for (int y = 0; y < 4; y++)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(message + new string('.', y));
                    Thread.Sleep(sleep);
                }
            }
            if (isSuccess)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(" OK");
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(" ERROR");
                Console.ForegroundColor = ConsoleColor.White;
            }
            Console.SetCursorPosition(0, Console.CursorTop);
        }

        public static void StartGame(Program program)
        {
            Console.Clear();

            var levels = new InitLevels();

            program.Level = levels.Level11;
            var level = program.Level;
            Console.BackgroundColor = level.LevelColor;
            level.LoadMap();

            program.Character = new Character(program);
            var character = program.Character;
            character.SetCharacterOnMap();

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);

                program.Character.DoAction(key.Key.ToString(), program.Level.GetMap());
            }
            while (key.Key != ConsoleKey.Escape);
        }
    }
}