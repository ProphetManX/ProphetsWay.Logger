﻿using System;

namespace ProphetsWay.Utilities.Generics
{
	/// <summary>
	/// A Base class for creating new custom Generic ILoggingDestinations, all you need to implement is the Log method
	/// </summary>
	public abstract class BaseLoggingDestination<T> : LoggingDestinationCore, ILoggingDestination<T>
	{
		protected BaseLoggingDestination(LogLevels reportingLevel) : base(reportingLevel) { }
		protected BaseLoggingDestination(string strReportingLevel) : base(strReportingLevel) { }
		protected BaseLoggingDestination(int intReportingLevel) : base(intReportingLevel) { }

		/// <summary>
		/// This is the required method from the ILoggingDestinations interface, this is the entry point for the destination from the Logger static class.
		/// </summary>
		public abstract void Log(LogLevels level, T metadata, string message = null, Exception ex = null);
	}
}
