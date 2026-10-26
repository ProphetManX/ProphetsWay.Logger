using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace ProphetsWay.Logger.Tests
{
	internal sealed class IsolatedFileFixture : IDisposable
	{
		private const string ParentPath = @"C:\temp\logger tests";
		private const uint PosixDirectoryMode = 0x1C0;
		private static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
		private static readonly StringComparison PathComparison = IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		private static readonly StringComparer PathComparer = IsWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
		private readonly string _rootPath;
		private readonly string _rootPrefix;
		private readonly string _rootName;
		private readonly HashSet<string> _reservations = new HashSet<string>(PathComparer);
		private readonly Dictionary<string, EntryIdentity> _ownedEntries = new Dictionary<string, EntryIdentity>(PathComparer);
		private readonly List<FileStream> _exclusiveHandles = new List<FileStream>();
		private readonly SafeFileHandle _parentOwnership;
		private readonly List<SafeFileHandle> _windowsAncestry;
		private readonly List<SafeFileHandle> _posixAncestry;
		private readonly SafeFileHandle _ownership;
		private bool _disposed;

		public IsolatedFileFixture()
		{
			if (!IsWindows && !IsSupportedPosixPlatform())
				throw new PlatformNotSupportedException("The fixture supports Windows, Linux, and macOS.");

			var parentPath = GetParentPath();
			PrepareParentDirectory(parentPath);
			_rootName = "m4b1-" + Guid.NewGuid().ToString("N");
			_rootPath = Path.Combine(parentPath, _rootName);
			_rootPrefix = _rootPath + Path.DirectorySeparatorChar;
			_reservations.Add(_rootPath);
			if (IsWindows)
			{
				_parentOwnership = null;
				_windowsAncestry = PinWindowsAncestry(parentPath, false);
				try
				{
					CreateFreshDirectory(_rootPath);
					_ownership = OpenWindowsEntry(_rootPath, true);
				}
				catch
				{
					CloseHandles(_windowsAncestry);
					throw;
				}
			}
			else
			{
				_posixAncestry = PinPosixAncestry(parentPath, false);
				_parentOwnership = DuplicateDirectory(_posixAncestry[_posixAncestry.Count - 1]);
				if (_parentOwnership.IsInvalid)
				{
					var error = new Win32Exception(Marshal.GetLastWin32Error());
					_parentOwnership.Dispose();
					CloseHandles(_posixAncestry);
					throw new IOException("Could not hold the approved fixture parent.", error);
				}
				if (CreateDirectoryAtPosix(GetFileDescriptor(_parentOwnership), _rootName, PosixDirectoryMode) != 0)
				{
					var error = new Win32Exception(Marshal.GetLastWin32Error());
					_parentOwnership.Dispose();
					CloseHandles(_posixAncestry);
					throw new IOException("Could not exclusively create the reserved fixture directory.", error);
				}
				_ownership = OpenDirectoryAtPosix(GetFileDescriptor(_parentOwnership), _rootName, PosixDirectoryOpenFlags());
			}
			if (_ownership.IsInvalid)
			{
				var error = new Win32Exception(Marshal.GetLastWin32Error());
				_ownership.Dispose();
				if (_parentOwnership != null)
					_parentOwnership.Dispose();
				CloseHandles(_posixAncestry);
				throw new IOException("Could not hold the newly owned fixture child; retained at " + _rootPath, error);
			}
			try
			{
				_ownedEntries.Add(_rootPath, IsWindows ? GetWindowsIdentity(_ownership) : GetEntryIdentity(GetFileDescriptor(_ownership)));
				ValidateRoot();
			}
			catch
			{
				_ownership.Dispose();
				if (_parentOwnership != null)
					_parentOwnership.Dispose();
				CloseHandles(_posixAncestry);
				CloseHandles(_windowsAncestry);
				throw;
			}
		}

		public string RootPath
		{
			get { return _rootPath; }
		}

		public void ValidateRootPath()
		{
			ValidateRoot();
		}

		private string ResolveFixturePath(string relativePath)
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
			return path;
		}

		public string GetPath(string relativePath)
		{
			var path = ResolveFixturePath(relativePath);
			if (IsWindows)
				ValidatePath(path, true);
			else
				ReservePosixPath(path);
			return path;
		}

		// Explicit, quiescent product-effect boundaries. Never used by GetPath or cleanup.
		public void ObserveCreatedEntries()
		{
			ValidateRoot();
			var observed = new Dictionary<string, EntryIdentity>(_ownedEntries, PathComparer);
			var paths = new List<string>(_reservations);
			paths.Sort((left, right) => left.Length.CompareTo(right.Length));
			foreach (var path in paths)
			{
				if (PathComparer.Equals(path, _rootPath))
					continue;
				EntryIdentity? actual;
				if (IsWindows)
				{
					var parents = PinWindowsOwnedParent(path, observed, true);
					try { actual = parents == null ? (EntryIdentity?)null : ReadWindowsEntryIdentity(path); }
					finally { CloseHandles(parents); }
				}
				else
				{
					using (var parent = OpenObservationPosixParent(path, observed))
						actual = parent == null ? (EntryIdentity?)null : ReadPosixEntryIdentity(GetFileDescriptor(parent), Path.GetFileName(path));
				}
				if (observed.TryGetValue(path, out var expected))
				{
					if (!actual.HasValue || !actual.Value.Equals(expected))
						throw new IOException("An owned fixture entry disappeared or changed identity or kind.");
				}
				else if (actual.HasValue)
				{
					if (actual.Value.FileType != PosixDirectoryType() && actual.Value.FileType != PosixRegularFileType())
						throw new IOException("Only ordinary reserved fixture entries can be observed.");
					observed.Add(path, actual.Value);
				}
			}
			// Failed observations leave all prior identity state intact.
			foreach (var entry in observed)
				if (!_ownedEntries.ContainsKey(entry.Key))
					_ownedEntries.Add(entry.Key, entry.Value);
		}

		public void ObserveDeletedFile(string relativePath)
		{
			var path = ResolveFixturePath(relativePath);
			ValidateRoot();
			if (!_reservations.Contains(path) || GetOwnedEntryIdentity(path).FileType != PosixRegularFileType())
				throw new IOException("Only a previously owned ordinary file can be observed as deleted.");
			EntryIdentity? actual;
			if (IsWindows)
			{
				var parents = PinWindowsOwnedParent(path);
				try { actual = ReadWindowsEntryIdentity(path); }
				finally { CloseHandles(parents); }
			}
			else
			{
				using (var parent = OpenPosixParent(path))
					actual = ReadPosixEntryIdentity(GetFileDescriptor(parent), Path.GetFileName(path));
			}
			if (actual.HasValue)
				throw new IOException("The selected owned file is still present; no identity was retired.");
			_ownedEntries.Remove(path);
		}

		internal void ObserveOpenedFile(string relativePath, SafeFileHandle handle)
		{
			var path = ResolveFixturePath(relativePath);
			ValidateRoot();
			if (!_reservations.Contains(path))
				throw new IOException("An opened file must have been reserved before creation.");
			var identity = IsWindows ? GetWindowsIdentity(handle) : GetEntryIdentity(GetFileDescriptor(handle));
			if (identity.FileType != PosixRegularFileType() ||
				(_ownedEntries.TryGetValue(path, out var expected) && !identity.Equals(expected)))
				throw new IOException("The opened file changed identity or kind.");
			if (IsWindows)
			{
				var parents = PinWindowsOwnedParent(path);
				try
				{
					var finalPath = new StringBuilder(32768);
					var length = GetFinalPathNameWindows(handle, finalPath, (uint)finalPath.Capacity, 0);
					if (length == 0 || length >= finalPath.Capacity ||
						!string.Equals(finalPath.ToString(), @"\\?\" + path, PathComparison))
						throw new IOException("The opened handle does not refer to the reserved fixture file.");
				}
				finally { CloseHandles(parents); }
			}
			else
			{
				using (var parent = OpenPosixParent(path))
					RequireSamePosixEntry(GetFileDescriptor(parent), Path.GetFileName(path), identity);
			}
			_ownedEntries[path] = identity;
		}

		private SafeFileHandle OpenObservationPosixParent(string path, Dictionary<string, EntryIdentity> observed)
		{
			var current = DuplicateDirectory(_ownership);
			try
			{
				var currentPath = _rootPath;
				var components = GetPosixComponents(path);
				for (var index = 0; index < components.Length - 1; index++)
				{
					currentPath = Path.Combine(currentPath, components[index]);
					if (!observed.TryGetValue(currentPath, out var expected))
					{
						current.Dispose();
						return null;
					}
					var next = OpenDirectoryAtPosix(GetFileDescriptor(current), components[index], PosixDirectoryOpenFlags());
					if (next.IsInvalid)
					{
						var error = Marshal.GetLastWin32Error();
						next.Dispose();
						throw new IOException("An observed fixture parent is missing or changed.", new Win32Exception(error));
					}
					if (!GetEntryIdentity(GetFileDescriptor(next)).Equals(expected))
					{
						next.Dispose();
						throw new IOException("An observed fixture parent changed identity.");
					}
					current.Dispose();
					current = next;
				}
				return current;
			}
			catch { current.Dispose(); throw; }
		}

		public void WriteAllBytes(string relativePath, byte[] contents)
		{
			if (contents == null)
				throw new ArgumentNullException(nameof(contents));
			var path = GetPath(relativePath);
			if (_ownedEntries.ContainsKey(path))
				throw new IOException("Synthetic setup requires a fresh, never-owned file.");
			if (!IsWindows)
			{
				WriteAllBytesPosix(path, contents);
				return;
			}
			if (ValidatePath(path, false).HasValue)
				throw new IOException("Synthetic setup requires a fresh file.");
			var parents = PinWindowsOwnedParent(path);
			try
			{
				using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					ObserveOpenedFile(relativePath, stream.SafeFileHandle);
					stream.Write(contents, 0, contents.Length);
				}
			}
			finally { CloseHandles(parents); }
		}

		public void CreateDirectory(string relativePath)
		{
			var path = GetPath(relativePath);
			if (!IsWindows)
			{
				CreateDirectoryPosix(path);
				return;
			}
			foreach (var descendant in Descendants(path))
			{
				var attributes = ValidatePath(descendant, false);
				if (!attributes.HasValue)
				{
					if (_ownedEntries.ContainsKey(descendant))
						throw new IOException("An owned fixture directory disappeared without verified deletion.");
					var parents = PinWindowsOwnedParent(descendant);
					try
					{
						CreateFreshDirectory(descendant);
						using (var created = OpenWindowsEntry(descendant, false))
						{
							var identity = GetWindowsIdentity(created);
							if (identity.FileType != PosixDirectoryType())
								throw new IOException("The newly created fixture directory changed kind.");
							_ownedEntries.Add(descendant, identity);
						}
					}
					finally { CloseHandles(parents); }
				}
				else if ((attributes.Value & FileAttributes.Directory) == 0)
					throw new IOException("An owned file obstructs directory preparation.");
			}
		}

		public FileStream OpenExclusive(string relativePath)
		{
			var path = GetPath(relativePath);
			if (!IsWindows)
			{
				var posixStream = OpenExclusivePosix(path);
				_exclusiveHandles.Add(posixStream);
				return posixStream;
			}
			var parents = PinWindowsOwnedParent(path);
			try
			{
				RequireKind(path, false);
				var descriptor = OpenDirectoryWindows(path, 0xC0000000, FileShare.None,
					IntPtr.Zero, FileMode.Open, 0x00200000, IntPtr.Zero);
				if (descriptor.IsInvalid)
				{
					var error = Marshal.GetLastWin32Error();
					descriptor.Dispose();
					throw new IOException("Could not exclusively open the owned fixture file without following links.", new Win32Exception(error));
				}
				FileStream stream;
				try { stream = new FileStream(descriptor, FileAccess.ReadWrite); }
				catch { descriptor.Dispose(); throw; }
				try
				{
					if (!GetWindowsIdentity(stream.SafeFileHandle).Equals(GetOwnedEntryIdentity(path)))
						throw new IOException("The owned fixture file changed identity.");
					_exclusiveHandles.Add(stream);
					return stream;
				}
				catch { stream.Dispose(); throw; }
			}
			finally { CloseHandles(parents); }
		}

		public void DeleteFile(string relativePath)
		{
			var path = GetPath(relativePath);
			RequireClosedHandles();
			if (!IsWindows)
			{
				DeletePosixEntry(path, false);
				return;
			}
			DeleteWindowsEntry(path, false);
		}

		public void DeleteDirectory(string relativePath)
		{
			var path = GetPath(relativePath);
			RequireClosedHandles();
			if (!IsWindows)
			{
				DeletePosixEntry(path, true);
				return;
			}
			DeleteWindowsEntry(path, true);
		}

		public void Dispose()
		{
			if (_disposed)
				return;
			if (!IsWindows)
			{
				DisposePosix();
				return;
			}

			try
			{
				RequireClosedHandles();
				ValidateRoot();
				var entries = new List<KeyValuePair<string, bool>>();
				CollectOwnedEntries(_rootPath, entries);
				foreach (var entry in entries)
				{
					DeleteWindowsEntry(entry.Key, entry.Value);
				}
				ValidateRoot();
				if (Directory.GetFileSystemEntries(_rootPath).Length != 0)
					throw new IOException("The fixture child changed during cleanup.");
				DeleteHeldWindowsEntry(_ownership);
			}
			finally
			{
				_ownership.Dispose();
				CloseHandles(_windowsAncestry);
				_disposed = true;
			}
		}

		private void WriteAllBytesPosix(string path, byte[] contents)
		{
			using (var parent = OpenPosixParent(path))
			{
				var components = GetPosixComponents(path);
				var name = components[components.Length - 1];
				var descriptor = OpenAtPosix(GetFileDescriptor(parent), name, PosixCreateExclusiveFileFlags(), 0x180);
				if (descriptor.IsInvalid)
					throw new IOException("Synthetic setup requires a fresh file.", new Win32Exception(Marshal.GetLastWin32Error()));
				using (descriptor)
				{
					var identity = GetEntryIdentity(GetFileDescriptor(descriptor));
					_ownedEntries[path] = identity;
					using (var stream = new FileStream(descriptor, FileAccess.Write))
						stream.Write(contents, 0, contents.Length);
				}
			}
		}

		private void CreateDirectoryPosix(string path)
		{
			var current = DuplicateDirectory(_ownership);
			try
			{
				var components = GetPosixComponents(path);
				var currentPath = _rootPath;
				for (var index = 0; index < components.Length; index++)
				{
					var name = components[index];
					currentPath = Path.Combine(currentPath, name);
					var wasOwned = _ownedEntries.TryGetValue(currentPath, out var expected);
					if (!wasOwned && CreateDirectoryAtPosix(GetFileDescriptor(current), name, PosixDirectoryMode) != 0)
						throw new IOException("An expected fresh fixture directory already exists or could not be created.", new Win32Exception(Marshal.GetLastWin32Error()));

					var child = OpenDirectoryAtPosix(GetFileDescriptor(current), name, PosixDirectoryOpenFlags());
					if (child.IsInvalid)
					{
						var error = new Win32Exception(Marshal.GetLastWin32Error());
						child.Dispose();
						throw new IOException("Could not open the newly created fixture directory without following links.", error);
					}
					var identity = GetEntryIdentity(GetFileDescriptor(child));
					if (wasOwned && !identity.Equals(expected))
					{
						child.Dispose();
						throw new IOException("An owned fixture directory changed identity.");
					}
					_ownedEntries[currentPath] = identity;
					// Replace the traversal handle only after opening the next component.
					if (index < components.Length - 1)
					{
						var next = child;
						current.Dispose();
						current = next;
					}
					else
						child.Dispose();
				}
			}
			finally
			{
				current.Dispose();
			}
		}

		private FileStream OpenExclusivePosix(string path)
		{
			var expected = GetOwnedEntryIdentity(path);
			using (var parent = OpenPosixParent(path))
			{
				var components = GetPosixComponents(path);
				var descriptor = OpenAtPosix(GetFileDescriptor(parent), components[components.Length - 1], PosixReadWriteNoFollowFlags(), 0);
				if (descriptor.IsInvalid)
					throw new IOException("The owned fixture file is missing or changed kind.", new Win32Exception(Marshal.GetLastWin32Error()));
				var identity = GetEntryIdentity(GetFileDescriptor(descriptor));
				if (!identity.Equals(expected) || identity.FileType != PosixRegularFileType())
				{
					descriptor.Dispose();
					throw new IOException("The owned fixture file changed identity or kind.");
				}
				try
				{
					return new FileStream(descriptor, FileAccess.ReadWrite);
				}
				catch
				{
					descriptor.Dispose();
					throw;
				}
			}
		}

		private void DeletePosixEntry(string path, bool directory)
		{
			var expected = GetOwnedEntryIdentity(path);
			using (var parent = OpenPosixParent(path))
			{
				var components = GetPosixComponents(path);
				var name = components[components.Length - 1];
				SafeFileHandle entry;
				if (directory)
					entry = OpenDirectoryAtPosix(GetFileDescriptor(parent), name, PosixDirectoryOpenFlags());
				else
					entry = OpenAtPosix(GetFileDescriptor(parent), name, PosixReadOnlyNoFollowFlags(), 0);
				if (entry.IsInvalid)
					throw new IOException("The owned fixture entry is missing, linked, or changed kind.", new Win32Exception(Marshal.GetLastWin32Error()));
				using (entry)
				{
					var identity = GetEntryIdentity(GetFileDescriptor(entry));
					if (!identity.Equals(expected) || identity.FileType != (directory ? PosixDirectoryType() : PosixRegularFileType()))
						throw new IOException("The owned fixture entry changed identity or kind.");
					RequireSamePosixEntry(GetFileDescriptor(parent), name, identity);
				}

				if (UnlinkAtPosix(GetFileDescriptor(parent), name, directory ? PosixRemoveDirectoryFlag() : 0) != 0)
					throw new IOException("Could not remove the owned fixture entry.", new Win32Exception(Marshal.GetLastWin32Error()));
				_ownedEntries.Remove(path);
			}
		}

		private void DisposePosix()
		{
			RequireClosedHandles();
			ValidateRoot();
			RemoveOwnedPosixEntries(_ownership, _rootPath);
			if (EnumeratePosixNames(GetFileDescriptor(_ownership)).Count != 0)
				throw new IOException("The fixture child changed during cleanup.");
			RequireSamePosixDirectory(GetFileDescriptor(_parentOwnership), _rootName, _ownership);
			if (UnlinkAtPosix(GetFileDescriptor(_parentOwnership), _rootName, PosixRemoveDirectoryFlag()) != 0)
				throw new IOException("Could not remove the owned fixture child.", new Win32Exception(Marshal.GetLastWin32Error()));

			_ownership.Dispose();
			_parentOwnership.Dispose();
			CloseHandles(_posixAncestry);
			_disposed = true;
		}

		private void RemoveOwnedPosixEntries(SafeFileHandle directory, string directoryPath)
		{
			var directoryDescriptor = GetFileDescriptor(directory);
			foreach (var name in EnumeratePosixNames(directoryDescriptor))
			{
				if (name == "." || name == ".." ||
					!Regex.IsMatch(name, @"\A[A-Za-z0-9_-]+(?:[. ][A-Za-z0-9_-]+)*\z") ||
					Regex.IsMatch(name, @"\A(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|\z)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
					throw new IOException("Cleanup encountered an entry outside the synthetic fixture naming rules.");

				var entryPath = Path.Combine(directoryPath, name);
				if (!_reservations.Contains(entryPath) || !_ownedEntries.TryGetValue(entryPath, out var expected))
					throw new IOException("An unreserved entry is not owned by this fixture.");

				var child = OpenDirectoryAtPosix(directoryDescriptor, name, PosixDirectoryOpenFlags());
				if (!child.IsInvalid)
				{
					using (child)
					{
						var identity = GetEntryIdentity(GetFileDescriptor(child));
						if (!identity.Equals(expected) || identity.FileType != PosixDirectoryType())
							throw new IOException("An owned fixture directory changed identity or kind.");
						RemoveOwnedPosixEntries(child, entryPath);
					}
					RequireSamePosixEntry(directoryDescriptor, name, expected);
					if (UnlinkAtPosix(directoryDescriptor, name, PosixRemoveDirectoryFlag()) != 0)
						throw new IOException("Could not remove an owned fixture directory.", new Win32Exception(Marshal.GetLastWin32Error()));
					_ownedEntries.Remove(entryPath);
				}
				else
				{
					var file = OpenAtPosix(directoryDescriptor, name, PosixReadOnlyNoFollowFlags(), 0);
					if (file.IsInvalid)
						throw new IOException("A linked or changed fixture entry cannot be adopted.", new Win32Exception(Marshal.GetLastWin32Error()));
					using (file)
					{
						var identity = GetEntryIdentity(GetFileDescriptor(file));
						if (!identity.Equals(expected) || identity.FileType != PosixRegularFileType())
							throw new IOException("An owned fixture file changed identity or kind.");
						RequireSamePosixEntry(directoryDescriptor, name, expected);
					}
					if (UnlinkAtPosix(directoryDescriptor, name, 0) != 0)
						throw new IOException("Could not remove an owned fixture file.", new Win32Exception(Marshal.GetLastWin32Error()));
					_ownedEntries.Remove(entryPath);
				}
			}
		}

		private void ReservePosixPath(string path)
		{
			ValidateRoot();
			var components = GetPosixComponents(path);
			var current = DuplicateDirectory(_ownership);
			try
			{
				var currentPath = _rootPath;
				var reachable = true;
				foreach (var component in components)
				{
					currentPath = Path.Combine(currentPath, component);
					if (!reachable)
						continue;

					var identity = ReadPosixEntryIdentity(GetFileDescriptor(current), component);
					if (!identity.HasValue)
					{
						reachable = false;
						continue;
					}

					if (!_ownedEntries.TryGetValue(currentPath, out var owned) ||
						!owned.Equals(identity.Value))
						throw new IOException("A linked or pre-existing unreserved entry cannot be adopted.");

					if (currentPath != path)
					{
						if (identity.Value.FileType != PosixDirectoryType())
						{
							reachable = false;
							continue;
						}
						var child = OpenDirectoryAtPosix(GetFileDescriptor(current), component, PosixDirectoryOpenFlags());
						if (child.IsInvalid)
							throw new IOException("An owned fixture directory changed or cannot be opened.", new Win32Exception(Marshal.GetLastWin32Error()));
						var openedIdentity = GetEntryIdentity(GetFileDescriptor(child));
						if (!openedIdentity.Equals(owned))
						{
							child.Dispose();
							throw new IOException("An owned fixture directory changed identity.");
						}
						// The traversal is intentionally rooted in the held fixture descriptor.
						var previous = current;
						current = child;
						previous.Dispose();
					}
				}
			}
			finally
			{
				current.Dispose();
			}

			foreach (var descendant in Descendants(path))
				_reservations.Add(descendant);
		}

		private SafeFileHandle OpenPosixParent(string path)
		{
			var components = GetPosixComponents(path);
			if (components.Length == 0)
				throw new IOException("Only fixture descendants may be opened.");
			var current = DuplicateDirectory(_ownership);
			try
			{
				var currentPath = _rootPath;
				for (var index = 0; index < components.Length - 1; index++)
				{
					currentPath = Path.Combine(currentPath, components[index]);
					var expected = GetOwnedEntryIdentity(currentPath);
					var child = OpenDirectoryAtPosix(GetFileDescriptor(current), components[index], PosixDirectoryOpenFlags());
					if (child.IsInvalid)
						throw new IOException("An owned fixture parent is missing, linked, or changed kind.", new Win32Exception(Marshal.GetLastWin32Error()));
					var identity = GetEntryIdentity(GetFileDescriptor(child));
					if (!identity.Equals(expected))
					{
						child.Dispose();
						throw new IOException("An owned fixture parent changed identity.");
					}
					var previous = current;
					current = child;
					previous.Dispose();
				}
				return current;
			}
			catch
			{
				current.Dispose();
				throw;
			}
		}

		private EntryIdentity GetOwnedEntryIdentity(string path)
		{
			if (!_ownedEntries.TryGetValue(path, out var identity))
				throw new IOException("The fixture entry has not been created and owned by this fixture.");
			return identity;
		}

		private static SafeFileHandle DuplicateDirectory(SafeFileHandle directory)
		{
			var descriptor = DuplicatePosix(GetFileDescriptor(directory));
			if (descriptor < 0)
				throw new IOException("Could not duplicate an owned fixture directory handle.", new Win32Exception(Marshal.GetLastWin32Error()));
			return new SafeFileHandle(new IntPtr(descriptor), true);
		}

		private string[] GetPosixComponents(string path)
		{
			if (!path.StartsWith(_rootPrefix, StringComparison.Ordinal) || path.Length == _rootPrefix.Length)
				throw new IOException("Only descendants of this fixture child are permitted.");
			return path.Substring(_rootPrefix.Length).Split(Path.DirectorySeparatorChar);
		}

		private static EntryIdentity GetEntryIdentity(int descriptor)
		{
			RequireSupportedPosixAbi();
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				MacStat value;
				if ((RuntimeInformation.ProcessArchitecture == Architecture.X64
					? FStatMacPosix(descriptor, out value)
					: FStatMacArm64Posix(descriptor, out value)) != 0)
					throw new IOException("Could not inspect an owned fixture descriptor.", new Win32Exception(Marshal.GetLastWin32Error()));
				return new EntryIdentity(unchecked((uint)value.Device), value.Inode, (uint)value.Mode & PosixFileTypeMask());
			}

			if (string.Equals(RuntimeInformation.ProcessArchitecture.ToString(), "Arm64", StringComparison.Ordinal))
			{
				LinuxArm64Stat arm64Value;
				if (FStatLinuxArm64Posix(descriptor, out arm64Value) != 0)
					throw new IOException("Could not inspect an owned fixture descriptor.", new Win32Exception(Marshal.GetLastWin32Error()));
				return new EntryIdentity(arm64Value.Device, arm64Value.Inode, arm64Value.Mode & PosixFileTypeMask());
			}

			LinuxStat linuxValue;
			if (FStatLinuxPosix(descriptor, out linuxValue) != 0)
				throw new IOException("Could not inspect an owned fixture descriptor.", new Win32Exception(Marshal.GetLastWin32Error()));
			return new EntryIdentity(linuxValue.Device, linuxValue.Inode, linuxValue.Mode & PosixFileTypeMask());
		}

		private static EntryIdentity? ReadPosixEntryIdentity(int directory, string name)
		{
			RequireSupportedPosixAbi();
			var entry = OpenAtPosix(directory, name, PosixReadOnlyNoFollowFlags(), 0);
			if (entry.IsInvalid)
			{
				var error = Marshal.GetLastWin32Error();
				if (error == 2)
					return null;
				throw new IOException("Could not open a fixture entry without following links.", new Win32Exception(error));
			}
			using (entry)
				return GetEntryIdentity(GetFileDescriptor(entry));
		}

		private static void RequireSamePosixEntry(int parent, string name, EntryIdentity expected)
		{
			var actual = ReadPosixEntryIdentity(parent, name);
			if (!actual.HasValue || !actual.Value.Equals(expected))
				throw new IOException("The fixture entry changed identity before removal.");
		}

		private static void RequireSamePosixDirectory(int parent, string name, SafeFileHandle openedDirectory)
		{
			var opened = GetEntryIdentity(GetFileDescriptor(openedDirectory));
			if (opened.FileType != PosixDirectoryType())
				throw new IOException("The held fixture root is not an ordinary directory.");
			RequireSamePosixEntry(parent, name, opened);
		}

		private static int PosixCreateExclusiveFileFlags()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
				? 0x01000000 | 0x00000100 | 0x00000200 | 0x00000800 | 0x00000001
				: 0x00080000 | 0x00020000 | 0x00000040 | 0x00000080 | 0x00000001;
		}

		private static int PosixReadWriteNoFollowFlags()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
				? 0x01000000 | 0x00000100 | 0x00000004 | 0x00000002
				: 0x00080000 | 0x00020000 | 0x00000800 | 0x00000002;
		}

		private static int PosixReadOnlyNoFollowFlags()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
				? 0x01000000 | 0x00000100 | 0x00000004
				: 0x00080000 | 0x00020000 | 0x00000800;
		}

		private static void RequireSupportedPosixAbi()
		{
			if (!IsSupportedPosixPlatform() || IntPtr.Size != 8 ||
				(RuntimeInformation.ProcessArchitecture != Architecture.X64 && RuntimeInformation.ProcessArchitecture != Architecture.Arm64))
				throw new PlatformNotSupportedException("The POSIX fixture supports Linux and macOS x64/Arm64 ABIs.");
		}

		private static uint PosixFileTypeMask()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0xF000U : 0xF000U;
		}

		private static uint PosixDirectoryType()
		{
			return 0x4000U;
		}

		private static uint PosixRegularFileType()
		{
			return 0x8000U;
		}

		private static SafeFileHandle OpenAtPosix(int directory, string name, int flags, uint mode)
		{
			return OpenAtPosixNative(directory, name, flags, mode);
		}

		private struct EntryIdentity : IEquatable<EntryIdentity>
		{
			public EntryIdentity(ulong device, ulong inode, uint fileType)
			{
				Device = device;
				Inode = inode;
				FileType = fileType;
			}

			public ulong Device;
			public ulong Inode;
			public uint FileType;

			public bool Equals(EntryIdentity other)
			{
				return Device == other.Device && Inode == other.Inode && FileType == other.FileType;
			}
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct LinuxStat
		{
			public ulong Device;
			public ulong Inode;
			public ulong LinkCount;
			public uint Mode;
			public uint UserId;
			public uint GroupId;
			public int Padding;
			public ulong SpecialDevice;
			public long Size;
			public long BlockSize;
			public long Blocks;
			public long AccessSeconds;
			public long AccessNanoseconds;
			public long ModifySeconds;
			public long ModifyNanoseconds;
			public long ChangeSeconds;
			public long ChangeNanoseconds;
			public long Reserved0;
			public long Reserved1;
			public long Reserved2;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct LinuxArm64Stat
		{
			public ulong Device;
			public ulong Inode;
			public uint Mode;
			public uint LinkCount;
			public uint UserId;
			public uint GroupId;
			public ulong SpecialDevice;
			public long Size;
			public int BlockSize;
			public int Padding;
			public long Blocks;
			public long AccessSeconds;
			public long AccessNanoseconds;
			public long ModifySeconds;
			public long ModifyNanoseconds;
			public long ChangeSeconds;
			public long ChangeNanoseconds;
			public long Reserved0;
			public long Reserved1;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct MacStat
		{
			public int Device;
			public ushort Mode;
			public ushort LinkCount;
			public ulong Inode;
			public uint UserId;
			public uint GroupId;
			public int SpecialDevice;
			public long AccessSeconds;
			public long AccessNanoseconds;
			public long ModifySeconds;
			public long ModifyNanoseconds;
			public long ChangeSeconds;
			public long ChangeNanoseconds;
			public long BirthSeconds;
			public long BirthNanoseconds;
			public long Size;
			public long Blocks;
			public int BlockSize;
			public uint Flags;
			public uint Generation;
			public int Spare;
			public long Spare0;
			public long Spare1;
		}

		private static List<string> EnumeratePosixNames(int directoryDescriptor)
		{
			RequireSupportedPosixAbi();
			// dup shares directory offsets. openat(".") creates a fresh open description.
			var opened = OpenDirectoryAtPosix(directoryDescriptor, ".", PosixDirectoryOpenFlags());
			if (opened.IsInvalid)
			{
				var error = Marshal.GetLastWin32Error();
				opened.Dispose();
				throw new IOException("Could not open an independent fixture directory stream.", new Win32Exception(error));
			}
			var directory = OpenDirectoryStreamPosix(GetFileDescriptor(opened));
			if (directory == IntPtr.Zero)
			{
				var error = Marshal.GetLastWin32Error();
				opened.Dispose();
				throw new IOException("Could not enumerate the owned fixture directory handle.", new Win32Exception(error));
			}
			opened.SetHandleAsInvalid(); // fdopendir now owns the fd; only closedir may close it.
			opened.Dispose();
			var completed = false;
			try
			{
				var names = new List<string>();
				var isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
				var errno = isMacOS ? ErrorLocationMacPosix() : ErrorLocationLinuxPosix();
				var nameOffset = isMacOS ? 21 : 19;
				while (true)
				{
					Marshal.WriteInt32(errno, 0);
					var entry = isMacOS && RuntimeInformation.ProcessArchitecture == Architecture.X64
						? ReadDirectoryEntryMacPosix(directory) : ReadDirectoryEntryPosix(directory);
					if (entry == IntPtr.Zero)
					{
						var error = Marshal.ReadInt32(errno);
						if (error != 0)
							throw new IOException("Could not read the fixture directory stream.", new Win32Exception(error));
						break;
					}
					var recordLength = unchecked((ushort)Marshal.ReadInt16(entry, 16));
					var capacity = Math.Min(recordLength - nameOffset, isMacOS ? 1024 : 256);
					if (capacity <= 0)
						throw new IOException("Invalid fixture directory entry length.");
					var bytes = new List<byte>();
					for (var index = 0; index < capacity; index++)
					{
						var value = Marshal.ReadByte(entry, nameOffset + index);
						if (value == 0)
							break;
						bytes.Add(value);
					}
					if (bytes.Count == 0 || bytes.Count == capacity ||
						(isMacOS && bytes.Count != unchecked((ushort)Marshal.ReadInt16(entry, 18))))
						throw new IOException("Could not read a bounded fixture directory entry name.");
					var name = new UTF8Encoding(false, true).GetString(bytes.ToArray());
					if (name != "." && name != "..")
						names.Add(name);
				}
				completed = true;
				return names;
			}
			finally
			{
				if (CloseDirectoryStreamPosix(directory) != 0 && completed)
					throw new IOException("Could not close the fixture directory stream.", new Win32Exception(Marshal.GetLastWin32Error()));
			}
		}

		private static int GetFileDescriptor(SafeFileHandle handle)
		{
			return handle.DangerousGetHandle().ToInt32();
		}

		private static int PosixDirectoryOpenFlags()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
				? 0x00100000 | 0x00000100 | 0x01000000
				: 0x00010000 | 0x00020000 | 0x00080000;
		}

		private static int PosixRemoveDirectoryFlag()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? 0x80 : 0x200;
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
					using (var entry = OpenWindowsEntry(descendant, false))
						if (!GetWindowsIdentity(entry).Equals(GetOwnedEntryIdentity(descendant)))
							throw new IOException("An owned fixture entry changed identity or kind.");
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
			if (!path.StartsWith(_rootPrefix, PathComparison) || path.Length == _rootPrefix.Length)
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
			if (IsWindows)
			{
				using (var root = OpenWindowsEntry(_rootPath, false))
					if (!GetWindowsIdentity(root).Equals(GetOwnedEntryIdentity(_rootPath)))
						throw new IOException("The fixture root changed identity.");
			}
			else
			{
				var components = Path.GetDirectoryName(_rootPath).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
				for (var index = 0; index < components.Length; index++)
					RequireSamePosixDirectory(GetFileDescriptor(_posixAncestry[index]), components[index], _posixAncestry[index + 1]);
				RequireSamePosixDirectory(GetFileDescriptor(_parentOwnership), _rootName, _ownership);
				if (!GetEntryIdentity(GetFileDescriptor(_ownership)).Equals(GetOwnedEntryIdentity(_rootPath)))
					throw new IOException("The held fixture root changed identity.");
			}
		}

		private static void PrepareParentDirectory(string path)
		{
			// Hold every no-follow ancestor while creating the next component. In
			// particular, never use recursive CreateDirectory through unchecked paths.
			var handles = IsWindows ? PinWindowsAncestry(path, true) : PinPosixAncestry(path, true);
			CloseHandles(handles);
		}

		private static List<SafeFileHandle> PinWindowsAncestry(string path, bool prepare)
		{
			var ancestry = new Stack<string>();
			for (var current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
				ancestry.Push(current);
			var handles = new List<SafeFileHandle>();
			try
			{
				foreach (var ancestor in ancestry)
				{
					var handle = OpenDirectoryWindows(ancestor, 0, FileShare.Read | FileShare.Write,
						IntPtr.Zero, FileMode.Open, 0x02000000 | 0x00200000, IntPtr.Zero);
					if (handle.IsInvalid)
					{
						var error = Marshal.GetLastWin32Error();
						handle.Dispose();
						if (!prepare || (error != 2 && error != 3))
							throw new IOException("Could not pin ordinary fixture ancestry.", new Win32Exception(error));
						if (!CreateDirectoryWindows(ancestor, IntPtr.Zero))
						{
							error = Marshal.GetLastWin32Error();
							if (error != 183) // A concurrent first preparation must still verify the winner.
								throw new IOException("Could not prepare fixture ancestry.", new Win32Exception(error));
						}
						handle = OpenWindowsEntry(ancestor, false, true);
					}
					handles.Add(handle);
					if (GetWindowsIdentity(handle).FileType != PosixDirectoryType())
						throw new IOException("Fixture ancestry must contain only ordinary directories.");
				}
				return handles;
			}
			catch { CloseHandles(handles); throw; }
		}

		private static List<SafeFileHandle> PinPosixAncestry(string path, bool prepare)
		{
			RequireSupportedPosixAbi();
			var handles = new List<SafeFileHandle>();
			try
			{
				var root = OpenDirectoryPosix("/", PosixDirectoryOpenFlags());
				handles.Add(root);
				if (root.IsInvalid)
					throw new IOException("Could not pin the filesystem root.", new Win32Exception(Marshal.GetLastWin32Error()));
				foreach (var name in Path.GetFullPath(path).Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
				{
					var parent = GetFileDescriptor(handles[handles.Count - 1]);
					var child = OpenDirectoryAtPosix(parent, name, PosixDirectoryOpenFlags());
					if (child.IsInvalid)
					{
						var error = Marshal.GetLastWin32Error();
						child.Dispose();
						if (!prepare || error != 2)
							throw new IOException("Could not pin ordinary fixture ancestry.", new Win32Exception(error));
						if (CreateDirectoryAtPosix(parent, name, PosixDirectoryMode) != 0)
						{
							error = Marshal.GetLastWin32Error();
							if (error != 17)
								throw new IOException("Could not prepare fixture ancestry.", new Win32Exception(error));
						}
						child = OpenDirectoryAtPosix(parent, name, PosixDirectoryOpenFlags());
					}
					handles.Add(child);
					if (child.IsInvalid || GetEntryIdentity(GetFileDescriptor(child)).FileType != PosixDirectoryType())
						throw new IOException("Fixture ancestry must contain only ordinary directories.");
					RequireSamePosixDirectory(parent, name, child);
				}
				return handles;
			}
			catch { CloseHandles(handles); throw; }
		}

		private static void CloseHandles(List<SafeFileHandle> handles)
		{
			if (handles == null)
				return;
			for (var index = handles.Count - 1; index >= 0; index--)
				handles[index].Dispose();
		}

		private List<SafeFileHandle> PinWindowsOwnedParent(string path)
		{
			return PinWindowsOwnedParent(path, _ownedEntries, false);
		}

		private List<SafeFileHandle> PinWindowsOwnedParent(string path, Dictionary<string, EntryIdentity> known, bool allowMissing)
		{
			ValidateRoot();
			var handles = new List<SafeFileHandle>();
			try
			{
				foreach (var ancestor in Descendants(path))
				{
					if (PathComparer.Equals(ancestor, path))
						break;
					if (!known.TryGetValue(ancestor, out var expected))
					{
						if (!allowMissing)
							throw new IOException("The fixture parent has not been observed as owned.");
						CloseHandles(handles);
						return null;
					}
					var handle = OpenWindowsEntry(ancestor, false, true);
					handles.Add(handle);
					var actual = GetWindowsIdentity(handle);
					if (!actual.Equals(expected) || actual.FileType != PosixDirectoryType())
						throw new IOException("An owned fixture parent changed identity or kind.");
				}
				return handles;
			}
			catch { CloseHandles(handles); throw; }
		}

		private static SafeFileHandle OpenWindowsEntry(string path, bool delete, bool pin = false)
		{
			var handle = OpenDirectoryWindows(path, delete ? 0x00010080U : 0U,
				FileShare.Read | FileShare.Write | (pin || delete ? 0 : FileShare.Delete),
				IntPtr.Zero, FileMode.Open, 0x02000000 | 0x00200000, IntPtr.Zero);
			if (handle.IsInvalid)
			{
				var error = Marshal.GetLastWin32Error();
				handle.Dispose();
				throw new IOException("Could not open a fixture entry without following reparse points.", new Win32Exception(error));
			}
			return handle;
		}

		private static EntryIdentity? ReadWindowsEntryIdentity(string path)
		{
			using (var handle = OpenDirectoryWindows(path, 0, FileShare.Read | FileShare.Write | FileShare.Delete,
				IntPtr.Zero, FileMode.Open, 0x02000000 | 0x00200000, IntPtr.Zero))
			{
				if (!handle.IsInvalid)
					return GetWindowsIdentity(handle);
				var error = Marshal.GetLastWin32Error();
				if (error == 2 || error == 3)
					return null;
				throw new IOException("Could not inspect a reserved fixture entry.", new Win32Exception(error));
			}
		}

		private static EntryIdentity GetWindowsIdentity(SafeFileHandle handle)
		{
			WindowsFileInformation value;
			if (!GetFileInformationWindows(handle, out value))
				throw new IOException("Could not inspect a fixture handle.", new Win32Exception(Marshal.GetLastWin32Error()));
			if ((value.Attributes & FileAttributes.ReparsePoint) != 0 || GetFileTypeWindows(handle) != 1)
				throw new IOException("A linked or non-disk fixture entry cannot be adopted.");
			return new EntryIdentity(value.Volume, ((ulong)value.IndexHigh << 32) | value.IndexLow,
				(value.Attributes & FileAttributes.Directory) != 0 ? PosixDirectoryType() : PosixRegularFileType());
		}

		private void DeleteWindowsEntry(string path, bool directory)
		{
			var parents = PinWindowsOwnedParent(path);
			try
			{
				using (var handle = OpenWindowsEntry(path, true))
				{
					var identity = GetWindowsIdentity(handle);
					if (!identity.Equals(GetOwnedEntryIdentity(path)) ||
						identity.FileType != (directory ? PosixDirectoryType() : PosixRegularFileType()))
						throw new IOException("An owned fixture entry changed identity or kind before removal.");
					DeleteHeldWindowsEntry(handle);
				}
				_ownedEntries.Remove(path);
			}
			finally { CloseHandles(parents); }
		}

		private static void DeleteHeldWindowsEntry(SafeFileHandle handle)
		{
			var delete = new WindowsFileDisposition { Delete = true };
			if (!SetFileDispositionWindows(handle, 4, ref delete, (uint)Marshal.SizeOf(typeof(WindowsFileDisposition))))
				throw new IOException("Could not remove the verified owned fixture entry.", new Win32Exception(Marshal.GetLastWin32Error()));
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct WindowsFileInformation
		{
			public FileAttributes Attributes;
			public uint CreationLow, CreationHigh, AccessLow, AccessHigh, WriteLow, WriteHigh;
			public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
		}

		[StructLayout(LayoutKind.Sequential)]
		private struct WindowsFileDisposition
		{
			[MarshalAs(UnmanagedType.U1)] public bool Delete;
		}

		[DllImport("kernel32.dll", EntryPoint = "GetFileInformationByHandle", ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetFileInformationWindows(SafeFileHandle handle, out WindowsFileInformation information);

		[DllImport("kernel32.dll", EntryPoint = "GetFileType", ExactSpelling = true, SetLastError = true)]
		private static extern uint GetFileTypeWindows(SafeFileHandle handle);

		[DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		private static extern uint GetFinalPathNameWindows(SafeFileHandle handle, StringBuilder path, uint length, uint flags);

		[DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool SetFileDispositionWindows(SafeFileHandle handle, int informationClass, ref WindowsFileDisposition information, uint size);

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

		private static string GetParentPath()
		{
			if (IsWindows)
				return ParentPath;

			// macOS commonly exposes its temporary directory through /var, which is a symlink.
			// Resolve only this OS-provided starting location, then reject links in the actual
			// ancestry and throughout the uniquely owned fixture child.
			var temporaryPath = Path.GetFullPath(Path.GetTempPath());
			var resolvedPath = ResolvePosixPath(temporaryPath);
			if (string.IsNullOrEmpty(resolvedPath))
				throw new IOException("Could not resolve the OS temporary directory.");
			return resolvedPath;
		}

		private static bool IsSupportedPosixPlatform()
		{
			return RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
				RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
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
			var created = IsWindows
				? CreateDirectoryWindows(path, IntPtr.Zero)
				: CreateDirectoryPosix(path, PosixDirectoryMode) == 0;
			if (!created)
				throw new IOException("Could not exclusively create the reserved fixture directory.", new Win32Exception(Marshal.GetLastWin32Error()));
		}

		private static SafeFileHandle OpenDirectory(string path)
		{
			if (IsWindows)
				return OpenDirectoryWindows(path, 0x00000001, FileShare.Read | FileShare.Write,
					IntPtr.Zero, FileMode.Open, 0x02000000 | 0x00200000, IntPtr.Zero);

			return OpenDirectoryPosix(path, PosixDirectoryOpenFlags());
		}

		private static string ResolvePosixPath(string path)
		{
			var resolved = RealPathPosix(path, IntPtr.Zero);
			if (resolved == IntPtr.Zero)
				throw new IOException("Could not resolve the OS temporary directory.", new Win32Exception(Marshal.GetLastWin32Error()));
			try
			{
				return Marshal.PtrToStringAnsi(resolved);
			}
			finally
			{
				FreePosix(resolved);
			}
		}

		[DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool CreateDirectoryWindows(string path, IntPtr securityAttributes);

		[DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
		private static extern SafeFileHandle OpenDirectoryWindows(string path, uint desiredAccess, FileShare shareMode,
			IntPtr securityAttributes, FileMode creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

		[DllImport("libc", EntryPoint = "mkdir", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern int CreateDirectoryPosix(string path, uint mode);

		[DllImport("libc", EntryPoint = "open", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern SafeFileHandle OpenDirectoryPosix(string path, int flags);

		[DllImport("libc", EntryPoint = "mkdirat", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern int CreateDirectoryAtPosix(int directory, string path, uint mode);

		[DllImport("libc", EntryPoint = "openat", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern SafeFileHandle OpenAtPosixNative(int directory, string path, int flags, uint mode);

		[DllImport("libc", EntryPoint = "openat", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern SafeFileHandle OpenDirectoryAtPosix(int directory, string path, int flags);

		[DllImport("libc", EntryPoint = "fstat", ExactSpelling = true, SetLastError = true)]
		private static extern int FStatLinuxPosix(int descriptor, out LinuxStat value);

		[DllImport("libc", EntryPoint = "fstat", ExactSpelling = true, SetLastError = true)]
		private static extern int FStatLinuxArm64Posix(int descriptor, out LinuxArm64Stat value);

		[DllImport("libc", EntryPoint = "fstat$INODE64", ExactSpelling = true, SetLastError = true)]
		private static extern int FStatMacPosix(int descriptor, out MacStat value);

		[DllImport("libc", EntryPoint = "fstat", ExactSpelling = true, SetLastError = true)]
		private static extern int FStatMacArm64Posix(int descriptor, out MacStat value);

		[DllImport("libc", EntryPoint = "dup", ExactSpelling = true, SetLastError = true)]
		private static extern int DuplicatePosix(int descriptor);

		[DllImport("libc", EntryPoint = "fdopendir", ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr OpenDirectoryStreamPosix(int descriptor);

		[DllImport("libc", EntryPoint = "readdir", ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr ReadDirectoryEntryPosix(IntPtr directory);

		[DllImport("libc", EntryPoint = "readdir$INODE64", ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr ReadDirectoryEntryMacPosix(IntPtr directory);

		[DllImport("libc", EntryPoint = "__error", ExactSpelling = true)]
		private static extern IntPtr ErrorLocationMacPosix();

		[DllImport("libc", EntryPoint = "__errno_location", ExactSpelling = true)]
		private static extern IntPtr ErrorLocationLinuxPosix();

		[DllImport("libc", EntryPoint = "closedir", ExactSpelling = true, SetLastError = true)]
		private static extern int CloseDirectoryStreamPosix(IntPtr directory);

		[DllImport("libc", EntryPoint = "close", ExactSpelling = true, SetLastError = true)]
		private static extern int ClosePosix(int descriptor);

		[DllImport("libc", EntryPoint = "unlinkat", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern int UnlinkAtPosix(int directory, string path, int flags);

		[DllImport("libc", EntryPoint = "realpath", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr RealPathPosix(string path, IntPtr resolvedPath);

		[DllImport("libc", EntryPoint = "free", ExactSpelling = true)]
		private static extern void FreePosix(IntPtr pointer);
	}
}