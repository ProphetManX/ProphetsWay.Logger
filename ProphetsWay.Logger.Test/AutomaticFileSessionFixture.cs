using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ProphetsWay.Logger.Test
{
	internal sealed class AutomaticFileSessionFixture : IDisposable
	{
		private readonly IsolatedFileFixture _fixture;
		private readonly object _gate = new object();
		private readonly string _rootPrefix;
		private readonly List<ObservedStream> _streams = new List<ObservedStream>();
		private Thread[] _workers = new Thread[0];
		private bool _workersRunning;
		private int _activeOperations;
		private bool _disposed;

		public AutomaticFileSessionFixture()
		{
			_fixture = new IsolatedFileFixture();
			try
			{
				PrimaryDirectory = _fixture.GetPath("primary");
				LocalRoot = _fixture.GetPath("local");
				_rootPrefix = Path.GetDirectoryName(PrimaryDirectory) + Path.DirectorySeparatorChar;
			}
			catch
			{
				_fixture.Dispose();
				throw;
			}
		}

		public string PrimaryDirectory { get; }
		public string LocalRoot { get; }

		public string GetPath(string relativePath)
		{
			lock (_gate)
			{
				ThrowIfDisposed();
				return _fixture.GetPath(relativePath);
			}
		}

		public void CreateDirectory(string fullPath)
		{
			lock (_gate)
			{
				ThrowIfDisposed();
				_fixture.CreateDirectory(ReserveFullPath(fullPath));
			}
		}

		public ObservedStream Open(string fullPath, FileMode mode, FileAccess access, FileShare share)
		{
			BeginOperation(fullPath);
			FileStream stream = null;
			try
			{
				stream = new FileStream(fullPath, mode, access, share);
				var observed = new ObservedStream(stream);
				lock (_gate)
					_streams.Add(observed);
				return observed;
			}
			catch
			{
				if (stream != null)
					stream.Dispose();
				throw;
			}
			finally
			{
				EndOperation();
			}
		}

		public void Seed(string fullPath, byte[] contents)
		{
			if (contents == null)
				throw new ArgumentNullException(nameof(contents));
			using (var stream = Open(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				stream.Write(contents, 0, contents.Length);
		}

		public byte[] ReadAllBytes(string fullPath)
		{
			BeginOperation(fullPath);
			try
			{
				return File.ReadAllBytes(fullPath);
			}
			finally
			{
				EndOperation();
			}
		}

		public void DeleteFile(string fullPath)
		{
			lock (_gate)
			{
				ThrowIfDisposed();
				RequireIdleAndClosed();
				_fixture.DeleteFile(ReserveFullPath(fullPath));
			}
		}

		public void DeleteDirectory(string fullPath)
		{
			lock (_gate)
			{
				ThrowIfDisposed();
				RequireIdleAndClosed();
				_fixture.DeleteDirectory(ReserveFullPath(fullPath));
			}
		}

		public Exception[] RunWorkers(Action[] workers, Action coordinate)
		{
			if (workers == null)
				throw new ArgumentNullException(nameof(workers));
			if (coordinate == null)
				throw new ArgumentNullException(nameof(coordinate));
			var errors = new Exception[workers.Length];
			var threads = new Thread[workers.Length];
			for (var index = 0; index < workers.Length; index++)
			{
				var workerIndex = index;
				var worker = workers[index];
				if (worker == null)
					throw new ArgumentException("Every worker must be supplied.", nameof(workers));
				threads[index] = new Thread(() =>
				{
					try { worker(); }
					catch (Exception failure) { errors[workerIndex] = failure; }
				}) { IsBackground = true };
			}
			lock (_gate)
			{
				ThrowIfDisposed();
				if (_workersRunning)
					throw new InvalidOperationException("A worker run is already active.");
				_workers = threads;
				_workersRunning = true;
			}
			var started = 0;
			try
			{
				foreach (var thread in threads)
				{
					thread.Start();
					started++;
				}
				coordinate();
			}
			finally
			{
				var joined = true;
				for (var index = 0; index < started; index++)
				{
					try { joined &= threads[index].Join(TimeSpan.FromSeconds(5)); }
					catch (Exception) { joined = false; }
				}
				if (!joined)
					Environment.FailFast("Session fixture workers did not terminate; aborting the isolated test host before shared teardown.");
				lock (_gate)
					_workersRunning = false;
			}
			return errors;
		}

		public void Dispose()
		{
			lock (_gate)
			{
				if (_disposed)
					return;
				RequireIdleAndClosed();
				_disposed = true;
			}
			_fixture.Dispose();
		}

		private string ReserveFullPath(string fullPath)
		{
			if (fullPath == null)
				throw new ArgumentNullException(nameof(fullPath));
			var normalized = fullPath.Replace('/', Path.DirectorySeparatorChar);
			if (!normalized.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase))
				throw new IOException("Only descendants of this fixture child are permitted.");
			var relative = normalized.Substring(_rootPrefix.Length);
			var reserved = _fixture.GetPath(relative);
			if (!string.Equals(reserved, Path.GetFullPath(fullPath), StringComparison.OrdinalIgnoreCase))
				throw new IOException("The supplied path does not match its reserved fixture path.");
			return relative;
		}

		private void BeginOperation(string fullPath)
		{
			lock (_gate)
			{
				ThrowIfDisposed();
				ReserveFullPath(fullPath);
				_activeOperations++;
			}
		}

		private void EndOperation()
		{
			lock (_gate)
				_activeOperations--;
		}

		private void RequireIdleAndClosed()
		{
			if (_workersRunning || _activeOperations != 0)
				throw new IOException("Finish fixture workers and file operations before deletion or cleanup.");
			foreach (var worker in _workers)
				if (worker.IsAlive)
					throw new IOException("A fixture worker is still running; cleanup is forbidden.");
			foreach (var stream in _streams)
				if (!stream.HandleClosed)
					throw new IOException("Close all fixture-issued file handles before deletion or cleanup.");
		}

		private void ThrowIfDisposed()
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(AutomaticFileSessionFixture));
		}

		internal sealed class ObservedStream : Stream
		{
			private readonly FileStream _stream;
			private readonly SafeFileHandle _handle;
			private int _seekCalls;
			private int _writeCalls;
			private int _flushCalls;
			private int _readCalls;
			private int _disposeCalls;

			internal ObservedStream(FileStream stream)
			{
				_stream = stream;
				_handle = stream.SafeFileHandle;
			}

			public Action<string> AfterOperation { get; set; }
			public int SeekCalls => Volatile.Read(ref _seekCalls);
			public int WriteCalls => Volatile.Read(ref _writeCalls);
			public int FlushCalls => Volatile.Read(ref _flushCalls);
			public int ReadCalls => Volatile.Read(ref _readCalls);
			public int DisposeCalls => Volatile.Read(ref _disposeCalls);
			public bool HandleClosed => _handle.IsClosed;
			public override bool CanRead => _stream.CanRead;
			public override bool CanSeek => _stream.CanSeek;
			public override bool CanWrite => _stream.CanWrite;
			public override bool CanTimeout => _stream.CanTimeout;
			public override long Length => _stream.Length;

			public override long Position
			{
				get => _stream.Position;
				set
				{
					Interlocked.Increment(ref _seekCalls);
					_stream.Position = value;
					AfterOperation?.Invoke("Seek");
				}
			}

			public override int ReadTimeout
			{
				get => _stream.ReadTimeout;
				set => _stream.ReadTimeout = value;
			}

			public override int WriteTimeout
			{
				get => _stream.WriteTimeout;
				set => _stream.WriteTimeout = value;
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				Interlocked.Increment(ref _seekCalls);
				var position = _stream.Seek(offset, origin);
				AfterOperation?.Invoke("Seek");
				return position;
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				Interlocked.Increment(ref _readCalls);
				return _stream.Read(buffer, offset, count);
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				Interlocked.Increment(ref _writeCalls);
				_stream.Write(buffer, offset, count);
				AfterOperation?.Invoke("Write");
			}

			public override void Flush()
			{
				Interlocked.Increment(ref _flushCalls);
				_stream.Flush();
				AfterOperation?.Invoke("Flush");
			}

			public override void SetLength(long value)
			{
				_stream.SetLength(value);
			}

			protected override void Dispose(bool disposing)
			{
				if (disposing)
				{
					Interlocked.Increment(ref _disposeCalls);
					try { _stream.Dispose(); }
					finally { base.Dispose(disposing); }
					AfterOperation?.Invoke("Dispose");
				}
				else
					base.Dispose(disposing);
			}
		}
	}
}