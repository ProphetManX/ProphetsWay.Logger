using System;

namespace ProphetsWay.Utilities.Generics
{
    public interface ILoggingDestination<T> : ILoggingDestination
    {
        void Log(LogLevels level, T metadata, string message = null, Exception ex = null);
    }
}
