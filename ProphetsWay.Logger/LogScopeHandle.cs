using System;
using System.Collections.Generic;
using System.Threading;

namespace ProphetsWay.Utilities
{
	/// <summary>Provides local cleanup for one native scope opened by Logger.BeginScope.</summary>
	/// <remarks>A10-A17: library-created, with no public constructor, transfer operation
	/// or exposed frame/state getter. Use this handle only in the operation that opened
	/// it and its normal continuations, including across await and changes of thread.
	/// Do not pass disposal to independent child, sibling, suppressed or unrelated work;
	/// concurrent independent use of this handle is unsupported. Non-transferability is
	/// a caller obligation, not authenticated ownership or an in-process sandbox.
	/// Normally queued child work inherits active frames at execution-context capture.
	/// It opens and ends its own scopes locally. Under supported use, those additions
	/// and cleanup preserve inherited frames and cannot change parent/sibling labels
	/// or interfere with their later cleanup. A child may outlive parent scope end and
	/// retains its captured frames and labels; completed entry captures also remain fixed.
	/// Deliberately suppressed execution-context flow carries no automatic inheritance
	/// to new work; it clears neither the caller nor an already captured child.
	/// Explicitly passed objects still require caller classification.
	/// Ending a scope disposes neither Logger, destinations nor property values, and
	/// neither revokes captured data nor securely erases retained copies.</remarks>
	public sealed class LogScopeHandle : IDisposable
	{
		private static readonly AsyncLocal<LogScopeHandle> Current = new AsyncLocal<LogScopeHandle>();
		private readonly LogScopeHandle _parent;
		private readonly LogScopeFrame _frame;

		/// <summary>Removes this handle's current frame, or does nothing when it is locally absent.</summary>
		/// <exception cref="InvalidOperationException">This handle's frame is present in
		/// the current local stack below another active frame.</exception>
		/// <remarks>A14-A17: the local stack is the ambient scope stack visible to this
		/// invocation. The frame is the distinct frame associated with this opening,
		/// not any frame having equal labels or properties.
		/// If that frame is current (innermost), remove only it and restore its preceding
		/// local frames. If present below another active frame, throw without changing
		/// the stack or marking any handle ended; later correctly ordered cleanup can succeed.
		/// If absent from the local stack, return normally without changing any frame.
		/// Absence is a no-op whether or not this invocation's flow previously ended it;
		/// no creator identity or completed-disposal history is required. Thus repeated
		/// cleanup remains a no-op after subsequent unrelated scopes have opened.
		/// These checks enforce local presence and ordering, not creator identity.
		/// Misusing an inherited handle can remove an inherited frame in the invoking
		/// flow; neither rejection nor inherited-label non-subtraction in that misusing
		/// flow is guaranteed. It grants no supported transfer operation.
		/// A call changes no other flow's stack or captured membership and cannot consume
		/// another flow's later valid cleanup through shared handle-disposal state.
		/// Library-authored error text and Data contain no labels, properties, handle/flow
		/// identifiers, raw cause or payload backlink. Wording is unspecified. This ordinary
		/// misuse exception is not LogDispatchException or a promise to erase CLR diagnostic
		/// state. No DispatchFailed notification, recipient callback or resource disposal
		/// occurs. No asynchronous cleanup, cancellation or drain operation is introduced.</remarks>
		public void Dispose()
		{
			var current = Current.Value;
			if (ReferenceEquals(current, this))
			{
				Current.Value = _parent;
				return;
			}

			for (var scope = current; scope != null; scope = scope._parent)
			{
				if (ReferenceEquals(scope, this))
					throw new InvalidOperationException("Scopes must be ended in nesting order.");
			}
		}

		internal LogScopeHandle(LogScopeFrame frame)
		{
			_frame = frame;
			_parent = Current.Value;
			Current.Value = this;
		}

		internal static LogContext Capture(LogAnnotations entryAnnotations)
		{
			var frames = new List<LogScopeFrame>();
			for (var scope = Current.Value; scope != null; scope = scope._parent)
				frames.Add(scope._frame);

			frames.Reverse();
			return new LogContext(frames, entryAnnotations);
		}
	}
}