namespace SonarLint.VisualStudio.Roslyn.Suppressions.Logging
{
    public interface ILogger
    {
        void Write(string message);
    }

    public sealed class NoOpLogger : ILogger
    {
        public void Write(string message) { /* no-op */ }
    }
}
