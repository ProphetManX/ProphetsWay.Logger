using System;

namespace ProphetsWay.Utilities
{
	/// <summary>Optionally receives complete native context alongside ordinary raw content.</summary>
	/// <remarks>B01-B04: optional capability, not automatic capture, filtering or authority.
	/// Logger selects one contextual or legacy handoff on the ordinary route, never both.
	/// Calls are synchronous and may be concurrent; retaining recipients own capture before
	/// return and later completion/cleanup. No disposal, serialization or durability follows.
	/// Independent custom direct calls remain application-owned. Supplied bases additionally
	/// enforce their documented current-context direct-entry rule; context possession is not permission.</remarks>
	public interface IContextLoggingDestination : ILoggingDestination
	{
		/// <summary>Receives raw content and the completed label and scope capture.</summary>
		/// <param name="context">Required complete LogContext, including full scope properties.</param>
		/// <param name="level">Nonzero known-bit raw mask, 1 through 63.</param>
		/// <param name="message">Original optional text; null, empty and whitespace are valid.</param>
		/// <param name="ex">Original optional exception reference; null remains absent.</param>
		/// <exception cref="ArgumentNullException">context is null; ParamName is "context".</exception>
		/// <exception cref="ArgumentOutOfRangeException">level is invalid; ParamName is "level".</exception>
		/// <exception cref="Exception">Implementation-defined failure; independent custom errors are not automatically sanitized.</exception>
		/// <remarks>B02-B04: validate context/mask before payload work, without competing-error
		/// precedence. Preserve supplied facts/references, with no fabricated payload or deep clone.
		/// Each call is a new attempt, not deduplicated delivery; effects can precede failure.
		/// Full context is payload, never a reusable permission or scope-cleanup handle.</remarks>
		void LogWithContext(LogContext context, LogLevels level, string message = null, Exception ex = null);
	}
}