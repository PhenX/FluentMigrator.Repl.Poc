using System.Text;

namespace FluentMigratorRepl.Services;

public class OutputLogger : ILogger, IDisposable
{
    private static StringBuilder _stringBuilder = new();
    
    public static OutputLogger Instance { get; } = new();

    private OutputLogger()
    {
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
            case LogLevel.Information:
                _stringBuilder.AppendLine(formatter(state, exception));
                break;
            case LogLevel.Warning:
                _stringBuilder.AppendLine($"WARNING: {formatter(state, exception)}");
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _stringBuilder.AppendLine($"ERROR: {formatter(state, exception)}");
                if (exception != null)
                    _stringBuilder.AppendLine(exception.ToString());
                break;
            case LogLevel.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
        }
    }
    
    public string GetOutput()
    {
        return _stringBuilder.ToString();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public void Dispose()
    {
        _stringBuilder.Clear();
        _stringBuilder = null!;
    }
}

public class OutputLoggerProvider : ILoggerProvider
{
    public readonly OutputLogger Logger = OutputLogger.Instance;
    
    public static OutputLoggerProvider Instance { get; } = new();

    private OutputLoggerProvider()
    {
    }
    
    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return Logger;
    }
    
}