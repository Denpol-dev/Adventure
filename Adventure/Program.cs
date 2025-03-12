using System;

public class Program
{
    public static void Main()
    {
        Prologue();
    }

    public static void Prologue()
    {
        string FoxNetLogo = "FFFFFFFFFFF    OOOOOOOOO    XXX       XXX  NNN     NNN  EEEEEEEEEEE  TTTTTTTTTTT\r\nFFFFFFFFFFF  OOOOOOOOOOOOO  XXX       XXX  NNNN    NNN  EEEEEEEEEEE  TTTTTTTTTTT\r\nFFF          OOO       OOO   XXX     XXX   NNN N   NNN  EE               TTT\r\nFFF          OOO       OOO    XXX   XXX    NNN NN  NNN  EE               TTT\r\nFFFFFFFFFFF  OOO       OOO       XXX       NNN NN  NNN  EEEEEEEEE        TTT\r\nFFFFFFFFFFF  OOO       OOO    XXX   XXX    NNN  NN NNN  EEEEEEEEE        TTT\r\nFFF          OOO       OOO   XXX     XXX   NNN  NN NNN  EE               TTT\r\nFFF          OOO       OOO  XXX       XXX  NNN   N NNN  EE               TTT\r\nFFF          OOOOOOOOOOOOO  XXX       XXX  NNN    NNNN  EEEEEEEEEEE      TTT\r\nFFF            OOOOOOOOO    XXX       XXX  NNN     NNN  EEEEEEEEEEE      TTT";

        Console.ForegroundColor = ConsoleColor.DarkYellow;
        Console.WriteLine(FoxNetLogo);
        Console.ForegroundColor = ConsoleColor.White;
        Console.WriteLine("Вас приветствует Система Дальней Связи компании FoxNet.");
        Console.WriteLine("Вы желаете подключиться к последнему выбранному планетарному объекту?");
        Console.WriteLine("y/n: ");
        var key = Console.ReadKey();
        if (key.Key.ToString() == "Y")
        {
            Console.Write(new string(' ', Console.WindowWidth));
            Loading("Активация узла дальней связи", true);
            Loading("Выполенено подключение к спутнику N910VT. Проверка состояния", false);
            Console.WriteLine("Выявлены многочисленные ошибки. Общее состояние спутника оценивается в 63%. Начинается проверка всех систем.");

            Loading("Проверка блока питания и солнечных панелей", true);
            Loading("Проверка маневровых двигателей", false);
            Loading("Проверка передающего устройства", true);
            Loading("Проверка систем управления планетарным объектом", true);
            Loading("Проверка систем слежения", false);

            Loading("Проверка оптических систем", false);
            Console.WriteLine("Выявлены ошибки в центральном узле оптической системе спутника...");

            Loading("Попытка исправления ошибок", false);

            Console.WriteLine("Переход на резервные системы.");
            Console.WriteLine();

            Console.WriteLine("Проверка тепловизионных систем");
            Loading("Проверка центрального узла", true);
            Loading("Проверка тепловизора", false);
            Console.WriteLine();

            Console.WriteLine("Проверка радиолокационных систем");
            Loading("Проверка центрального узла", true);
            Loading("Проверка передатчика", true);
            Loading("Проверка антенны", false);
            Console.WriteLine();

            Console.WriteLine("Проверка систем лазерного дальномера");
            Loading("Проверка центрального узла", true);
            Loading("Проверка импульсного лазера", true);
            Loading("Проверка детектора излучения", true);
            Loading("Тестовый запуск", true);
            Loading("Попытка переориентации лазерного дальномера на задачи слежения", true);
            Console.WriteLine("Переход на лазерный дальномер");
            Console.WriteLine();

            Console.WriteLine("Подключение выполнено");
            Console.WriteLine("Продолжить?");
            Console.WriteLine("y/n: ");
            key = Console.ReadKey();
            if(key.Key.ToString() == "Y")
            {
                Loading("Загрузка альтернативного изображения", true);
                Console.Clear();
            }
            else
            {
                Console.WriteLine("До свидания!");
            }
        }
        else
        {
            Console.WriteLine("До свидания!");
        }
    }

    public static void Loading(string message, bool isSuccess)
    {
        var random = new Random();
        for (int x = 0; x < random.Next(2, 4); x++)
        {
            for (int y = 0; y < 4; y++)
            {
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(new string(' ', Console.WindowWidth));
                Console.SetCursorPosition(0, Console.CursorTop);
                Console.Write(message + new string('.', y));
                Thread.Sleep(500);
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
}