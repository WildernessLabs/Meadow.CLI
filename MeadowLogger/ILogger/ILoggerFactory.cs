using System;

namespace Meadow.CLI.Core.Logging
{
    public interface ILoggerFactory
    {
        //stub for download manager from extension class 
        public ILogger<T> CreateLogger<T>();

        /// <summary>
        /// Represents a type used to configure the logging system and create instances of <see cref="ILogger"/> from
        /// the registered <see cref="ILoggerProvider"/>s.
        /// </summary>
        public interface ILoggerFactory : IDisposable
        {
            /// <summary>
            /// Creates a new <see cref="ILogger"/> instance.
            /// </summary>
            /// <param name="categoryName">The category name for messages produced by the logger.</param>
            /// <returns>The <see cref="ILogger"/>.</returns>
            ILogger CreateLogger(string categoryName);

            /// <summary>
            /// Adds an <see cref="ILoggerProvider"/> to the logging system.
            /// </summary>
            /// <param name="provider">The <see cref="ILoggerProvider"/>.</param>
            //void AddProvider(ILoggerProvider provider);
        }
    }
}