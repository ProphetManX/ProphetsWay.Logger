using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace ProphetsWay.Logger.Test
{
	internal sealed class IsolatedFileFixture : IDisposable
	{
		private const string ParentPath = @"C:\temp\logger tests";
		private const uint ListDirectory = 0x00000001;
		private const uint BackupSemantics = 0x02000000;
		private const uint OpenReparsePoint = 0x00200000;
		private readonly string _rootPath;
		private readonly string _rootPrefix;
		private readonly HashSet<string> _reservations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		private readonly List<FileStream> _exclusiveHandles = new List<FileStream>();
		private readonly SafeFileHandle _ownership;
		private bool _disposed;

		public IsolatedFileFixture()
		{
			if (Environment.OSVersion.Platform != PlatformID.Win32NT)
				throw new PlatformNotSupportedException("This fixture requires Windows.");

			_rootPath = Path.Combine(ParentPath, "m4b1-" + Guid.NewGuid().ToString("N"));
			_rootPrefix = _rootPath + Path.DirectorySeparatorChar;
			_reservations.Add(_rootPath);
			ValidateDirectoryAncestry(ParentPath);
			CreateFreshDirectory(_rootPath);
			_ownership = OpenDirectoryNative(_rootPath, ListDirectory, FileShare.Read | FileShare.Write,
				IntPtr.Zero, FileMode.Open, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
			if (_ownership.IsInvalid)
			{
				var error = new Win32Exception(Marshal.GetLastWin32Error());
				_ownership.Dispose();
				throw new IOException("Could not hold the newly owned fixture child; retained at " + _rootPath, error);
			}
			try
			{
				ValidateRoot();
			}
			catch
			{
				_ownership.Dispose();
				throw;
			}
		}

		public string GetPath(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
				throw new ArgumentException("A relative fixture path is required.", nameof(relativePath));

			var components = relativePath.Replace('/', Path.DirectorySeparatorChar).Split(Path.DirectorySeparatorChar);
			foreach (var component in components)
			{
				if (!Regex.IsMatch(component, @"\A[A-Za-z0-9_-]+(?:[. ][A-Za-z0-9_-]+)*\z") ||
					Regex.IsMatch(component, @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|\z)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
					throw new ArgumentException("Use ordinary synthetic fixture path components.", nameof(relativePath));
			}

			var path = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
			ValidatePath(path, true);
			return path;
		}

		public void WriteAllBytes(string relativePath, byte[] contents)
		{
			if (contents == null)
				throw new ArgumentNullException(nameof(contents));
			var path = GetPath(relativePath);
			if (ValidatePath(path, false).HasValue)
				throw new IOException("Synthetic setup requires a fresh file.");
			using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				stream.Write(contents, 0, contents.Length);
		}

		public void CreateDirectory(string relativePath)
		{
			var path = GetPath(relativePath);
			foreach (var descendant in Descendants(path))
			{
				var attributes = ValidatePath(descendant, false);
				if (!attributes.HasValue)
					CreateFreshDirectory(descendant);
				else if ((attributes.Value & FileAttributes.Directory) == 0)
					throw new IOException("An owned file obstructs directory preparation.");
			}
		}

		public FileStream OpenExclusive(string relativePath)
		{
			var path = GetPath(relativePath);
			RequireKind(path, false);
			var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			_exclusiveHandles.Add(stream);
			return stream;
		}

		public void DeleteFile(string relativePath)
		{
			var path = GetPath(relativePath);
			RequireClosedHandles();
			RequireKind(path, false);
			File.Delete(path);
		}

		public void DeleteDirectory(string relativePath)
		{
			var path = GetPath(relativePath);
			RequireClosedHandles();
			RequireKind(path, true);
			Directory.Delete(path, false);
		}

		public void Dispose()
		{
			if (_disposed)
				return;
			try
			{
				RequireClosedHandles();
				ValidateRoot();
				var entries = new List<KeyValuePair<string, bool>>();
				CollectOwnedEntries(_rootPath, entries);
				foreach (var entry in entries)
				{
					RequireKind(entry.Key, entry.Value);
					if (entry.Value)
						Directory.Delete(entry.Key, false);
					else
						File.Delete(entry.Key);
				}
				ValidateRoot();
				if (Directory.GetFileSystemEntries(_rootPath).Length != 0)
					throw new IOException("The fixture child changed during cleanup.");
			}
			finally
			{
				_ownership.Dispose();
				_disposed = true;
			}
			ValidateDirectoryAncestry(_rootPath);
			Directory.Delete(_rootPath, false);
		}

		private void CollectOwnedEntries(string directory, List<KeyValuePair<string, bool>> entries)
		{
			if (directory == _rootPath)
				ValidateRoot();
			else
				RequireKind(directory, true);
			foreach (var path in Directory.GetFileSystemEntries(directory))
			{
				var attributes = ValidatePath(path, false);
				if (!attributes.HasValue)
					throw new IOException("An owned entry disappeared during cleanup inspection.");
				var isDirectory = (attributes.Value & FileAttributes.Directory) != 0;
				if (isDirectory)
					CollectOwnedEntries(path, entries);
				entries.Add(new KeyValuePair<string, bool>(path, isDirectory));
			}
		}

		private FileAttributes? ValidatePath(string path, bool reserve)
		{
			ValidateRoot();
			var pending = new List<string>();
			var reachable = true;
			FileAttributes? attributes = null;
			foreach (var descendant in Descendants(path))
			{
				var owned = _reservations.Contains(descendant);
				if (!owned && !reserve)
					throw new IOException("An unreserved entry is not owned by this fixture.");
				attributes = reachable ? ReadAttributes(descendant) : null;
				if (attributes.HasValue)
				{
					if ((attributes.Value & FileAttributes.ReparsePoint) != 0 || !owned)
						throw new IOException("A linked or pre-existing unreserved entry cannot be adopted.");
					reachable = (attributes.Value & FileAttributes.Directory) != 0;
				}
				else
					reachable = false;
				pending.Add(descendant);
			}
			if (reserve)
				foreach (var descendant in pending)
					_reservations.Add(descendant);
			return attributes;
		}

		private IEnumerable<string> Descendants(string path)
		{
			if (!path.StartsWith(_rootPrefix, StringComparison.OrdinalIgnoreCase) || path.Length == _rootPrefix.Length)
				throw new IOException("Only descendants of this fixture child are permitted.");
			var current = _rootPath;
			foreach (var component in path.Substring(_rootPrefix.Length).Split(Path.DirectorySeparatorChar))
			{
				current = Path.Combine(current, component);
				yield return current;
			}
		}

		private void RequireKind(string path, bool directory)
		{
			var attributes = ValidatePath(path, false);
			if (!attributes.HasValue || ((attributes.Value & FileAttributes.Directory) != 0) != directory)
				throw new IOException("The owned fixture entry is missing or has changed kind.");
		}

		private void RequireClosedHandles()
		{
			foreach (var stream in _exclusiveHandles)
				if (stream.CanRead || stream.CanWrite)
					throw new IOException("Close all fixture-issued file handles before deletion or cleanup.");
		}

		private void ValidateRoot()
		{
			if (_disposed)
				throw new ObjectDisposedException(nameof(IsolatedFileFixture));
			if (_ownership.IsClosed || _ownership.IsInvalid)
				throw new IOException("The fixture ownership handle is unavailable.");
			ValidateDirectoryAncestry(_rootPath);
		}

		private static void ValidateDirectoryAncestry(string path)
		{
			var ancestry = new Stack<string>();
			for (var current = path; current != null; current = Path.GetDirectoryName(current))
				ancestry.Push(current);
			foreach (var ancestor in ancestry)
			{
				var attributes = File.GetAttributes(ancestor);
				if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
					throw new IOException("Fixture ancestry must contain only existing ordinary directories.");
			}
		}

		private static FileAttributes? ReadAttributes(string path)
		{
			try
			{
				return File.GetAttributes(path);
			}
			catch (FileNotFoundException)
			{
				return null;
			}
			catch (DirectoryNotFoundException)
			{
				return null;
			}
		}

		private static void CreateFreshDirectory(string path)
		{
			if (!CreateDirectoryNative(path, IntPtr.Zero))
				throw new IOException("Could not exclusively create the reserved fixture directory.", new Win32Exception(Marshal.GetLastWin32Error()));
		}

		[DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CreateDirectoryNative(string path, IntPtr securityAttributes);

		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		private static extern SafeFileHandle OpenDirectoryNative(string path, uint desiredAccess, FileShare shareMode,
			IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
	}
}