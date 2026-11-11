using Microsoft.Extensions.Logging;

namespace MyDrawVector.Services;

public sealed class FileLoggerProvider : ILoggerProvider
{
    private const long MaxLogBytes = 1024 * 1024;

    private readonly object _gate = new();
    private readonly StreamWriter _writer;

    public FileLoggerProvider(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "app.log");
        RotateIfNeeded(path);
        _writer = new StreamWriter(path, append: true)
        {
            AutoFlush = true,
        };
    }

    private static void RotateIfNeeded(string path)
    {
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
            {
                File.Move(path, path + ".1", overwrite: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(this, categoryName);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }

    internal void Write(string line)
    {
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    private sealed class FileLogger(FileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= LogLevel.Information;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff} [{logLevel}] {categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                message += Environment.NewLine + exception;
            }

            provider.Write(message);
        }
    }
}
