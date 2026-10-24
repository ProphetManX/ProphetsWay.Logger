using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ProphetsWay.Utilities
{
	/// <summary>Explicitly connects Microsoft loggers to Logger's exact LogBridgeEntry route.</summary>
	/// <remarks>I01-I05: only Microsoft-to-Logger delivery is installed. Every created
	/// logger shares this provider's lifetime and scope source, but keeps its supplied
	/// category. Delivery uses the common declared metadata type LogBridgeEntry while
	/// retaining the concrete LogBridgeEntry&lt;TState&gt; payload. No ordinary, producer-TState,
	/// runtime-subtype or other metadata route is also invoked. Native registrations,
	/// settings, insertion order, fallback suppression and failure rules remain independent.
	/// An enabled compatible explicit registration suppresses this route's fallback even
	/// when it rejects. With none, existing exact-type automatic fallback receives the
	/// captured producer text and optional exception without a custom destination subclass.
	/// Provider construction, logger creation and scope setup perform no output or file
	/// establishment. No reverse adapter, destination registration or global setting is
	/// implicitly added. The returned ILogger operations have the inbound contract
	/// documented on MicrosoftLogger in this snapshot; their signatures are Microsoft
	/// ILogger's, with no additional public logger construction surface.
	/// Operations may run concurrently. Capture sees a complete provider lifetime/source
	/// choice and complete native route/settings snapshots, not a global transaction
	/// between independent configuration and logical-flow state. No registry or lifetime
	/// lock encloses caller code. Calls are synchronous, not idempotent delivery; callback
	/// changes affect later captures and unrelated recursive logging is a new call.
	/// Mappers and supplied scope providers are borrowed, even if disposable. Retained
	/// state, exceptions, delegates and nested values remain caller-owned. This provider
	/// owns only its created logger/scope adaptation resources; captured users finish
	/// before those resources retire. Hosts quiesce users before disposing borrowed
	/// resources. Retaining recipients own capture before return and later cleanup.
	/// Classification, sanitization, readers, storage and retention remain consumer
	/// responsibilities. This is no sandbox, redaction, drain, queue, retry, process-exit
	/// flush, remote-completion or durability contract.</remarks>
	public sealed class MicrosoftLoggerProvider : ILoggerProvider, ISupportExternalScope
	{
		/// <summary>Creates an inbound provider with optional explicit external label mapping.</summary>
		/// <param name="labelMapper">Borrowed mapper, or null for no new external annotations.</param>
		/// <remarks>I06: retain the mapper without invoking it. Null does not remove native
		/// or already retained controlled-source labels. Initially use a library-owned
		/// logical-flow scope source shared by this provider's loggers. No factory,
		/// recipient, mapper callback, registration change or output is invoked here.</remarks>
		public MicrosoftLoggerProvider(IMicrosoftLogLabelMapper labelMapper = null)
		{
			_labelMapper = labelMapper;
			_ownedScopeProvider = new OwnedExternalScopeProvider();
			_scopeProvider = _ownedScopeProvider;
		}

		/// <summary>Creates a Microsoft logger associated with this provider and category.</summary>
		/// <param name="categoryName">Required category; empty and whitespace are preserved.</param>
		/// <returns>A non-null ILogger retaining the exact category and this provider's lifetime.</returns>
		/// <exception cref="ArgumentNullException">categoryName is null; ParamName is "categoryName".</exception>
		/// <remarks>I07: no trimming, inferred category, output, mapping or ambient capture.
		/// Repeated creation has equivalent behavior; logger reference identity is unspecified.
		/// After provider disposal creation still succeeds for valid input, returning an
		/// inert logger: Log is no-op, IsEnabled false and BeginScope inert. Existing
		/// loggers become inert under the same rule; ordinary argument validation remains.</remarks>
		public ILogger CreateLogger(string categoryName)
		{
			if (categoryName == null)
				throw new ArgumentNullException(nameof(categoryName));

			return new MicrosoftLogger(this, categoryName);
		}

		/// <summary>Uses a supplied external source for subsequent scope captures and openings.</summary>
		/// <param name="scopeProvider">Required borrowed external scope provider.</param>
		/// <exception cref="ArgumentNullException">scopeProvider is null; ParamName is "scopeProvider".</exception>
		/// <remarks>I08: publish the complete source reference atomically without invoking
		/// or disposing either source. All existing loggers use the replacement on later
		/// captures. Already admitted calls keep their captured source; outstanding handles
		/// still clean up the source on which they opened. No frames migrate between sources
		/// and no native scope or registration is changed. Repeating the same reference has
		/// the same effective configuration. A valid call after disposal does nothing.
		/// Microsoft factory scope integration uses this interface; the bridge does not
		/// additionally enumerate its previous owned source and duplicate factory scopes.
		/// Caller-supplied source behavior and the lifetime of its own handles remain that
		/// source's responsibility; this method grants no ownership of it.</remarks>
		public void SetScopeProvider(IExternalScopeProvider scopeProvider)
		{
			if (scopeProvider == null)
				throw new ArgumentNullException(nameof(scopeProvider));

			lock (_gate)
			{
				if (!_disposed)
					_scopeProvider = scopeProvider;
			}
		}

		/// <summary>Disables this provider's inbound registrations without draining captured calls.</summary>
		/// <remarks>I09: idempotent and nonthrowing. Publish retirement before returning;
		/// calls admitted before that boundary may complete using their captured resources.
		/// Later valid Log calls are no-op, IsEnabled is false and new scopes are inert.
		/// Do not clear native routes, remove another provider, dispose borrowed objects,
		/// end callers' open scopes, or reset the automatic file session. Existing scope
		/// handles remain usable for their original local cleanup. Self-disposal from a
		/// callback does not wait for itself or abandon other captured eligible recipients.
		/// A factory may still retain an inert provider reference; disposal does not promise
		/// physical removal from a foreign factory's collection or erasure of retained data.</remarks>
		public void Dispose()
		{
			lock (_gate)
			{
				_disposed = true;
			}
		}

		private readonly object _gate = new object();
		private readonly IMicrosoftLogLabelMapper _labelMapper;
		private readonly OwnedExternalScopeProvider _ownedScopeProvider;
		private readonly AsyncLocal<ScopeNode> _current = new AsyncLocal<ScopeNode>();
		private IExternalScopeProvider _scopeProvider;
		private bool _disposed;

		internal bool TryGetSnapshot(out IExternalScopeProvider scopeProvider,
			out IMicrosoftLogLabelMapper labelMapper)
		{
			lock (_gate)
			{
				if (_disposed)
				{
					scopeProvider = null;
					labelMapper = null;
					return false;
				}

				scopeProvider = _scopeProvider;
				labelMapper = _labelMapper;
				return true;
			}
		}

		internal bool IsEnabled(LogLevel logLevel)
		{
			IExternalScopeProvider ignoredScopeProvider;
			IMicrosoftLogLabelMapper ignoredMapper;
			if (!TryGetSnapshot(out ignoredScopeProvider, out ignoredMapper))
			{
				Logger.MapMicrosoftLogLevel(logLevel);
				return false;
			}

			return Logger.IsMicrosoftLogEnabled(logLevel);
		}

		internal IDisposable BeginScope<TState>(TState state)
		{
			IExternalScopeProvider scopeProvider;
			IMicrosoftLogLabelMapper ignoredMapper;
			if (!TryGetSnapshot(out scopeProvider, out ignoredMapper))
				return InertScopeHandle.Instance;

			var sourceHandle = scopeProvider.Push(state);
			if (sourceHandle == null)
				throw new InvalidOperationException("The scope provider did not return a cleanup handle.");

			var node = new ScopeNode(_current.Value, sourceHandle);
			_current.Value = node;
			return new InboundScopeHandle(this, node);
		}

		internal void Log<TState>(string categoryName, LogLevels level, EventId eventId,
			TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
			IExternalScopeProvider scopeProvider;
			IMicrosoftLogLabelMapper labelMapper;
			if (!TryGetSnapshot(out scopeProvider, out labelMapper) || level == 0)
				return;

			Logger.DispatchMicrosoftInbound(categoryName, level, eventId, state, exception,
				formatter, scopeProvider, labelMapper);
		}

		private sealed class ScopeNode
		{
			internal readonly ScopeNode Parent;
			internal readonly IDisposable SourceHandle;
			internal ScopeNode(ScopeNode parent, IDisposable sourceHandle)
			{
				Parent = parent;
				SourceHandle = sourceHandle;
			}
		}

		private sealed class InboundScopeHandle : IDisposable
		{
			private readonly MicrosoftLoggerProvider _provider;
			private readonly ScopeNode _node;
			internal InboundScopeHandle(MicrosoftLoggerProvider provider, ScopeNode node)
			{
				_provider = provider;
				_node = node;
			}
			public void Dispose()
			{
				var current = _provider._current.Value;
				if (ReferenceEquals(current, _node))
				{
					_provider._current.Value = _node.Parent;
					_node.SourceHandle.Dispose();
					return;
				}
				for (var scope = current; scope != null; scope = scope.Parent)
					if (ReferenceEquals(scope, _node))
						throw new InvalidOperationException("Scopes must be ended in nesting order.");
			}
		}

		private sealed class InertScopeHandle : IDisposable
		{
			internal static readonly InertScopeHandle Instance = new InertScopeHandle();
			public void Dispose() { }
		}

		private sealed class OwnedExternalScopeProvider : IExternalScopeProvider
		{
			private readonly AsyncLocal<OwnedScopeNode> _current = new AsyncLocal<OwnedScopeNode>();
			public IDisposable Push(object state)
			{
				var node = new OwnedScopeNode(_current.Value, state);
				_current.Value = node;
				return new OwnedScopeHandle(this, node);
			}
			public void ForEachScope<TState>(Action<object, TState> callback, TState state)
			{
				var scopes = new List<OwnedScopeNode>();
				for (var node = _current.Value; node != null; node = node.Parent)
					scopes.Add(node);
				for (var index = scopes.Count - 1; index >= 0; index--)
					callback(scopes[index].State, state);
			}
			private sealed class OwnedScopeNode
			{
				internal readonly OwnedScopeNode Parent;
				internal readonly object State;
				internal OwnedScopeNode(OwnedScopeNode parent, object value)
				{
					Parent = parent;
					State = value;
				}
			}
			private sealed class OwnedScopeHandle : IDisposable
			{
				private readonly OwnedExternalScopeProvider _provider;
				private readonly OwnedScopeNode _node;
				internal OwnedScopeHandle(OwnedExternalScopeProvider provider, OwnedScopeNode node)
				{
					_provider = provider;
					_node = node;
				}
				public void Dispose()
				{
					var current = _provider._current.Value;
					if (ReferenceEquals(current, _node))
					{
						_provider._current.Value = _node.Parent;
						return;
					}
				for (var scope = current; scope != null; scope = scope.Parent)
					if (ReferenceEquals(scope, _node))
						throw new InvalidOperationException("Scopes must be ended in nesting order.");
				}
			}
		}

	}
}
