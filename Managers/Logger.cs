using MeshQTT.Entities;

namespace MeshQTT.Managers
{
    public static class Logger
    {
        private static readonly object _lock = new object();
        private static LogRotationConfig? _logRotationConfig;
        private static string? _logDir;
        private static string? _logPath;

        /// <summary>
        /// Initialize the logger with configuration.
        /// </summary>
        public static void Initialize(Config? config)
        {
            _logRotationConfig = config?.LogRotation ?? new LogRotationConfig();
            _logDir = Path.Combine(AppContext.BaseDirectory, "logs");
            _logPath = Path.Combine(_logDir, "app.log");
            Directory.CreateDirectory(_logDir);
        }

        public static void Log(string message)
        {
            var logMessage = $"[{DateTime.Now:HH:mm:ss}] {message}";
            Console.WriteLine(logMessage);
            try
            {
                // Initialize with defaults if not already initialized
                if (_logRotationConfig == null)
                {
                    Initialize(null);
                }

                var logDir = _logDir ?? Path.Combine(AppContext.BaseDirectory, "logs");
                Directory.CreateDirectory(logDir);
                var logPath = _logPath ?? Path.Combine(logDir, "app.log");

                lock (_lock)
                {
                    // Check if log rotation is needed
                    if (_logRotationConfig!.Enabled && File.Exists(logPath))
                    {
                        RotateLogIfNeeded(logPath);
                    }

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

        private static void RotateLogIfNeeded(string logPath)
        {
            if (_logRotationConfig == null || !_logRotationConfig.Enabled)
                return;

            var fileInfo = new FileInfo(logPath);
            bool shouldRotate = false;

            // Check file size
            if (_logRotationConfig.MaxFileSizeMB > 0)
            {
                var maxSizeBytes = _logRotationConfig.MaxFileSizeMB * 1024 * 1024;
                if (fileInfo.Length > maxSizeBytes)
                {
                    shouldRotate = true;
                }
            }

            // Check file age
            if (_logRotationConfig.MaxAgeDays > 0)
            {
                var maxAge = TimeSpan.FromDays(_logRotationConfig.MaxAgeDays);
                if (DateTime.Now - fileInfo.LastWriteTime > maxAge)
                {
                    shouldRotate = true;
                }
            }

            if (shouldRotate)
            {
                RotateLog(logPath);
            }
        }

        private static void RotateLog(string logPath)
        {
            try
            {
                var logDir = Path.GetDirectoryName(logPath)!;
                var logName = Path.GetFileNameWithoutExtension(logPath);
                var logExt = Path.GetExtension(logPath);
                
                // Create rotated filename with timestamp including milliseconds
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmssfff");
                var rotatedPath = Path.Combine(logDir, $"{logName}_{timestamp}{logExt}");

                // Ensure unique filename
                int counter = 1;
                while (File.Exists(rotatedPath))
                {
                    rotatedPath = Path.Combine(logDir, $"{logName}_{timestamp}_{counter:D3}{logExt}");
                    counter++;
                }

                // Move current log to rotated name
                File.Move(logPath, rotatedPath);

                // Clean up old log files
                CleanupOldLogs(logDir, logName, logExt);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger Error] Failed to rotate log: {ex.Message}");
            }
        }

        private static void CleanupOldLogs(string logDir, string logName, string logExt)
        {
            if (_logRotationConfig == null || _logRotationConfig.MaxFiles <= 0)
                return;

            try
            {
                // Find all rotated log files (excluding the current active log)
                var pattern = $"{logName}_*{logExt}";
                var logFiles = Directory.GetFiles(logDir, pattern)
                    .Select(f => new FileInfo(f))
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                // Keep MaxFiles-1 rotated files (since we also have the current active log)
                var maxRotatedFiles = Math.Max(1, _logRotationConfig.MaxFiles - 1);

                // Remove files beyond the max count
                for (int i = maxRotatedFiles; i < logFiles.Count; i++)
                {
                    logFiles[i].Delete();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Logger Error] Failed to cleanup old logs: {ex.Message}");
            }
        }
    }
}
