﻿using System;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable MemberCanBeProtected.Global

namespace ElmahCore.Mvc.Logger
{
    public class ElmahLogger : ILogger
    {
        private readonly IHttpContextAccessor _accessor;
        private Func<string, LogLevel, bool> _filter;


        internal ElmahLogger(string name, Func<string, LogLevel, bool> filter, IExternalScopeProvider scopeProvider, 
            IHttpContextAccessor accessor)
        {
            _accessor = accessor;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Filter = filter ?? ((category, logLevel) => true);
            ScopeProvider = scopeProvider;
        }

        public Func<string, LogLevel, bool> Filter
        {
            get => _filter;
            set => _filter = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string Name { get; }

        internal IExternalScopeProvider ScopeProvider { get; set; }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var message = formatter(state, exception);

            if (!string.IsNullOrEmpty(message) || exception != null)
            {
                WriteMessage(logLevel, Name, eventId.Id, message, exception);
            }
        }

        public virtual void WriteMessage(LogLevel logLevel, string logName, int eventId, string message,
            Exception exception)
        {
            var entry = new ElmahLogMessageEntry
            {
                TimeStamp = DateTime.Now,
                Message = message,
                //Scope = GetScopeInformation(),
                Exception = exception?.ToString(),
                Level = logLevel
            };
            _accessor?.HttpContext?.Features.Get<ElmahLogFeature>()?.AddMessage(entry);

        }

        public bool IsEnabled(LogLevel logLevel) 
            => _accessor?.HttpContext?.Features.Get<ElmahLogFeature>() != null && logLevel != LogLevel.None && Filter(Name, logLevel);

        public IDisposable BeginScope<TState>(TState state) => ScopeProvider?.Push(state) ?? NullScope.Instance;

        // ReSharper disable once UnusedMember.Local
        private string GetScopeInformation()
        {
            var stringBuilder = new StringBuilder();
            var scopeProvider = ScopeProvider;
            if (scopeProvider != null)
            {
                var initialLength = stringBuilder.Length;

                scopeProvider.ForEachScope((scope, state) =>
                {
                    var (builder, length) = state;
                    var first = length == builder.Length;
                    builder.Append(first ? "" : " => ").Append(scope);
                }, (stringBuilder, initialLength));

                if (stringBuilder.Length > initialLength)
                {
                    stringBuilder.Insert(initialLength, ' ');
                    stringBuilder.AppendLine();
                }
            }
            return stringBuilder.ToString();
        }
    }
    public class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();

        private NullScope()
        {
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}