using System;

namespace Greyrose
{
    static class ServerLog
    {
        public static event Action<string> OnLine;

        public static void WriteLine(string message)
        {
            Console.WriteLine(message);
            OnLine?.Invoke(message);
        }

        public static void WriteLine(string format, params object[] args)
        {
            WriteLine(string.Format(format, args));
        }

        public static void Write(string message)
        {
            Console.Write(message);
            OnLine?.Invoke(message);
        }

        public static void ColorTitle(string input)
        {
            Console.ForegroundColor = ConsoleColor.DarkBlue;
            Console.WriteLine(input);
            Console.ResetColor();
            OnLine?.Invoke(input);
        }
    }
}
