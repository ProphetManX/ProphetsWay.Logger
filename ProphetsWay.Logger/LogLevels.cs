using System;

namespace ProphetsWay.Utilities
{
    [Flags]
    public enum LogLevels{
        //The original levels are meant to be used and allow for fall-thru of log messages
        /// <summary>
        /// Only 'Error' level messages will log to destinations with this setting.
        /// </summary>
        Error           = 0b000001,     //1

        /// <summary>
        /// 'Warning' and 'Error' level messages will log to destinations with this setting.
        /// </summary>
        Warning         = 0b000011,     //3,

        /// <summary>
        /// 'Security', 'Warning', and 'Error' level messages will log to destinations with this setting.
        /// </summary>
        Security        = 0b000111,     //7,

        /// <summary>
        /// 'Information', 'Security', 'Warning', and 'Error' level messages will log to destinations with this setting.
        /// </summary>
        Information     = 0b001111,     //15,

        /// <summary>
        /// All message levels will log to destinations with this setting.
        /// </summary>
        Debug           = 0b011111,     //31

        /// <summary>
        /// Only 'Debug' level messages will log to destinations with this setting.
        /// </summary>
        DebugOnly       = 0b010000,     //16

        /// <summary>
        /// Only 'Information' level messages will log to destinations with this setting.
        /// </summary>
        InformationOnly = 0b001000,     //8

        /// <summary>
        /// Only 'Security' level messages will log to destinations with this setting.
        /// </summary>
        SecurityOnly    = 0b000100,     //4

        /// <summary>
        /// Only 'Warning' level messages will log to destinations with this setting.
        /// </summary>
        WarningOnly     = 0b000010      //2
    }
}