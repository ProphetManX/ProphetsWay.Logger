using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Extensions.Logging;

namespace ProphetsWay.Utilities
{
	/// <summary>Explicitly maps external entry and scope inputs to optional label attachments.</summary>
	/// <remarks>I10: application-supplied code, not an automatic classifier or recipient.
	/// The provider calls it during shared capture before recipient label selection and
	/// before producer formatting. It may therefore see an entry later denied everywhere.
	/// Calls can be concurrent and implementations own synchronization and side effects.
	/// No lock, timeout, rollback, deep cloning, disposal or sandbox is supplied.
	/// A null mapper skips both callbacks. Null results mean no attachment; present-empty
	/// and duplicate occurrences are retained. Neither result can subtract source/native
	/// labels. Only these explicitly selected callbacks attach labels to fresh external
	/// input; property spellings and annotation-shaped raw objects have no authority.</remarks>
	public interface IMicrosoftLogLabelMapper
	{
		/// <summary>Maps one newly entering external event without requiring its rendered text.</summary>
		/// <typeparam name="TState">Unconstrained declared producer state type.</typeparam>
		/// <param name="categoryName">Non-null unchanged category, including empty or whitespace.</param>
		/// <param name="logLevel">Known Microsoft level other than None.</param>
		/// <param name="eventId">Original EventId, including default, Id and optional Name.</param>
		/// <param name="state">Original typed state; null and default(TState) are valid.</param>
		/// <param name="exception">Original optional exception being logged.</param>
		/// <param name="properties">Non-null completed read-only ordered event pairs; possibly empty.</param>
		/// <returns>Optional immutable annotation for this external entry attachment only.</returns>
		/// <exception cref="Exception">Mapping fails; the invoking Log treats it as one shared capture failure.</exception>
		/// <remarks>I11: once for a fresh entry when mapping is reached, never per recipient
		/// and never from IsEnabled. Recognized properties are already copied; duplicate,
		/// null/empty/whitespace keys, null values and default pairs remain present. State
		/// and nested values are not frozen. No producer formatter is invoked to prepare
		/// mapper input. A controlled forward preserves its completed original attachment
		/// without remapping it; a genuinely new call is mapped independently. Returned
		/// annotations are not a replacement for native entry or scope attachments.
		/// The provider accepts null results normally, but never substitutes null after
		/// an exception. No invocation order relative to mapping other frames is promised.</remarks>
		LogAnnotations MapEntry<TState>(string categoryName, LogLevel logLevel,
			EventId eventId, TState state, Exception exception,
			ReadOnlyCollection<KeyValuePair<string, object>> properties);

		/// <summary>Maps one newly captured external scope frame without merging it with an event.</summary>
		/// <param name="state">Original opaque, scalar or structured scope state, including null.</param>
		/// <param name="properties">Non-null completed read-only ordered pairs for this frame; possibly empty.</param>
		/// <returns>Optional immutable annotation belonging only to this external frame.</returns>
		/// <exception cref="Exception">Mapping fails; the invoking Log treats it as one shared capture failure.</exception>
		/// <remarks>I12: once per newly captured frame occurrence when reached in a Log
		/// capture, including empty or repeated frames; not at BeginScope and not per
		/// recipient. Preserve the same pair/value rules as MapEntry. A retained controlled
		/// frame keeps its original attachment without a second mapping. Newly observed
		/// external host frames are mapped independently. Native frames are never passed
		/// here for automatic reinterpretation. Null and empty results cannot remove
		/// enclosing native or retained external labels. No ordering between callbacks
		/// or caching across fresh Log calls is promised.</remarks>
		LogAnnotations MapScope(object state,
			ReadOnlyCollection<KeyValuePair<string, object>> properties);
	}
}
