using System;

namespace ProphetsWay.Utilities.Generics
{
    public static class MetadataExtensions
    {
        public static void Log<T>(this T metadata, LogLevels level, string message = null, Exception ex = null) where T : ILoggerMetadata
        {
            Logger.Log(level, metadata, message, ex);
        }

        public static void Debug<T>(this T metadata, string message = null) where T : ILoggerMetadata
        {
            Logger.Debug(message, metadata);
        }

        public static void Info<T>(this T metadata, string message = null) where T : ILoggerMetadata
        {
            Logger.Info(message, metadata);
        }

        public static void Security<T>(this T metadata, string message = null) where T : ILoggerMetadata
        {
            Logger.Security(message, metadata);
        }

        public static void Warn<T>(this T metadata, string message = null, Exception ex = null) where T : ILoggerMetadata
        {
            Logger.Warn(message, metadata, ex);
        }

        public static void Error<T>(this T metadata, Exception ex = null, string message = null) where T : ILoggerMetadata
        {
            Logger.Error(ex, metadata, message);
        }
    }
}
