namespace MeshQTT.Managers
{
    public static class Logger
    {
        private static readonly object _lock = new object();

        public static void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(logMessage);
            try
            {
                var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                var logPath = Path.Combine(logDir, "app.log");
                lock (_lock)
                {
                    using (
                        var stream = new FileStream(
                            logPath,
                            FileMode.Append,
                            FileAccess.Write,
                            FileShare.ReadWrite
                        )
                    )
                    using (var writer = new StreamWriter(stream))
                    {
                        writer.WriteLine(logMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger Error] {ex.Message}");
            }
        }
    }
}
