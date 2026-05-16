using System;
using System.IO;
using System.Text;

namespace MissionTime.Services
{
    public static class LogService
    {
        private static readonly string LogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MissionTime.log");
        private static readonly object LockObj = new object();

        public static void Log(string message, Exception ex = null)
        {
            try
            {
                lock (LockObj)
                {
                    // Ограничение размера (если файл > 10MB, обрезаем)
                    var fileInfo = new FileInfo(LogPath);
                    if (fileInfo.Exists && fileInfo.Length > 10 * 1024 * 1024)
                    {
                        File.WriteAllText(LogPath, "--- LOG CLEARED DUE TO SIZE ---" + Environment.NewLine);
                    }

                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
                    
                    if (ex != null)
                    {
                        sb.AppendLine("   ОШИБКА: " + ex.Message);
                        sb.AppendLine("   ИСТОЧНИК: " + ex.Source);
                        sb.AppendLine("   СТЕКТРЕЙС: " + ex.StackTrace);
                        if (ex.InnerException != null)
                        {
                            sb.AppendLine("   ВНУТРЕННЯЯ ОШИБКА: " + ex.InnerException.Message);
                            sb.AppendLine("   ВНУТР. СТЕКТРЕЙС: " + ex.InnerException.StackTrace);
                        }
                    }
                    sb.AppendLine(new string('-', 40));

                    File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Если даже лог не пишется (нет прав доступа), просто молчим, чтобы не ложить приложение
            }
        }
    }
}
