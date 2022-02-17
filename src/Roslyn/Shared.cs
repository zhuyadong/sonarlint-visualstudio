using SonarLint.VisualStudio.Roslyn.Suppressions.Interfaces;
using SonarLint.VisualStudio.Roslyn.Suppressions.Logging;

namespace SonarLint.VisualStudio.Roslyn.Suppressions
{
    internal static class Shared
    {
        static Shared()
        {

            System.Diagnostics.Debugger.Launch();

            SuppressionChecker = new NoOpSuppressionChecker();
            Logger = new FileLogger(RootDirectory);
        }

        public static string RootDirectory = @"D:\proto\SLVS\CSharpSuppression\SLVS";

        public static string SuppressableDiagnosticsFileName = "SuppressableDiagnostics.txt";

        public static ISuppressionChecker SuppressionChecker { get; set; }

        public static ILogger Logger { get; set; }
    }

    internal static class LoggerExtensions
    {
        public static void Log(this object source, string message)
        {
            var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            var fullMessage = $"[{source.GetType().Name}] [Thread : {threadId}]  {message}";
            Shared.Logger.Write(fullMessage);
        }
    }
}
