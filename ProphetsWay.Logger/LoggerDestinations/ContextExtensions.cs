using ProphetsWay.Utilities.Generics;
using System;

namespace ProphetsWay.Utilities.LoggerDestinations
{
    public static class ContextExtensions
    {
        public static void Log(this ILoggerContext context, LogLevels level, string message = null, Exception ex = null)
        {
            Logger.Log(level, context, message, ex);
        }

        public static void Debug(this ILoggerContext context, string message = null)
        {
            Logger.Debug(message, context);
        }

        public static void Info(this ILoggerContext context, string message = null)
        {
            Logger.Info(message, context);
        }

        public static void Security(this ILoggerContext context, string message = null)
        {
            Logger.Security(message, context);
        }

        public static void Warn(this ILoggerContext context, string message = null, Exception ex = null)
        {
            Logger.Warn(message, context, ex);
        }

        public static void Error(this ILoggerContext context, Exception ex = null, string message = null)
        {
            Logger.Error(ex, context, message);
        }
    }
}
