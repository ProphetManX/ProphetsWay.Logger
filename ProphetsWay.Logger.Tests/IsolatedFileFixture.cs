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
		private readonly Dictionary<string, PosixIdentity> _posixOwnedEntries = new Dictionary<string, PosixIdentity>(PathComparer);
		private readonly List<FileStream> _exclusiveHandles = new List<FileStream>();
		private readonly SafeFileHandle _parentOwnership;
		private readonly SafeFileHandle _ownership;
		private bool _disposed;

		public IsolatedFileFixture()
		{
			if (!IsWindows && !IsSupportedPosixPlatform())
				throw new PlatformNotSupportedException("The fixture supports Windows, Linux, and macOS.");

			var parentPath = GetParentPath();
			ValidateDirectoryAncestry(parentPath);
			_rootName = "m4b1-" + Guid.NewGuid().ToString("N");
			_rootPath = Path.Combine(parentPath, _rootName);
			_rootPrefix = _rootPath + Path.DirectorySeparatorChar;
			_reservations.Add(_rootPath);
			if (IsWindows)
			{
				_parentOwnership = null;
				CreateFreshDirectory(_rootPath);
				_ownership = OpenDirectory(_rootPath);
			}
			else
			{
				_parentOwnership = OpenDirectory(parentPath);
				if (_parentOwnership.IsInvalid)
				{
					var error = new Win32Exception(Marshal.GetLastWin32Error());
					_parentOwnership.Dispose();
					throw new IOException("Could not hold the approved fixture parent.", error);
				}
				if (CreateDirectoryAtPosix(GetFileDescriptor(_parentOwnership), _rootName, PosixDirectoryMode) != 0)
				{
					var error = new Win32Exception(Marshal.GetLastWin32Error());
					_parentOwnership.Dispose();
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
				throw new IOException("Could not hold the newly owned fixture child; retained at " + _rootPath, error);
			}
			if (!IsWindows)
				_posixOwnedEntries.Add(_rootPath, GetPosixIdentity(GetFileDescriptor(_ownership)));
			try
			{
				ValidateRoot();
			}
			catch
			{
				_ownership.Dispose();
				if (_parentOwnership != null)
					_parentOwnership.Dispose();
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
			if (IsWindows)
				ValidatePath(path, true);
			else
				ReservePosixPath(path);
			return path;
		}

		public void WriteAllBytes(string relativePath, byte[] contents)
		{
			if (contents == null)
				throw new ArgumentNullException(nameof(contents));
			var path = GetPath(relativePath);
			if (!IsWindows)
			{
				WriteAllBytesPosix(path, contents);
				return;
			}
			if (ValidatePath(path, false).HasValue)
				throw new IOException("Synthetic setup requires a fresh file.");
			using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				stream.Write(contents, 0, contents.Length);
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
					CreateFreshDirectory(descendant);
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
			RequireKind(path, false);
			var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
			_exclusiveHandles.Add(stream);
			return stream;
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
			RequireKind(path, false);
			File.Delete(path);
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
			RequireKind(path, true);
			Directory.Delete(path, false);
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
					var identity = GetPosixIdentity(GetFileDescriptor(descriptor));
					_posixOwnedEntries[path] = identity;
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
					var wasOwned = _posixOwnedEntries.TryGetValue(currentPath, out var expected);
					if (!wasOwned && CreateDirectoryAtPosix(GetFileDescriptor(current), name, PosixDirectoryMode) != 0)
						throw new IOException("An expected fresh fixture directory already exists or could not be created.", new Win32Exception(Marshal.GetLastWin32Error()));

					var child = OpenDirectoryAtPosix(GetFileDescriptor(current), name, PosixDirectoryOpenFlags());
					if (child.IsInvalid)
					{
						var error = new Win32Exception(Marshal.GetLastWin32Error());
						child.Dispose();
						throw new IOException("Could not open the newly created fixture directory without following links.", error);
					}
					var identity = GetPosixIdentity(GetFileDescriptor(child));
					if (wasOwned && !identity.Equals(expected))
					{
						child.Dispose();
						throw new IOException("An owned fixture directory changed identity.");
					}
					_posixOwnedEntries[currentPath] = identity;
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
			var expected = GetOwnedPosixIdentity(path);
			using (var parent = OpenPosixParent(path))
			{
				var components = GetPosixComponents(path);
				var descriptor = OpenAtPosix(GetFileDescriptor(parent), components[components.Length - 1], PosixReadWriteNoFollowFlags(), 0);
				if (descriptor.IsInvalid)
					throw new IOException("The owned fixture file is missing or changed kind.", new Win32Exception(Marshal.GetLastWin32Error()));
				var identity = GetPosixIdentity(GetFileDescriptor(descriptor));
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
			var expected = GetOwnedPosixIdentity(path);
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
					var identity = GetPosixIdentity(GetFileDescriptor(entry));
					if (!identity.Equals(expected) || identity.FileType != (directory ? PosixDirectoryType() : PosixRegularFileType()))
						throw new IOException("The owned fixture entry changed identity or kind.");
					RequireSamePosixEntry(GetFileDescriptor(parent), name, identity);
				}

				if (UnlinkAtPosix(GetFileDescriptor(parent), name, directory ? PosixRemoveDirectoryFlag() : 0) != 0)
					throw new IOException("Could not remove the owned fixture entry.", new Win32Exception(Marshal.GetLastWin32Error()));
				_posixOwnedEntries.Remove(path);
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
				if (!_reservations.Contains(entryPath) || !_posixOwnedEntries.TryGetValue(entryPath, out var expected))
					throw new IOException("An unreserved entry is not owned by this fixture.");

				var child = OpenDirectoryAtPosix(directoryDescriptor, name, PosixDirectoryOpenFlags());
				if (!child.IsInvalid)
				{
					using (child)
					{
						var identity = GetPosixIdentity(GetFileDescriptor(child));
						if (!identity.Equals(expected) || identity.FileType != PosixDirectoryType())
							throw new IOException("An owned fixture directory changed identity or kind.");
						RemoveOwnedPosixEntries(child, entryPath);
					}
					RequireSamePosixEntry(directoryDescriptor, name, expected);
					if (UnlinkAtPosix(directoryDescriptor, name, PosixRemoveDirectoryFlag()) != 0)
						throw new IOException("Could not remove an owned fixture directory.", new Win32Exception(Marshal.GetLastWin32Error()));
					_posixOwnedEntries.Remove(entryPath);
				}
				else
				{
					var file = OpenAtPosix(directoryDescriptor, name, PosixReadOnlyNoFollowFlags(), 0);
					if (file.IsInvalid)
						throw new IOException("A linked or changed fixture entry cannot be adopted.", new Win32Exception(Marshal.GetLastWin32Error()));
					using (file)
					{
						var identity = GetPosixIdentity(GetFileDescriptor(file));
						if (!identity.Equals(expected) || identity.FileType != PosixRegularFileType())
							throw new IOException("An owned fixture file changed identity or kind.");
						RequireSamePosixEntry(directoryDescriptor, name, expected);
					}
					if (UnlinkAtPosix(directoryDescriptor, name, 0) != 0)
						throw new IOException("Could not remove an owned fixture file.", new Win32Exception(Marshal.GetLastWin32Error()));
					_posixOwnedEntries.Remove(entryPath);
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

					if (!_posixOwnedEntries.TryGetValue(currentPath, out var owned) ||
						!owned.Equals(identity.Value))
						throw new IOException("A linked or pre-existing unreserved entry cannot be adopted.");

					if (currentPath != path)
					{
						if (identity.Value.FileType != PosixDirectoryType())
							throw new IOException("An owned file obstructs fixture path preparation.");
						var child = OpenDirectoryAtPosix(GetFileDescriptor(current), component, PosixDirectoryOpenFlags());
						if (child.IsInvalid)
							throw new IOException("An owned fixture directory changed or cannot be opened.", new Win32Exception(Marshal.GetLastWin32Error()));
						var openedIdentity = GetPosixIdentity(GetFileDescriptor(child));
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
					var expected = GetOwnedPosixIdentity(currentPath);
					var child = OpenDirectoryAtPosix(GetFileDescriptor(current), components[index], PosixDirectoryOpenFlags());
					if (child.IsInvalid)
						throw new IOException("An owned fixture parent is missing, linked, or changed kind.", new Win32Exception(Marshal.GetLastWin32Error()));
					var identity = GetPosixIdentity(GetFileDescriptor(child));
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

		private PosixIdentity GetOwnedPosixIdentity(string path)
		{
			if (!_posixOwnedEntries.TryGetValue(path, out var identity))
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

		private static PosixIdentity GetPosixIdentity(int descriptor)
		{
			RequireSupportedPosixAbi();
			if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				MacStat value;
				if (FStatMacPosix(descriptor, out value) != 0)
					throw new IOException("Could not inspect an owned fixture descriptor.", new Win32Exception(Marshal.GetLastWin32Error()));
				return new PosixIdentity(unchecked((uint)value.Device), value.Inode, (uint)value.Mode & PosixFileTypeMask());
			}

			if (string.Equals(RuntimeInformation.ProcessArchitecture.ToString(), "Arm64", StringComparison.Ordinal))
			{
				LinuxArm64Stat arm64Value;
				if (FStatLinuxArm64Posix(descriptor, out arm64Value) != 0)
					throw new IOException("Could not inspect an owned fixture descriptor.", new Win32Exception(Marshal.GetLastWin32Error()));
				return new PosixIdentity(arm64Value.Device, arm64Value.Inode, arm64Value.Mode & PosixFileTypeMask());
			}

			LinuxStat linuxValue;
			if (FStatLinuxPosix(descriptor, out linuxValue) != 0)
				throw new IOException("Could not inspect an owned fixture descriptor.", new Win32Exception(Marshal.GetLastWin32Error()));
			return new PosixIdentity(linuxValue.Device, linuxValue.Inode, linuxValue.Mode & PosixFileTypeMask());
		}

		private static PosixIdentity? ReadPosixEntryIdentity(int directory, string name)
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
				return GetPosixIdentity(GetFileDescriptor(entry));
		}

		private static void RequireSamePosixEntry(int parent, string name, PosixIdentity expected)
		{
			var actual = ReadPosixEntryIdentity(parent, name);
			if (!actual.HasValue || !actual.Value.Equals(expected))
				throw new IOException("The fixture entry changed identity before removal.");
		}

		private static void RequireSamePosixDirectory(int parent, string name, SafeFileHandle openedDirectory)
		{
			var opened = GetPosixIdentity(GetFileDescriptor(openedDirectory));
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
			if (IntPtr.Size != 8 ||
				(RuntimeInformation.IsOSPlatform(OSPlatform.Linux) &&
				 !string.Equals(RuntimeInformation.ProcessArchitecture.ToString(), "X64", StringComparison.Ordinal) &&
				 !string.Equals(RuntimeInformation.ProcessArchitecture.ToString(), "Arm64", StringComparison.Ordinal)))
				throw new PlatformNotSupportedException("The POSIX fixture identity check supports Linux x64/Arm64 and 64-bit macOS ABIs.");
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

		private struct PosixIdentity : IEquatable<PosixIdentity>
		{
			public PosixIdentity(ulong device, ulong inode, uint fileType)
			{
				Device = device;
				Inode = inode;
				FileType = fileType;
			}

			public ulong Device;
			public ulong Inode;
			public uint FileType;

			public bool Equals(PosixIdentity other)
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
			var duplicate = DuplicatePosix(directoryDescriptor);
			if (duplicate < 0)
				throw new IOException("Could not duplicate the owned fixture directory handle.", new Win32Exception(Marshal.GetLastWin32Error()));
			var directory = OpenDirectoryStreamPosix(duplicate);
			if (directory == IntPtr.Zero)
			{
				var error = new Win32Exception(Marshal.GetLastWin32Error());
				ClosePosix(duplicate);
				throw new IOException("Could not enumerate the owned fixture directory handle.", error);
			}

			try
			{
				var names = new List<string>();
				RequireSupportedPosixAbi();
				// Darwin's 64-bit struct dirent has d_name at byte 21 after d_ino,
				// d_seekoff, d_reclen, d_namlen, and d_type. Linux x86-64/AArch64
				// places d_name at byte 19 after d_ino, d_off, d_reclen, and d_type.
				var isMacOS = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
				var nameOffset = isMacOS ? 21 : 19;
				var nameCapacity = isMacOS ? 1024 : 256;
				while (true)
				{
					var entry = ReadDirectoryEntryPosix(directory);
					if (entry == IntPtr.Zero)
						break;
					var bytes = new List<byte>();
					for (var index = 0; index < nameCapacity; index++)
					{
						var value = Marshal.ReadByte(entry, nameOffset + index);
						if (value == 0)
							break;
						bytes.Add(value);
					}
					if (bytes.Count == 0 || bytes.Count == nameCapacity)
						throw new IOException("Could not read a fixture directory entry name.");
					names.Add(new UTF8Encoding(false, true).GetString(bytes.ToArray()));
				}
				return names;
			}
			finally
			{
				CloseDirectoryStreamPosix(directory);
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

		[DllImport("libc", EntryPoint = "fstat", ExactSpelling = true, SetLastError = true)]
		private static extern int FStatMacPosix(int descriptor, out MacStat value);

		[DllImport("libc", EntryPoint = "dup", ExactSpelling = true, SetLastError = true)]
		private static extern int DuplicatePosix(int descriptor);

		[DllImport("libc", EntryPoint = "fdopendir", ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr OpenDirectoryStreamPosix(int descriptor);

		[DllImport("libc", EntryPoint = "readdir", ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr ReadDirectoryEntryPosix(IntPtr directory);

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