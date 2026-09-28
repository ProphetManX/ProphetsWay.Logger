using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace ProphetsWay.Utilities
{
	/// <summary>Owns one automatic file session for completed native records.</summary>
	/// <remarks>
	/// B01-B12 bind this type and every member. Apply accepted S09-S20 and
	/// S26-S28, with S09 and S14 replaced by accepted report10. The owner is
	/// synchronous, initially inert and shared by its callers, not by a static
	/// registry in this type. It retains configuration, dependencies and bounded
	/// session state, never a record, encoded buffer, raw failure or open stream
	/// between attempts. It performs no rendering, routing or notification.
	/// </remarks>
	internal sealed class AutomaticFileSession
	{
		private readonly object _gate = new object();
		private readonly Func<string> _hostDirectoryProvider;
		private readonly Func<string> _localApplicationDataProvider;
		private readonly Func<DateTime> _utcNowProvider;
		private readonly Func<Guid> _tokenProvider;
		private readonly Action<string> _createDirectory;
		private readonly Func<string, FileMode, FileAccess, FileShare, Stream> _openFile;
		private string _hostDirectoryOverride;
		private string _selectedPath;
		private SessionState _state;

		/// <summary>Creates an uninitialized owner using the trusted BCL defaults.</summary>
		/// <remarks>
		/// B01/B03-B05: construction binds defaults without invoking them, capturing
		/// a host, deriving a path, sampling allocation values or touching files.
		/// It has no borrowed resource, disposal obligation or registration effect.
		/// </remarks>
		internal AutomaticFileSession()
		{
			_hostDirectoryProvider = () => AppContext.BaseDirectory;
			_localApplicationDataProvider = () => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			_utcNowProvider = () => DateTime.UtcNow;
			_tokenProvider = Guid.NewGuid;
			_createDirectory = path => Directory.CreateDirectory(path);
			_openFile = (path, mode, access, share) => new FileStream(path, mode, access, share);
		}

		/// <summary>Creates the same owner with deterministic trusted dependencies.</summary>
		/// <param name="hostDirectoryProvider">Supplies the host base when B03 captures it; null, invalid or throwing results mean unavailable.</param>
		/// <param name="localApplicationDataProvider">Supplies the local-data root only through B03's conditional secondary resolution.</param>
		/// <param name="utcNowProvider">Supplies an allocation DateTime in UTC, independently of record event time; B04 governs sampling and failure.</param>
		/// <param name="tokenProvider">Supplies a Guid for each candidate filename, including replacement collision candidates.</param>
		/// <param name="createDirectory">Prepares the exact supplied full directory, or throws; it does not choose a root or write records.</param>
		/// <param name="openFile">Honors the exact path, mode, access and sharing request, returning a non-null owned writable seekable Stream; B05/B09 govern ownership.</param>
		/// <exception cref="ArgumentNullException">A dependency delegate is null; ParamName names that parameter.</exception>
		/// <remarks>
		/// B01/B10/B12: all six delegates are required and retained, never invoked
		/// during construction. No priority among null dependencies is promised.
		/// They are trusted internal/test-host operations, not consumer extension
		/// points. They must not render, log, notify, re-enter this owner, or acquire
		/// a Logger registry lock. There is no test-specific owner behavior.
		/// </remarks>
		internal AutomaticFileSession(
			Func<string> hostDirectoryProvider,
			Func<string> localApplicationDataProvider,
			Func<DateTime> utcNowProvider,
			Func<Guid> tokenProvider,
			Action<string> createDirectory,
			Func<string, FileMode, FileAccess, FileShare, Stream> openFile)
		{
			_hostDirectoryProvider = hostDirectoryProvider ?? throw new ArgumentNullException(nameof(hostDirectoryProvider));
			_localApplicationDataProvider = localApplicationDataProvider ?? throw new ArgumentNullException(nameof(localApplicationDataProvider));
			_utcNowProvider = utcNowProvider ?? throw new ArgumentNullException(nameof(utcNowProvider));
			_tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
			_createDirectory = createDirectory ?? throw new ArgumentNullException(nameof(createDirectory));
			_openFile = openFile ?? throw new ArgumentNullException(nameof(openFile));
		}

		/// <summary>Configures only the primary directory before initialization starts.</summary>
		/// <param name="directory">Required nonempty directory path, normalized once with DirectoryInfo.FullName at this call, including relative-path resolution.</param>
		/// <exception cref="ArgumentNullException">directory is null; ParamName is directory.</exception>
		/// <exception cref="ArgumentException">directory is empty or rejected by framework path syntax; ParamName is directory for the ordinary argument mapping.</exception>
		/// <exception cref="InvalidOperationException">Initialization has started, succeeded or failed; the configuration is unchanged.</exception>
		/// <exception cref="Exception">Other framework normalization failures propagate as ordinary local errors, not dispatch reports.</exception>
		/// <remarks>
		/// B02/S26-S28: successful calls atomically replace the override only while
		/// Uninitialized; repeating the same directory has the same effective
		/// configuration. Failed calls publish nothing. No invalid-input/state
		/// precedence is promised. Do not trim or probe the path, invoke any injected
		/// dependency, establish output, notify, register a recipient or reset state.
		/// The override never supplies the secondary naming key. The complete
		/// accepted public configuration semantics apply unchanged internally.
		/// </remarks>
		internal void ConfigureHostDirectory(string directory)
		{
			if (directory == null)
				throw new ArgumentNullException(nameof(directory));
			if (directory.Length == 0)
				throw new ArgumentException("A directory path is required.", nameof(directory));

			string fullDirectory;
			try
			{
				fullDirectory = new DirectoryInfo(directory).FullName;
			}
			catch (ArgumentException)
			{
				throw new ArgumentException("The directory path is invalid.", nameof(directory));
			}

			lock (_gate)
			{
				if (_state != SessionState.Uninitialized)
					throw new InvalidOperationException("Automatic file session initialization has already started.");
				_hostDirectoryOverride = fullDirectory;
			}
		}

		/// <summary>Attempts one synchronous append of a completed native record.</summary>
		/// <param name="completedRecord">Non-null completed native record without its physical terminator, supplied after rendering; not a payload or formatter.</param>
		/// <returns>
		/// True only when this record's write, flush and stream disposal all finish
		/// successfully. False only when both initial locations are unusable, or
		/// that initial failure is already remembered. False is a mandatory failed
		/// automatic Output, never a successful or deliberately rejected entry.
		/// </returns>
		/// <exception cref="Exception">Record encoding, selected-stream positioning/write/flush/disposal, or a later selected-path open fails; B07/B09/B11 apply.</exception>
		/// <remarks>
		/// B03-B11: coordinate initialization, then append UTF-8 bytes of the record
		/// followed by exactly one Environment.NewLine, without a preamble, leading
		/// separator, reformatting or content inspection. Empty text therefore adds
		/// just that terminator. Non-null completed text is a trusted handoff
		/// precondition, not a new producer-argument contract. Repeated successful
		/// calls append repeated records; no concurrent-caller order is promised.
		/// Selection survives any subsequent output failure. Never replay, relocate
		/// or retain this record. Release the owned handle before the attempt ends.
		/// This method neither creates a report nor emits diagnostic text; the
		/// trusted caller consumes its bounded outcome under B11 after lock release.
		/// </remarks>
		internal bool TryWriteRecord(string completedRecord)
		{
			lock (_gate)
			{
				if (_state == SessionState.InitialFailureRemembered)
					return false;

				Stream stream;
				if (_state == SessionState.Uninitialized)
				{
					_state = SessionState.Initializing;
					var capturedHostDirectory = AutomaticFilePaths.CaptureHostDirectory(_hostDirectoryProvider);
					var allocationTimeSampled = false;
					string allocationTimestamp = null;
					stream = TryReserveFile(_hostDirectoryOverride ?? capturedHostDirectory, ref allocationTimeSampled, ref allocationTimestamp);
					if (stream == null)
					{
						var secondaryDirectory = AutomaticFilePaths.ResolveSecondaryDirectory(capturedHostDirectory, _localApplicationDataProvider);
						stream = TryReserveFile(secondaryDirectory, ref allocationTimeSampled, ref allocationTimestamp);
					}
					if (stream == null)
					{
						_state = SessionState.InitialFailureRemembered;
						return false;
					}
				}
				else
				{
					stream = _openFile(_selectedPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
				}

				using (stream)
				{
					var bytes = Encoding.UTF8.GetBytes(completedRecord + Environment.NewLine);
					stream.Seek(0, SeekOrigin.End);
					stream.Write(bytes, 0, bytes.Length);
					stream.Flush();
				}
				return true;
			}
		}

		private Stream TryReserveFile(string directory, ref bool allocationTimeSampled, ref string allocationTimestamp)
		{
			if (directory == null || (allocationTimeSampled && allocationTimestamp == null))
				return null;

			try
			{
				_createDirectory(directory);
				if (!allocationTimeSampled)
				{
					allocationTimeSampled = true;
					allocationTimestamp = _utcNowProvider().ToString("yyyy-MM-dd HH-mm-ss.fffffff'Z'", CultureInfo.InvariantCulture);
				}

				while (true)
				{
					var token = _tokenProvider().ToString("N", CultureInfo.InvariantCulture);
					var path = Path.Combine(directory, "Default Log " + allocationTimestamp + "-" + token + ".log");
					Stream stream;
					try
					{
						stream = _openFile(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
					}
					catch (IOException exception) when (exception.HResult == unchecked((int)0x80070050) || exception.HResult == unchecked((int)0x800700B7))
					{
						continue;
					}
					_selectedPath = path;
					_state = SessionState.Established;
					return stream;
				}
			}
			catch (Exception)
			{
				return null;
			}
		}

		private enum SessionState
		{
			Uninitialized,
			Initializing,
			Established,
			InitialFailureRemembered
		}
	}
}