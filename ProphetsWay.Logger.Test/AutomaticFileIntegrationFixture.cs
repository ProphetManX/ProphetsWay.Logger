using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ProphetsWay.Logger.Test
{
	internal sealed class AutomaticFileIntegrationFixture : IDisposable
	{
		private const BindingFlags PublicStatic = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;
		private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		private readonly object _gate = new object();
		private readonly Type _loggerType;
		private readonly FieldInfo _ownerField;
		private readonly object _owner;
		private readonly FieldInfo[] _dependencies;
		private readonly string _rootPrefix;
		private readonly List<Subscription> _subscriptions = new List<Subscription>();
		private readonly List<SafeFileHandle> _handles = new List<SafeFileHandle>();
		private bool _bindingAttempted;
		private bool _bound;
		private bool _untrackedStream;
		private bool _disposing;
		private bool _disposed;
		private int _activeOperations;

		public AutomaticFileIntegrationFixture()
		{
			ProductAssembly = Assembly.Load(File.ReadAllBytes(typeof(Utilities.Logger).Assembly.Location));
			_loggerType = ProductAssembly.GetType("ProphetsWay.Utilities.Logger", true);
			var ownerType = ProductAssembly.GetType("ProphetsWay.Utilities.AutomaticFileSession", true);
			var owners = _loggerType.GetFields(PublicStatic | BindingFlags.NonPublic)
				.Where(field => field.FieldType == ownerType).ToArray();
			if (owners.Length == 0)
				throw new MissingFieldException(_loggerType.FullName, "static AutomaticFileSession owner");
			if (owners.Length != 1)
				throw new AmbiguousMatchException("Logger must contain exactly one static AutomaticFileSession owner.");
			_ownerField = owners[0];
			if (!_ownerField.IsInitOnly || !_ownerField.IsPrivate)
				throw new InvalidOperationException("The real Logger owner must be private static readonly.");
			var configure = ResolvePublic("ConfigureAutomaticFileHostDirectory", Type.EmptyTypes, new[] { typeof(string) });
			if (configure.ReturnType != typeof(void))
				throw new MissingMethodException(_loggerType.FullName, "void ConfigureAutomaticFileHostDirectory(string)");
			_owner = _ownerField.GetValue(null);
			if (_owner == null || _owner.GetType() != ownerType)
				throw new InvalidOperationException("The production-created Logger owner is unavailable.");
			var names = new[] { "_hostDirectoryProvider", "_localApplicationDataProvider", "_utcNowProvider", "_tokenProvider", "_createDirectory", "_openFile" };
			var types = new[]
			{
				typeof(Func<string>), typeof(Func<string>), typeof(Func<DateTime>), typeof(Func<Guid>),
				typeof(Action<string>), typeof(Func<string, FileMode, FileAccess, FileShare, Stream>)
			};
			_dependencies = new FieldInfo[names.Length];
			for (var index = 0; index < names.Length; index++)
			{
				var field = OwnerField(names[index]);
				if (!field.IsInitOnly || field.IsStatic || field.FieldType != types[index] ||
					!field.FieldType.IsInstanceOfType(field.GetValue(_owner)))
					throw new InvalidOperationException("An owner dependency is not the required readonly instance delegate: " + names[index]);
				_dependencies[index] = field;
			}
			RequireUnusedOwner();
			Files = new AutomaticFileSessionFixture();
			try
			{
				_rootPrefix = Path.GetDirectoryName(Files.PrimaryDirectory) + Path.DirectorySeparatorChar;
			}
			catch
			{
				Files.Dispose();
				throw;
			}
		}

		public Assembly ProductAssembly { get; }
		public AutomaticFileSessionFixture Files { get; }

		public void BindUnusedOwner(Func<string> hostDirectoryProvider,
			Func<string> localApplicationDataProvider, Func<DateTime> utcNowProvider,
			Func<Guid> tokenProvider, Action<string> createDirectory,
			Func<string, FileMode, FileAccess, FileShare, Stream> openFile)
		{
			var replacements = new Delegate[] { hostDirectoryProvider, localApplicationDataProvider, utcNowProvider, tokenProvider, createDirectory, openFile };
			lock (_gate)
			{
				ThrowIfUnavailable();
				if (_bindingAttempted || _activeOperations != 0)
					throw new InvalidOperationException("Bind the unused owner exactly once before operations.");
				RequireUnusedOwner();
				for (var index = 0; index < replacements.Length; index++)
					if (replacements[index] == null || !_dependencies[index].FieldType.IsInstanceOfType(replacements[index]))
						throw new ArgumentException("Every replacement must match its owner dependency type.");
				_bindingAttempted = true;
				for (var index = 0; index < replacements.Length; index++)
					_dependencies[index].SetValue(_owner, replacements[index]);
				RequireUnusedOwner();
				for (var index = 0; index < replacements.Length; index++)
					if (!ReferenceEquals(_dependencies[index].GetValue(_owner), replacements[index]))
						throw new InvalidOperationException("An owner dependency did not retain its replacement.");
				_bound = true;
			}
		}

		public object InvokePublic(string name, Type[] genericArguments, Type[] parameterTypes, params object[] arguments)
		{
			BeginOperation(true);
			try { return Invoke(ResolvePublic(name, genericArguments, parameterTypes), arguments); }
			finally { EndOperation(); }
		}

		public IDisposable SubscribeFailures(Action<object> observer)
		{
			if (observer == null)
				throw new ArgumentNullException(nameof(observer));
			BeginOperation(true);
			try
			{
				var notification = _loggerType.GetEvent("DispatchFailed", PublicStatic);
				var reportType = ProductAssembly.GetType("ProphetsWay.Utilities.LogFailureReport", true);
				if (notification == null || notification.EventHandlerType != typeof(Action<>).MakeGenericType(reportType) ||
					notification.GetAddMethod() == null || notification.GetRemoveMethod() == null)
					throw new MissingMemberException(_loggerType.FullName, "DispatchFailed");
				var report = Expression.Parameter(reportType, "report");
				var callback = Expression.Lambda(notification.EventHandlerType,
					Expression.Invoke(Expression.Constant(observer), Expression.Convert(report, typeof(object))), report).Compile();
				var subscription = new Subscription(() => Invoke(notification.GetRemoveMethod(), new object[] { callback }));
				Invoke(notification.GetAddMethod(), new object[] { callback });
				lock (_gate)
					_subscriptions.Add(subscription);
				return subscription;
			}
			finally { EndOperation(); }
		}

		public IDisposable ObserveDestination(object destination, Action<object, object> observer)
		{
			if (destination == null)
				throw new ArgumentNullException(nameof(destination));
			if (observer == null)
				throw new ArgumentNullException(nameof(observer));
			BeginOperation(true);
			try
			{
				var destinationType = destination.GetType();
				var field = destinationType.GetField("LoggingEvent", BindingFlags.Public | BindingFlags.Instance);
				if (destinationType.Assembly != ProductAssembly || field == null || field.IsInitOnly ||
					field.DeclaringType.Assembly != ProductAssembly || !field.FieldType.IsGenericType ||
					field.FieldType.GetGenericTypeDefinition() != typeof(EventHandler<>))
					throw new MissingFieldException(destinationType.FullName, "LoggingEvent");
				var argumentType = field.FieldType.GetGenericArguments()[0];
				if (argumentType.Assembly != ProductAssembly)
					throw new InvalidOperationException("Destination arguments must belong to this product copy.");
				var sender = Expression.Parameter(typeof(object), "sender");
				var arguments = Expression.Parameter(argumentType, "arguments");
				var callback = Expression.Lambda(field.FieldType, Expression.Invoke(Expression.Constant(observer),
					sender, Expression.Convert(arguments, typeof(object))), sender, arguments).Compile();
				var subscription = new Subscription(() =>
				{
					lock (_gate)
						field.SetValue(destination, Delegate.Remove((Delegate)field.GetValue(destination), callback));
				});
				lock (_gate)
				{
					field.SetValue(destination, Delegate.Combine((Delegate)field.GetValue(destination), callback));
					_subscriptions.Add(subscription);
				}
				return subscription;
			}
			finally { EndOperation(); }
		}

		public void CreateDirectoryWith(Action<string> createDirectory, string fullPath)
		{
			if (createDirectory == null)
				throw new ArgumentNullException(nameof(createDirectory));
			BeginOperation(false);
			try
			{
				ReserveFullPath(fullPath);
				createDirectory(fullPath);
			}
			finally { EndOperation(); }
		}

		public Stream OpenFileWith(Func<string, FileMode, FileAccess, FileShare, Stream> openFile,
			string fullPath, FileMode mode, FileAccess access, FileShare share)
		{
			if (openFile == null)
				throw new ArgumentNullException(nameof(openFile));
			BeginOperation(false);
			try
			{
				ReserveFullPath(fullPath);
				var stream = openFile(fullPath, mode, access, share);
				try
				{
					var file = stream as FileStream;
					if (file == null)
						throw new InvalidOperationException("The default open delegate must return its actual FileStream.");
					var handle = file.SafeFileHandle;
					lock (_gate)
						_handles.Add(handle);
					return file;
				}
				catch
				{
					lock (_gate)
						_untrackedStream = true;
					throw;
				}
			}
			finally { EndOperation(); }
		}

		public void Dispose()
		{
			Subscription[] subscriptions;
			lock (_gate)
			{
				if (_disposed)
					return;
				if (_disposing || _activeOperations != 0 || _untrackedStream || _handles.Any(handle => !handle.IsClosed))
					throw new IOException("Finish operations and close all default-delegate handles before fixture cleanup.");
				_disposing = true;
				subscriptions = _subscriptions.ToArray();
			}
			try
			{
				foreach (var subscription in subscriptions)
					subscription.Dispose();
				Files.Dispose();
				lock (_gate)
					_disposed = true;
			}
			finally
			{
				lock (_gate)
					_disposing = false;
			}
		}

		private MethodInfo ResolvePublic(string name, Type[] genericArguments, Type[] parameterTypes)
		{
			if (name == null)
				throw new ArgumentNullException(nameof(name));
			if (genericArguments == null)
				throw new ArgumentNullException(nameof(genericArguments));
			if (parameterTypes == null)
				throw new ArgumentNullException(nameof(parameterTypes));
			MethodInfo selected = null;
			foreach (var definition in _loggerType.GetMethods(PublicStatic).Where(method => method.Name == name))
			{
				if (definition.GetGenericArguments().Length != genericArguments.Length)
					continue;
				var method = definition;
				if (definition.IsGenericMethodDefinition)
				{
					try { method = definition.MakeGenericMethod(genericArguments); }
					catch (ArgumentException) { continue; }
				}
				if (method.ContainsGenericParameters || !method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes))
					continue;
				if (selected != null)
					throw new AmbiguousMatchException("The requested public Logger overload is ambiguous: " + name);
				selected = method;
			}
			return selected ?? throw new MissingMethodException(_loggerType.FullName, name);
		}

		private FieldInfo OwnerField(string name)
		{
			return _owner.GetType().GetField(name, PrivateInstance) ?? throw new MissingFieldException(_owner.GetType().FullName, name);
		}

		private void RequireUnusedOwner()
		{
			if (!ReferenceEquals(_ownerField.GetValue(null), _owner))
				throw new InvalidOperationException("The real static owner identity changed.");
			foreach (var name in new[] { "_selectedPath", "_hostDirectoryOverride" })
			{
				var field = OwnerField(name);
				if (field.FieldType != typeof(string) || field.GetValue(_owner) != null)
					throw new InvalidOperationException("The real owner must have no selected path or configured override.");
			}
			var state = OwnerField("_state");
			if (!state.FieldType.IsEnum || state.GetValue(_owner).ToString() != "Uninitialized")
				throw new InvalidOperationException("The real owner must be unused before binding.");
		}

		private void ReserveFullPath(string fullPath)
		{
			if (fullPath == null)
				throw new ArgumentNullException(nameof(fullPath));
			var normalized = fullPath.Replace('/', Path.DirectorySeparatorChar);
			if (!normalized.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
				throw new IOException("Only descendants of this fixture child are permitted.");
			var reserved = Files.GetPath(normalized.Substring(_rootPrefix.Length));
			if (!string.Equals(reserved, Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase))
				throw new IOException("The supplied path does not match its reserved fixture path.");
		}

		private void BeginOperation(bool requireBinding)
		{
			lock (_gate)
			{
				ThrowIfUnavailable();
				if (requireBinding && !_bound)
					throw new InvalidOperationException("Bind the unused owner before accessing public Logger operations.");
				if (!ReferenceEquals(_ownerField.GetValue(null), _owner))
					throw new InvalidOperationException("The real static owner identity changed.");
				_activeOperations++;
			}
		}

		private void EndOperation()
		{
			lock (_gate)
				_activeOperations--;
		}

		private void ThrowIfUnavailable()
		{
			if (_disposed || _disposing)
				throw new ObjectDisposedException(nameof(AutomaticFileIntegrationFixture));
		}

		private static object Invoke(MethodInfo method, object[] arguments)
		{
			try { return method.Invoke(null, arguments); }
			catch (TargetInvocationException failure) when (failure.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
				throw;
			}
		}

		private sealed class Subscription : IDisposable
		{
			private Action _detach;

			internal Subscription(Action detach) { _detach = detach; }

			public void Dispose()
			{
				var detach = Interlocked.Exchange(ref _detach, null);
				if (detach != null)
					detach();
			}
		}
	}
}