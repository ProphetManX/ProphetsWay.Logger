using System;

namespace ProphetsWay.Utilities{
    public interface ILoggingDestination
    {
        void Log(LogLevels level, string message = null, Exception ex = null);
    }
}