using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ProphetsWay.Utilities
{
	/// <summary>Retains a producer's exact typed state and formatter with shared bridge facts.</summary>
	/// <typeparam name="TState">Unconstrained declared producer state type, not a metadata marker.</typeparam>
	/// <remarks>B24-B27: inherits the common entry's capture, ownership, origin and
	/// exposure rules. The producer formatter signature follows Microsoft ILogger.Log's
	/// generic delegate shape; this class does not implement ILogger or invoke Log.
	/// There is no public constructor, capture factory, mutation or extra type constraint.</remarks>
	public sealed class LogBridgeEntry<TState> : LogBridgeEntry
	{
		/// <summary>Associates original typed inputs with already available common facts.</summary>
		/// <param name="state">Original TState, including null or default(TState).</param>
		/// <param name="producerFormatter">Original typed producer delegate; null if unavailable.</param>
		/// <param name="categoryName">Original optional category, unchanged.</param>
		/// <param name="eventId">Original optional EventId, unchanged.</param>
		/// <param name="originalLevel">Full native mask, 1 through 63.</param>
		/// <param name="rawMessage">Original optional separately supplied message.</param>
		/// <param name="formattedMessage">Already captured optional producer text.</param>
		/// <param name="exception">Original optional exception payload.</param>
		/// <param name="properties">Required finite stable sequence of application pairs.</param>
		/// <param name="scopes">Required finite stable sequence of non-null external frames.</param>
		/// <param name="annotations">Optional explicitly mapped external entry attachment.</param>
		/// <param name="nativeContext">Original optional completed native context.</param>
		/// <exception cref="ArgumentNullException">properties or scopes is null;
		/// ParamName names that argument.</exception>
		/// <exception cref="ArgumentOutOfRangeException">originalLevel is invalid;
		/// ParamName is "originalLevel".</exception>
		/// <exception cref="ArgumentException">scopes contains a null frame; ParamName is "scopes".</exception>
		/// <exception cref="Exception">Sequence access fails; no usable entry is produced.</exception>
		/// <remarks>B24: common-constructor rules apply unchanged. Store typed inputs
		/// without invoking the delegate or formatting/inspecting state. Association
		/// with previously captured text is supplied by the controlled capture caller;
		/// it is not verified by executing the formatter again. A native raw record
		/// need not have a producer delegate. This absence does not relax the separate
		/// Microsoft ILogger.Log argument contract for an actual Microsoft invocation.</remarks>
		internal LogBridgeEntry(TState state, Func<TState, Exception, string> producerFormatter,
			string categoryName, EventId? eventId, LogLevels originalLevel,
			string rawMessage, string formattedMessage, Exception exception,
			IEnumerable<KeyValuePair<string, object>> properties,
			IEnumerable<LogBridgeScope> scopes, LogAnnotations annotations,
			LogContext nativeContext)
			: base(categoryName, eventId, originalLevel, rawMessage, formattedMessage,
				exception, properties, scopes, annotations, nativeContext)
		{
			State = state;
			ProducerFormatter = producerFormatter;
		}

		/// <summary>Gets the original producer state without erasing its declared type.</summary>
		/// <value>The supplied TState value/reference, including null or default(TState).</value>
		/// <remarks>B25: no cloning, normalization, property conversion or implicit
		/// formatting occurs. Mutable reference-type state remains the same object;
		/// value-type reads follow normal C# value semantics.</remarks>
		public TState State { get; }

		/// <summary>Gets the producer delegate with its original generic signature.</summary>
		/// <value>The supplied Func&lt;TState, Exception, string&gt; reference, or null for absence.</value>
		/// <remarks>B26: preserve the actual delegate and its state/exception association,
		/// including any caller-owned closure. This getter neither wraps nor executes it.
		/// Shared delivery uses FormattedMessage; retaining the original is fidelity,
		/// not permission for controlled adapters to recapture text per recipient.</remarks>
		public Func<TState, Exception, string> ProducerFormatter { get; }

		/// <inheritdoc />
		/// <remarks>B27: the common view of State, with the inherited boxing rules.</remarks>
		public override object OriginalState
		{
			get
			{
				return State;
			}
		}

		/// <inheritdoc />
		/// <remarks>B27: exactly typeof(TState), irrespective of the current state value.</remarks>
		public override Type StateType
		{
			get
			{
				return typeof(TState);
			}
		}

		/// <inheritdoc />
		/// <remarks>B27: exactly ProducerFormatter viewed as Delegate, including null.</remarks>
		public override Delegate OriginalProducerFormatter
		{
			get
			{
				return ProducerFormatter;
			}
		}
	}
}