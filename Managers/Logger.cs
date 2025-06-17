using System;

namespace MeshQTT.Managers
{
    public static class Logger
    {
        public static void Log(string message)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }
}
