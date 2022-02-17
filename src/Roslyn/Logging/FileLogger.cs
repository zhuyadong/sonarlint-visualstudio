using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace SonarLint.VisualStudio.Roslyn.Suppressions.Logging
{
    public class FileLogger : IDisposable, ILogger
    {
        private bool disposedValue;

        private FileStream fileStream;
        private readonly Encoding encoding;

        private static int loggerInstanceCount;
        public FileLogger(string logRootPath)
        {
            encoding = new UTF8Encoding(false);

            var process = Process.GetCurrentProcess();
            var logFilePath = Path.Combine(logRootPath, $"{Path.GetFileNameWithoutExtension(process.MainModule.FileName)}_{process.Id}.log");

            Directory.CreateDirectory(logRootPath);
            GetFileStream(logFilePath);

            var currentCount = Interlocked.Increment(ref loggerInstanceCount);
            Write($"New logger instance created: count = {currentCount}");
        }

        private void GetFileStream(string logFilePath)
        {
            var deferredMessages = new List<string>();

            QueueDeferredMessage($"Trying to create file path: {logFilePath}");
            QueueDeferredMessage($"File exists: {File.Exists(logFilePath)}");

            try
            {
                QueueDeferredMessage("Trying to open file for read/write...");
                fileStream = File.Open(logFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
                WriteDeferredMessages();
            }
            catch (Exception ex)
            {
                QueueDeferredMessage($"Error creating file: {ex.Message}");
            }

            void QueueDeferredMessage(string message)
            {
                Debug.WriteLine(message);
                deferredMessages.Add(message);
            }

            void WriteDeferredMessages()
            {
                foreach (var message in deferredMessages)
                {
                    Write(message);
                }
            }
        }

        public void Write(string message)
        {
            System.Diagnostics.Debug.WriteLine(message);

            var bytes = encoding.GetBytes(message + Environment.NewLine);
            fileStream.Write(bytes, 0, bytes.Length);
            fileStream.Flush();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing && fileStream != null)
                {
                    fileStream.Flush();
                    fileStream.Dispose();
                    fileStream = null;
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Ne changez pas ce code. Placez le code de nettoyage dans la méthode 'Dispose(bool disposing)'
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
