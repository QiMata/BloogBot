#if NETFRAMEWORK && INJECTION_SHIM && !NET8_0_OR_GREATER
using System;
using System.IO;
using System.Threading;

namespace ForegroundBotRunner
{
    internal static class InjectedBotHost
    {
        private static Thread _loop;
        private static volatile bool _stop;
        private static string LogPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory ?? Environment.CurrentDirectory, "BloogBotLogs", "injection.log");

        public static void Start()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"InjectedBotHost.Start {DateTime.Now:HH:mm:ss}\n");
                if (_loop != null) return;
                _loop = new Thread(MainLoop) { IsBackground = true, Name = "InjectedShimLoop" };
                _loop.Start();
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(LogPath, $"Start error: {ex}\n"); } catch { }
            }
        }

        private static void MainLoop()
        {
            int counter = 0;
            while (!_stop)
            {
                Thread.Sleep(10000);
                counter++;
                if (counter % 6 == 0)
                {
                    try { File.AppendAllText(LogPath, $"[{DateTime.Now:HH:mm:ss}] shim alive #{counter}\n"); } catch { }
                }
            }
        }

        public static void Stop()
        {
            _stop = true;
            try { File.AppendAllText(LogPath, $"InjectedBotHost.Stop {DateTime.Now:HH:mm:ss}\n"); } catch { }
        }
    }
}
#endif
