namespace MeshQTT.Managers
{
    public static class Logger
    {
        public static void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(logMessage);
            try
            {
                File.AppendAllText("/App/logs/app.log", logMessage + "\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger Error] {ex.Message}");
            }
        }
    }
}
