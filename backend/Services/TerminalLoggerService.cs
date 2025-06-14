using System;
using System.IO;
using System.Text;

namespace SwAIvyn.Services
{    public interface ITerminalLoggerService : IDisposable
    {
        void StartLogging();
        void StopLogging();
        string GetLogFilePath();
    }

    public class TerminalLoggerService : ITerminalLoggerService, IDisposable
    {
        private readonly string _logFilePath;
        private StreamWriter? _logWriter;
        private TextWriter? _originalOut;
        private TextWriter? _originalError;
        private readonly object _lockObject = new object();        public TerminalLoggerService()
        {
            // Create logs directory if it doesn't exist
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            if (!Directory.Exists(logDirectory))
            {
                Directory.CreateDirectory(logDirectory);
            }

            // Create log file with timestamp
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = Path.Combine(logDirectory, $"terminal_{timestamp}.log");
            
            // Write initial header to the log file
            File.WriteAllText(_logFilePath, $"=== SwAIvyn Terminal Log Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}");
        }

        public void StartLogging()
        {
            lock (_lockObject)
            {
                if (_logWriter != null) return; // Already logging

                try
                {
                    _logWriter = new StreamWriter(_logFilePath, append: true, Encoding.UTF8)
                    {
                        AutoFlush = true
                    };

                    // Store original console streams
                    _originalOut = Console.Out;
                    _originalError = Console.Error;

                    // Create new tee writers that write to both console and file
                    var teeOut = new TeeTextWriter(_originalOut, _logWriter);
                    var teeError = new TeeTextWriter(_originalError, _logWriter);

                    // Redirect console output
                    Console.SetOut(teeOut);
                    Console.SetError(teeError);

                    // Write startup message
                    Console.WriteLine($"[TERMINAL LOG] Terminal logging started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"[TERMINAL LOG] Log file: {_logFilePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to start terminal logging: {ex.Message}");
                }
            }
        }

        public void StopLogging()
        {
            lock (_lockObject)
            {
                try
                {
                    if (_originalOut != null && _originalError != null)
                    {
                        Console.WriteLine($"[TERMINAL LOG] Terminal logging stopped at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        
                        // Restore original console streams
                        Console.SetOut(_originalOut);
                        Console.SetError(_originalError);
                    }

                    _logWriter?.Close();
                    _logWriter?.Dispose();
                    _logWriter = null;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Failed to stop terminal logging: {ex.Message}");
                }
            }
        }

        public string GetLogFilePath() => _logFilePath;

        public void Dispose()
        {
            StopLogging();
        }
    }

    /// <summary>
    /// A TextWriter that writes to two destinations simultaneously
    /// </summary>
    internal class TeeTextWriter : TextWriter
    {
        private readonly TextWriter _writer1;
        private readonly TextWriter _writer2;

        public TeeTextWriter(TextWriter writer1, TextWriter writer2)
        {
            _writer1 = writer1;
            _writer2 = writer2;
        }

        public override Encoding Encoding => _writer1.Encoding;

        public override void Write(char value)
        {
            _writer1.Write(value);
            _writer2.Write($"[{DateTime.Now:HH:mm:ss}] {value}");
        }

        public override void Write(string? value)
        {
            _writer1.Write(value);
            _writer2.WriteLine($"[{DateTime.Now:HH:mm:ss}] {value}");
        }

        public override void WriteLine(string? value)
        {
            _writer1.WriteLine(value);
            _writer2.WriteLine($"[{DateTime.Now:HH:mm:ss}] {value}");
        }

        public override void WriteLine()
        {
            _writer1.WriteLine();
            _writer2.WriteLine($"[{DateTime.Now:HH:mm:ss}]");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Don't dispose the original writers, just the log writer
                if (_writer2 != _writer1)
                {
                    _writer2?.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
