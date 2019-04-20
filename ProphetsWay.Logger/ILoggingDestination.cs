using System;

namespace ProphetsWay.Utilities{
    public interface ILoggingDestination : IDestination
    {
        void Log(LogLevels level, string message = null, Exception ex = null);
    }
}