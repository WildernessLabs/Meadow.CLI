﻿  // Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Meadow.CLI.Core.Logging
{
    /// <summary>
    /// Minimalistic logger that does nothing.
    /// </summary>
    public class NullLogger : ILogger
    {
        public static NullLogger Instance { get; } = new NullLogger();

        private NullLogger()
        {
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return null;// ToDo NullScope.Instance;
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        /// <inheritdoc />
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
        }
    }

    /// <summary>
    /// Minimalistic logger that does nothing.
    /// </summary>
    public class NullLogger<T> : ILogger<T>
    {
        public static readonly NullLogger<T> Instance = new NullLogger<T>();

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return NullDisposable.Instance;
        }

        /// <inheritdoc />
        /// <remarks>
        /// This method ignores the parameters and does nothing.
        /// </remarks>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return false;
        }

        private class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new NullDisposable();

            public void Dispose()
            {
                // intentionally does nothing
            }
        }
    }
}