using System;

namespace ProphetsWay.Logger
{
    [Flags]
    public enum LogLevels{
        NoLogging = 0,
        Error = 1,
        Warning = 3,
        Security = 7,
        Information = 15,
        Debug = 31
    }
}