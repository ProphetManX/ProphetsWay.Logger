using System;

namespace ProphetsWay.Utilities
{

	/// <summary>
	/// A Base class for creating new ILoggingDestinations, it is thread safe and will check the level of a message before logging.
	/// </summary>
	public abstract class BaseLoggingDestination : ILoggingDestination
	{
		protected BaseLoggingDestination(LogLevels reportingLevel){
			_reportingLevel = reportingLevel;
		}

		protected readonly object LoggerLock = new object();
		private readonly LogLevels _reportingLevel;

		/// <summary>
		/// This is the required method from the ILoggingDestinations interface, this is the entry point for the destination from the Logger static class.
		/// </summary>
		public void Log(LogLevels level, string message = null, Exception ex = null)
		{
			//if the level of the message being passed is lower than the ReportingLevel set on the Destination, then return and don't log the message
			if((level & _reportingLevel) < level || level == LogLevels.NoLogging)
				return;

			MassageLogStatement(level, message, ex);
		}

		/// <summary>
		/// This is the method you must implement in your custom LoggingDestination.  
		/// It is where you will actually write the given message wherever you want.
		/// This is called from inside the Destination whenever there is a valid message to write to the log.
		/// </summary>
		/// <param name="masssagedMessage">The massaged statement text being logged.</param>
		/// <param name="level">The LogLevel of the message being passed.</param>
		/// <param name="rawMessage">The raw original message text that was logged.</param,>
		/// <param name="thrownException">The raw original exception that was logged.</param>
		protected abstract void WriteLogEntry(string masssagedMessage, LogLevels level, string rawMessage, Exception thrownException);

		/// <summary>
		/// This method is meant to massage the statement to be logged, before it goes to the WriteLogEntry method.
		/// It will extract the details from an exception and add them to the end of the message.
		/// It is Virtual and can be overridden if you wish to customize how you handle writing the raw Log method call.
		/// This method calls WriteLogEntry.
		/// </summary>
		protected virtual void MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
		{
			string exMessage, exStackTrace, massagedMessage = null;

			switch(level){
				case  LogLevels.Error:
					ExceptionDetailer(ex, out exMessage, out exStackTrace);
					massagedMessage = $"{message}{Environment.NewLine}{exMessage}{Environment.NewLine}{exStackTrace}";
					break;

				case LogLevels.Warning:
					if(ex != null){
						ExceptionDetailer(ex, out exMessage, out exStackTrace);
						massagedMessage = $"{message}{Environment.NewLine}{exMessage}{Environment.NewLine}{exStackTrace}";
					}
					break;

				default:
					massagedMessage = message;
					break;
			}

			WriteLogEntry(massagedMessage, level, message, ex);
		}

		/// <summary>
		/// Recursive function to peer deep into an Exception and its Inner Exceptions to capture the messages and stack traces within.
		/// </summary>
		private static void ExceptionDetailer(Exception ex, out string message, out string stack){
			var imessage = string.Empty;
			var istack = string.Empty;

			if(ex.InnerException != null)
				ExceptionDetailer(ex.InnerException, out imessage, out istack);
			
			message = string.IsNullOrEmpty(imessage)
				? ex.Message
				: string.Format("{0}{1}{1}Inner Exception Message:{1}{2}", ex.Message, Environment.NewLine, imessage);
			
			stack = string.IsNullOrEmpty(istack)
				? ex.StackTrace
				: string.Format("{0}{1}{1}Inner Exception Stack Trace:{1}{2}", ex.StackTrace, Environment.NewLine, istack);
		}


	}
}