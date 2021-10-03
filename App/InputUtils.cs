using System;

namespace App
{
    public static class InputUtils
    {
        public static string InputString(string message)
        {
            Console.WriteLine(message);
            return Console.ReadLine();
        }
        
        public static int InputInt(string message)
        {
            Console.WriteLine(message);
            return Int32.Parse(Console.ReadLine() ?? "0");
        }
    }
}