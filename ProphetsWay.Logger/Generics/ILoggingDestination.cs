using System;

namespace ProphetsWay.Utilities.Generics
{
    public interface ILoggingDestination<T> : IDestination
    {
        void Log(LogLevels level, T metadata, string message = null, Exception ex = null);
    }
}
