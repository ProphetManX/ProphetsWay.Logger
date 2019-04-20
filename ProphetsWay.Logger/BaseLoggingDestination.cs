using System;

namespace ProphetsWay.Utilities
{
	/// <summary>
	/// A Base class for creating new custom ILoggingDestinations, all you need to implement is the Log method
	/// </summary>
	public abstract class BaseLoggingDestination : LoggingDestinationCore, ILoggingDestination
	{
        protected BaseLoggingDestination(LogLevels reportingLevel) : base(reportingLevel) { }

        /// <summary>
        /// This is the required method from the ILoggingDestinations interface, this is the entry point for the destination from the Logger static class.
        /// </summary>
        public abstract void Log(LogLevels level, string message = null, Exception ex = null);
	}
}