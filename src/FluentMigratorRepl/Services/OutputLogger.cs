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
        var now = DateTime.Now.ToString("HH:mm:ss");
        var prefix = $"<span class=\"text-muted\">[{now}]</span> ";
        
        switch (logLevel)
        {
            case LogLevel.Trace:
            case LogLevel.Debug:
                _stringBuilder.AppendLine($"{prefix}<span class=\"text-muted\">{formatter(state, exception)}</span>");
                break;
            case LogLevel.Information:
                _stringBuilder.AppendLine($"{prefix}{formatter(state, exception)}");
                break;
            case LogLevel.Warning:
                _stringBuilder.AppendLine($"{prefix}<span class=\"text-warning\">{formatter(state, exception)}</span>");
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                _stringBuilder.AppendLine($"{prefix}<span class=\"text-error\">{formatter(state, exception)}</span>");
                if (exception != null)
                    _stringBuilder.AppendLine(exception.ToString());
                break;
            case LogLevel.None:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
        }
    }
    
    public static string GetOutput()
    {
        var output = _stringBuilder.ToString();
        _stringBuilder.Clear();
        
        return output;
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