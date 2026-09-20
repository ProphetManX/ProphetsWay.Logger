using System;
using System.Collections.Generic;
using System.Globalization;
using ProphetsWay.Utilities.LoggerDestinations;

namespace ProphetsWay.Utilities
{
	/// <summary>Dispatches ordinary and typed log calls to configured recipients.</summary>
	/// <remarks>B01-B07/B23-B24 apply to all helpers below on explicitly configured routes. Validate required
	/// arguments before effects, then capture complete ordered membership for the ordinary route or exact T.
	/// Invoke severity eligibility before payload handoff. False is not failure; a throw withholds that
	/// recipient. Attempt remaining recipients in captured order, including after output throws. No retries.
	/// Eligible recipients receive original values/references, including allowed null/default metadata;
	/// custom state and object graphs are not cloned or frozen. Callback mutation affects later captures.
	/// No consumer callback runs under registry coordination; borrowed recipients are never disposed here.
	/// After attempts, any original failure causes bounded reporting followed by LogDispatchException,
	/// irrespective of other success or reporting failure. Rejected/successful-only calls do not report.
	/// Calls/subscribers may run concurrently; arbitrary consumer objects are not made thread-safe.
	/// Recursion is a new dispatch; B17-B18 suppress only recursive reporting. Unconfigured fallback is
	/// unchanged and unqualified by this contract slice; it is not exercised by M2-C checks.</remarks>
	public static partial class Logger
	{
		/// <summary>Notifies subscribers of one dispatch's bounded safe failure report.</summary>
		/// <remarks>B08-B18/B22: the report argument is never null. Both routes share this event.
		/// After recipient attempts, capture the subscriber list and invoke every entry synchronously in
		/// subscription order, outside registry coordination, then attempt a safe stderr summary of at most
		/// 512 UTF-16 units including any terminator. Contain each reporter error; never alter original counts
		/// or the mandatory throw. No subscriber is required. Reports do not use ordinary Logger fanout.
		/// Normal delegate add/remove rules apply: null is a no-op, duplicates/multicast entries are allowed,
		/// and remove deletes the last matching subsequence. Publication is atomic; changes after capture
		/// affect later notifications. References are retained until removed, without disposal or draining.
		/// While reporting on this synchronous managed-thread stack, nested dispatch still runs and throws
		/// safely when failed, but emits no nested event or stderr. Cleanup restores subsequent reporting.
		/// Other threads are independent; cross-thread/asynchronous cycles are not prevented. No callback
		/// serialization, timeout, guaranteed observation, or process-fatal containment is promised.</remarks>
		public static event Action<LogFailureReport> DispatchFailed;

		private static readonly object DestinationLock = new object();
		private static readonly List<ILoggingDestination> OrdinaryDestinations = new List<ILoggingDestination>();
		private const int RetainedFailureLimit = 8;
		[ThreadStatic]
		private static bool _reportingFailure;

		/// <summary>
		/// Registers a borrowed destination at the end of the ordinary explicit route.
		/// </summary>
		/// <param name="newDest">The non-null destination instance to register.</param>
		/// <exception cref="ArgumentNullException"><paramref name="newDest"/> is null.</exception>
		/// <exception cref="ArgumentException">The same reference is already registered on the ordinary route; ParamName is "newDest".</exception>
		/// <remarks>
		/// Null and duplicate rejection leave membership unchanged. Identity never uses user equality or hashing.
		/// Typed routes are independent, and the same instance may register on other compatible routes.
		/// Publication completes before return without draining captured calls or invoking recipient code.
		/// A reject-all destination remains an active registration; custom recipient state is not frozen.
		/// </remarks>
		public static void AddDestination(ILoggingDestination newDest)
		{
			if (newDest == null)
				throw new ArgumentNullException(nameof(newDest));

			lock (DestinationLock)
			{
				if (OrdinaryDestinations.Exists(destination => ReferenceEquals(destination, newDest)))
					throw new ArgumentException("The destination is already registered on this route.", nameof(newDest));

				OrdinaryDestinations.Add(newDest);
			}
		}

		/// <summary>
		/// Removes a borrowed destination reference from the ordinary explicit route only.
		/// </summary>
		/// <param name="destToRemove">The exact instance to remove. Null or an absent instance is a no-op.</param>
		/// <remarks>
		/// Uses reference identity and preserves survivor order. Never invokes or disposes the recipient.
		/// Returns after publication, not draining; older captures may still call the removed recipient.
		/// Hosts must stop producers and await synchronous calls before disposing borrowed recipients.
		/// </remarks>
		public static void RemoveDestination(ILoggingDestination destToRemove)
		{
			lock (DestinationLock)
			{
				OrdinaryDestinations.RemoveAll(destination => ReferenceEquals(destination, destToRemove));
			}
		}

		/// <summary>
		/// Clears ordinary explicit membership without changing any typed route.
		/// </summary>
		/// <remarks>
		/// An empty route is a no-op. Publishes empty membership without invoking or disposing recipients.
		/// Older captures retain their ordered references and may finish after this method returns.
		/// This is not a drain or a guarantee about subsequent unconfigured-route logging.
		/// </remarks>
		public static void ClearDestinations()
		{
			lock (DestinationLock)
			{
				OrdinaryDestinations.Clear();
			}
		}

		/// <summary>
		/// Now hidden, you shouldn't need to use this method directly, only use the shortcut methods below
		/// </summary>
		/// <param name="level">The severity level of the log statement.</param>
		/// <param name="message">The message you wish to convey in the log entry.</param>
		/// <param name="ex">Optional, pass if you have an exception you want to add to the log entry.</param>
		/// <remarks>
		/// Captures complete ordered membership before eligibility. User code runs outside registry coordination.
		/// Mutations affect later captures only; recursive logging captures anew. Payloads and custom state are not cloned.
		/// </remarks>
		private static void Log(LogLevels level, string message, Exception ex = null)
		{
			ILoggingDestination[] destinations;
			lock (DestinationLock)
			{
				destinations = OrdinaryDestinations.ToArray();
			}

			if (destinations.Length == 0)
			{
				AddDestination(new FileDestination($"Default Log {DateTime.Now:yyyy-MM-dd hh-mm}.log"));
				lock (DestinationLock)
				{
					destinations = OrdinaryDestinations.ToArray();
				}
			}

			List<LogFailureDescriptor> failures = null;
			var overflowCount = 0;
			for (var index = 0; index < destinations.Length; index++)
			{
				var destination = destinations[index];
				try
				{
					if (!destination.ValidateMessageLevel(level))
						continue;
				}
				catch (Exception)
				{
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.Eligibility);
					continue;
				}

				try
				{
					destination.Log(level, message, ex);
				}
				catch (Exception)
				{
					RecordFailure(ref failures, ref overflowCount, index + 1, LogFailureStage.Output);
				}
			}

			ThrowDispatchFailure(failures, overflowCount);
		}

		private static void RecordFailure(ref List<LogFailureDescriptor> failures, ref int overflowCount, int registrationId, LogFailureStage stage)
		{
			if (failures == null)
				failures = new List<LogFailureDescriptor>(RetainedFailureLimit);

			if (failures.Count < RetainedFailureLimit)
				failures.Add(new LogFailureDescriptor(registrationId, stage));
			else
				overflowCount++;
		}

		private static void ThrowDispatchFailure(List<LogFailureDescriptor> failures, int overflowCount)
		{
			if (failures == null)
				return;

			var report = new LogFailureReport(failures, overflowCount);
			if (!_reportingFailure)
			{
				_reportingFailure = true;
				try
				{
					var subscribers = DispatchFailed;
					if (subscribers != null)
					{
						foreach (Action<LogFailureReport> subscriber in subscribers.GetInvocationList())
						{
							try
							{
								subscriber(report);
							}
							catch (Exception)
							{
							}
						}
					}

					try
					{
						Console.Error.Write(string.Format(CultureInfo.InvariantCulture,
							"Log dispatch failed. CorrelationId={0:D}; failures={1}; overflow={2}.\n",
							report.CorrelationId, report.Failures.Count + report.OverflowCount, report.OverflowCount));
					}
					catch (Exception)
					{
					}
				}
				finally
				{
					_reportingFailure = false;
				}
			}

			throw new LogDispatchException(report);
		}

		/// <summary>Logs a message with the exact TraceOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">message is null, before dispatch effects.</exception>
		/// <exception cref="LogDispatchException">A configured eligibility/output callback failed.</exception>
		/// <remarks>B02-B07: follows Logger's configured-route attempt/report/throw contract.</remarks>
		public static void Trace(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.TraceOnly, message);
		}

		/// <summary>Logs a message with the exact DebugOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">message is null, before dispatch effects.</exception>
		/// <exception cref="LogDispatchException">A configured eligibility/output callback failed.</exception>
		/// <remarks>B02-B07: follows Logger's configured-route attempt/report/throw contract.</remarks>
		public static void Debug(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.DebugOnly, message);
		}

		/// <summary>Logs a message with the exact InformationOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">message is null, before dispatch effects.</exception>
		/// <exception cref="LogDispatchException">A configured eligibility/output callback failed.</exception>
		/// <remarks>B02-B07: follows Logger's configured-route attempt/report/throw contract.</remarks>
		public static void Info(string message)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.InformationOnly, message);
		}

		/// <summary>Logs context with the exact WarningOnly bit.</summary>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <param name="ex">Optional original exception reference; null is valid.</param>
		/// <exception cref="ArgumentNullException">message is null, before dispatch effects.</exception>
		/// <exception cref="LogDispatchException">A configured eligibility/output callback failed.</exception>
		/// <remarks>B02-B07: follows Logger's configured-route attempt/report/throw contract.</remarks>
		public static void Warn(string message, Exception ex = null)
		{
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.WarningOnly, message, ex);
		}

		/// <summary>Logs an exception with the exact ErrorOnly bit.</summary>
		/// <param name="ex">Required non-null original exception reference.</param>
		/// <param name="message">Optional original text, including null, empty or whitespace.</param>
		/// <exception cref="ArgumentNullException">ex is null, before dispatch effects.</exception>
		/// <exception cref="LogDispatchException">A configured eligibility/output callback failed.</exception>
		/// <remarks>B02-B07: absent context stays null; follows Logger's configured-route contract.</remarks>
		public static void Error(Exception ex, string message = null)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));

			Log(LogLevels.ErrorOnly, message, ex);
		}

		/// <summary>Logs an exception and context with the exact Critical bit.</summary>
		/// <param name="ex">Required non-null original exception reference.</param>
		/// <param name="message">Required non-null text; empty and whitespace are preserved.</param>
		/// <exception cref="ArgumentNullException">ex or message is null, before dispatch effects.</exception>
		/// <exception cref="LogDispatchException">A configured eligibility/output callback failed.</exception>
		/// <remarks>B02-B07: no priority between invalid arguments; follows Logger's configured-route contract.</remarks>
		public static void Critical(Exception ex, string message)
		{
			if (ex == null)
				throw new ArgumentNullException(nameof(ex));
			if (message == null)
				throw new ArgumentNullException(nameof(message));

			Log(LogLevels.Critical, message, ex);
		}
	}
}
