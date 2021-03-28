using System;
using System.Collections.Concurrent;
using CliFx.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Meadow.CommandLine
{
    public class CliFxConsoleLogger : ILogger
    {
        private readonly CliFxConsoleLoggerProviderConfig _config;
        private readonly string _name;
        private readonly IConsole _console;
        
        public CliFxConsoleLogger(
            IConsole console,
            string name,
            CliFxConsoleLoggerProviderConfig config) =>
            (_console, _name, _config) = (console, name, config);


        public void Log<TState>(LogLevel logLevel,
                                EventId eventId,
                                TState state,
                                Exception exception,
                                Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            _console.Output.WriteLine(formatter(state, exception));
        }

        public bool IsEnabled(LogLevel logLevel) =>
            logLevel == _config.LogLevel;

        public IDisposable BeginScope<TState>(TState state) => default;
    }

    public sealed class CliFxConsoleLoggerProvider : ILoggerProvider
    {
        private readonly CliFxConsoleLoggerProviderConfig _config;
        private readonly IConsole _console;
        private readonly ConcurrentDictionary<string, CliFxConsoleLogger> _loggers =
            new ConcurrentDictionary<string, CliFxConsoleLogger>();

        public CliFxConsoleLoggerProvider(CliFxConsoleLoggerProviderConfig config, IConsole console) =>
            (_config, _console) = (config, console);

        public ILogger CreateLogger(string categoryName) =>
            _loggers.GetOrAdd(categoryName, name => new CliFxConsoleLogger(_console, name, _config));

        public void Dispose() => _loggers.Clear();
    }

    public class CliFxConsoleLoggerProviderConfig
    {
        public LogLevel LogLevel {get; init; }
    }
}
