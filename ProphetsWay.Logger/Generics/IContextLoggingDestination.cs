using System;

namespace ProphetsWay.Utilities.Generics
{
	/// <summary>Optionally receives full native context separately from exact-T metadata.</summary>
	/// <typeparam name="T">Unconstrained declared route type; runtime subtype never redirects it.</typeparam>
	/// <remarks>B01-B04: same synchronous, concurrent-call, retention and direct-call limits as
	/// the ordinary contextual capability. Select only this exact-T interface on this route;
	/// implementing both capabilities does not cause an ordinary or second legacy delivery.</remarks>
	public interface IContextLoggingDestination<T> : ILoggingDestination<T>
	{
		/// <summary>Receives original typed content with complete label and scope facts.</summary>
		/// <param name="context">Required complete LogContext, separate from metadata.</param>
		/// <param name="level">Nonzero known-bit raw mask, 1 through 63.</param>
		/// <param name="metadata">Original T value/reference, including null or default(T).</param>
		/// <param name="message">Original optional text; null, empty and whitespace are valid.</param>
		/// <param name="ex">Original optional exception reference; null remains absent.</param>
		/// <exception cref="ArgumentNullException">context is null; ParamName is "context".</exception>
		/// <exception cref="ArgumentOutOfRangeException">level is invalid; ParamName is "level".</exception>
		/// <exception cref="Exception">Implementation-defined failure; independent custom errors are not automatically sanitized.</exception>
		/// <remarks>B02-B04: validate before payload work with no competing-error precedence.
		/// Preserve supplied facts and original metadata; annotation-shaped T is still metadata.
		/// Transport inspects no arbitrary getters and performs no implicit object formatting,
		/// nested enumeration or deep clone. Each synchronous attempt may have effects before failure.</remarks>
		void LogWithContext(LogContext context, LogLevels level, T metadata, string message = null, Exception ex = null);
	}
}