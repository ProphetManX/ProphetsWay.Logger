using System;

namespace ProphetsWay.Logger{
    public interface ILoggingDestination
    {
        void Log(LogLevels level, string message = null, Exception ex = null);
    }
}