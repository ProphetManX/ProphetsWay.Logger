using System;

namespace ProphetsWay.Utilities
{
	/// <summary>Provides guarded ordinary entrypoints and a permitted contextual delivery hook.</summary>
	/// <remarks>B06-B10/B18/B21: supplied public entrypoints are nonvirtual and perform all
	/// argument validation, complete current-context capture and configured severity/label checks
	/// before any overridable payload work. Raw Log captures active native frames with absent
	/// entry annotations. LogWithContext accepts its supplied scope/label facts only when its ordered
	/// frames correspond to the same successful scope openings currently active here, including
	/// empty/unlabeled frames. Both empty sequences match. Equal labels/properties from different
	/// openings do not establish correspondence. EntryAnnotations need not be absent or empty.
	/// Reusing a context while those openings remain current is allowed, but rechecks current
	/// destination configuration; no successful earlier delivery grants later permission.
	/// An added, removed or replaced active frame makes that context noncurrent: reject with
	/// ArgumentException naming context, before hooks or output, without changing either capture
	/// or ambient state. Do not merge, discard or reconstruct supplied frames, compare arbitrary
	/// property objects, infer creator/thread identity, or maintain completed-call history.
	/// The correspondence check concerns native frame membership, not authenticated ownership;
	/// normally inherited work with the same active frames can match. D024 handle rules stay separate.
	/// Capture the destination policy before callbacks. Require every message bit and the existing
	/// policy truth table against complete effective labels; no registration lookup applies to a
	/// direct call. A valid rejection returns without massage, hook, callback, export or fallback.
	/// Successful selection invokes LogCore once with original raw values and the complete context.
	/// A direct call is a one-recipient attempt whose sole captured position is 1, not a fabricated
	/// registration. Shared capture failure counts once and returns after safe reporting; an opted-in
	/// check failure withholds this recipient, reports LabelCheck and returns. Output/hook failure
	/// reports Output then throws LogDispatchException. Arguments/noncurrent context are local errors,
	/// not reports; fixed authored diagnostics contain no supplied values, causes or backlinks.
	/// These paths share Logger.DispatchFailed and its containment/recursive-report rules. Reporter
	/// failure cannot change the original result. No strict selector, retry or rescue is introduced.
	/// Logger's trusted captured handoff instead uses its already checked capture and policies;
	/// it neither recaptures ambient state nor repeats direct freshness/selection checks. Its output
	/// failure returns to Logger's accumulation boundary, without a second local notification.
	/// Calls are synchronous, not idempotent delivery, and may run concurrently without serializing
	/// custom work. Captures freeze membership, not nested objects; no registry lock spans hooks.
	/// Borrowed resources and retaining destinations keep their existing lifetime responsibilities.
	/// Hiding/reimplementing members or invoking hooks from consumer-written methods is arbitrary
	/// custom code outside these supplied entrypoints, not a promised sandbox.
	/// <para>F01-F04: every public direct invocation is a new call with one newly
	/// captured UTC event time. An accepted supplied context contributes its same
	/// immutable frames and label facts, including entry annotations, but the selected
	/// context carries the new time. The supplied context is unchanged. Time is not
	/// part of current-frame correspondence and cannot authorize direct delivery.
	/// Trusted LogCaptured delivery instead keeps its original call's complete
	/// context/time without a new sample or public direct-validation pass.</para></remarks>
	public abstract class BaseLoggingDestination : LoggingDestinationCore, ILoggingDestination, IContextLoggingDestination
	{
		protected BaseLoggingDestination(LogLevels reportingLevel) : base(reportingLevel) { }
		protected BaseLoggingDestination(string strReportingLevel) : base(strReportingLevel) { }
		protected BaseLoggingDestination(int intReportingLevel) : base(intReportingLevel) { }

		/// <summary>Attempts an ordinary direct entry using current native scopes.</summary>
		/// <param name="level">Raw mask 1 through 63.</param>
		/// <param name="message">Original optional text, including null, empty or whitespace.</param>
		/// <param name="ex">Original optional exception; null is valid.</param>
		/// <exception cref="ArgumentOutOfRangeException">level is invalid; ParamName is "level".</exception>
		/// <exception cref="LogDispatchException">Selected direct output failed, after safe reporting.</exception>
		/// <remarks>B06/B08/B10: follows the common supplied-base guard, capture and result rules.</remarks>
		public void Log(LogLevels level, string message = null, Exception ex = null)
		{
			Logger.DispatchDirect(this, null, level, context => LogCore(context, level, message, ex));
		}

		/// <summary>Attempts ordinary direct delivery of a context matching current native frames.</summary>
		/// <param name="context">Required completed context; same active openings as specified by the base remarks.</param>
		/// <param name="level">Raw mask 1 through 63.</param>
		/// <param name="message">Original optional text; all permitted absences remain unchanged.</param>
		/// <param name="ex">Original optional exception reference.</param>
		/// <exception cref="ArgumentNullException">context is null; ParamName is "context".</exception>
		/// <exception cref="ArgumentException">context is noncurrent; ParamName is "context".</exception>
		/// <exception cref="ArgumentOutOfRangeException">level is invalid; ParamName is "level".</exception>
		/// <exception cref="LogDispatchException">Selected direct output failed, after safe reporting.</exception>
		/// <remarks>B07-B10: common base rules apply; no priority between invalid arguments.
		/// <para>F01-F04: every public direct invocation is a new call with one newly
		/// captured UTC event time. An accepted supplied context contributes its same
		/// immutable frames and label facts, including entry annotations, but the selected
		/// context carries the new time. The supplied context is unchanged. Time is not
		/// part of current-frame correspondence and cannot authorize direct delivery.
		/// Trusted LogCaptured delivery instead keeps its original call's complete
		/// context/time without a new sample or public direct-validation pass.</para></remarks>
		public void LogWithContext(LogContext context, LogLevels level, string message = null, Exception ex = null)
		{
			if (context == null)
				throw new ArgumentNullException(nameof(context));

			Logger.DispatchDirect(this, context, level, selected => LogCore(selected, level, message, ex));
		}

		/// <summary>Delivers one permitted ordinary entry after the supplied guard.</summary>
		/// <param name="context">Non-null completed capture already selected for this attempt.</param>
		/// <param name="level">Validated and permitted full raw mask.</param>
		/// <param name="message">Original optional text, unchanged.</param>
		/// <param name="ex">Original optional exception reference.</param>
		/// <exception cref="Exception">Implementation-defined output failure; the invoking guarded boundary handles it.</exception>
		/// <remarks>B08-B10: invoked once only after all gates; never for denial or failed capture/check.
		/// Do not recapture ambient context to replace this value. Effects may precede failure;
		/// there is no rollback, retry, async completion or durability guarantee.</remarks>
		protected abstract void LogCore(LogContext context, LogLevels level, string message, Exception ex);

		internal void LogCaptured(LogContext context, LogLevels level, string message, Exception ex)
		{
			LogCore(context, level, message, ex);
		}

		void ILoggingDestination.Log(LogLevels level, string message, Exception ex)
		{
			Log(level, message, ex);
		}

		void IContextLoggingDestination.LogWithContext(LogContext context, LogLevels level, string message, Exception ex)
		{
			LogWithContext(context, level, message, ex);
		}
	}
}