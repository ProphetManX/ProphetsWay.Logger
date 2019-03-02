using System;

namespace ProphetsWay.Logger{

	public abstract class BaseLoggingDestination : ILoggingDestination
	{
		public BaseLoggingDestination(LogLevels reportingLevel){
			_reportingLevel = reportingLevel;
		}

		protected readonly object LoggerLock = new object();
		private readonly LogLevels _reportingLevel;


		public void Log(LogLevels level, string message = null, Exception ex = null)
		{
			if((level & _reportingLevel) < level)
				return;

			MassageLogStatement(level, message, ex);
		}

		protected abstract void WriteLogEntry(string message, LogLevels level);

		protected virtual void MassageLogStatement(LogLevels level, string message = null, Exception ex = null){
			string exMessage, exStackTrace;

			switch(level){
				case  LogLevels.Error:
					ExceptionDetailer(ex, out exMessage, out exStackTrace);
					message = $"{message}{Environment.NewLine}{exMessage}{Environment.NewLine}{exStackTrace}";
					break;

				case LogLevels.Warning:
					if(ex != null){
						ExceptionDetailer(ex, out exMessage, out exStackTrace);
						message = $"{message}{Environment.NewLine}{exMessage}{Environment.NewLine}{exStackTrace}";
					}
					break;
			}

			WriteLogEntry(message, level);
		}

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