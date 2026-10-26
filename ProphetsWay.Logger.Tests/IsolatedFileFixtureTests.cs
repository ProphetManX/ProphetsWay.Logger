using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Tests
{
	public class IsolatedFileFixtureTests
	{
		[Fact]
		public void DisposeRemovesTheFreshOwnedChild()
		{
			string rootPath;
			using (var fixture = new IsolatedFileFixture())
			{
				rootPath = fixture.RootPath;
				fixture.WriteAllBytes("owned.log", new byte[] { 1, 2, 3 });
			}

			Directory.Exists(rootPath).ShouldBeFalse();
		}

		[Fact]
		public void DisposeRejectsAndPreservesAnUnreservedEntry()
		{
			var fixture = new IsolatedFileFixture();
			var rootPath = fixture.RootPath;
			var unreservedPath = Path.Combine(rootPath, "unreserved.log");
			var contents = new byte[] { 4, 5, 6 };

			try
			{
				File.WriteAllBytes(unreservedPath, contents);

				Should.Throw<IOException>(() => fixture.Dispose());

				Directory.Exists(rootPath).ShouldBeTrue();
				File.ReadAllBytes(unreservedPath).ShouldBe(contents);
			}
			finally
			{
				CleanSyntheticRemainder(fixture, rootPath, unreservedPath, contents);
			}
		}

		[Fact]
		public void DisposeRejectsAndPreservesAnUnreservedEntryNestedBelowAnOwnedDirectory()
		{
			var fixture = new IsolatedFileFixture();
			var rootPath = fixture.RootPath;
			var nestedDirectory = Path.Combine(rootPath, "owned", "nested");
			var unreservedPath = Path.Combine(nestedDirectory, "unreserved.log");
			var contents = new byte[] { 16, 17, 18 };

			try
			{
				fixture.CreateDirectory("owned/nested");
				File.WriteAllBytes(unreservedPath, contents);

				Should.Throw<IOException>(() => fixture.Dispose());

				Directory.Exists(rootPath).ShouldBeTrue();
				Directory.Exists(nestedDirectory).ShouldBeTrue();
				File.ReadAllBytes(unreservedPath).ShouldBe(contents);
			}
			finally
			{
				if (File.Exists(unreservedPath))
				{
					var attributes = File.GetAttributes(unreservedPath);
					if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0 &&
						File.ReadAllBytes(unreservedPath).SequenceEqual(contents))
						File.Delete(unreservedPath);
				}

				if (Directory.Exists(rootPath))
				{
					if (IsWindows)
					{
						CleanEmptySyntheticRoot(nestedDirectory);
						CleanEmptySyntheticRoot(Path.Combine(rootPath, "owned"));
						CleanEmptySyntheticRoot(rootPath);
					}
					else
						fixture.Dispose();
				}
			}
		}

		[Fact]
		public void DisposeRemovesNestedOwnedDirectoriesAndFiles()
		{
			string rootPath;
			using (var fixture = new IsolatedFileFixture())
			{
				rootPath = fixture.RootPath;
				fixture.CreateDirectory("owned/nested");
				fixture.WriteAllBytes("owned/nested/entry.log", new byte[] { 7, 8, 9 });
			}

			Directory.Exists(rootPath).ShouldBeFalse();
		}

		[Fact]
		public void DisposePreservesTheApprovedParentAndASyntheticSibling()
		{
			var fixture = new IsolatedFileFixture();
			var rootPath = fixture.RootPath;
			var parentPath = Directory.GetParent(rootPath).FullName;
			var siblingPath = Path.Combine(parentPath, "m4b1-sibling-" + Guid.NewGuid().ToString("N") + ".log");
			var contents = new byte[] { 10, 11, 12 };
			var expectedParent = IsWindows
				? Path.GetFullPath(@"C:\temp\logger tests")
				: ResolvePosixPath(Path.GetFullPath(Path.GetTempPath()));
			var siblingCreated = false;

			try
			{
				using (var stream = new FileStream(siblingPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					stream.Write(contents, 0, contents.Length);
					siblingCreated = true;
				}

				string.Equals(parentPath, expectedParent, IsWindows ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
					.ShouldBeTrue("The fixture must use the approved platform-specific parent.");

				fixture.Dispose();

				Directory.Exists(parentPath).ShouldBeTrue();
				Directory.Exists(rootPath).ShouldBeFalse();
				File.ReadAllBytes(siblingPath).ShouldBe(contents);
			}
			finally
			{
				if (siblingCreated)
					CleanSyntheticFile(siblingPath, contents);
				if (Directory.Exists(rootPath))
					fixture.Dispose();
			}
		}

		[Fact]
		public void DisposeRejectsAnOutsideDirectoryLinkAndPreservesItsSentinel()
		{
			var fixture = new IsolatedFileFixture();
			var rootPath = fixture.RootPath;
			var parentPath = Directory.GetParent(rootPath).FullName;
			var linkPath = fixture.GetPath("outside-link");
			var targetPath = Path.Combine(parentPath, "m4b1-target-" + Guid.NewGuid().ToString("N"));
			var sentinelPath = Path.Combine(targetPath, "sentinel.log");
			var contents = new byte[] { 13, 14, 15 };
			var linkCreated = false;
			var targetCreated = false;
			var sentinelCreated = false;
			var disposeAttempted = false;

			try
			{
				if (Directory.Exists(targetPath) || File.Exists(targetPath))
					throw new IOException("The synthetic outside target unexpectedly already exists.");
				Directory.CreateDirectory(targetPath);
				targetCreated = true;
				using (var stream = new FileStream(sentinelPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					stream.Write(contents, 0, contents.Length);
					sentinelCreated = true;
				}

				CreateOutsideDirectoryLink(linkPath, targetPath);
				linkCreated = true;
				disposeAttempted = true;
				Should.Throw<IOException>(() => fixture.Dispose());

				Directory.Exists(rootPath).ShouldBeTrue();
				Directory.Exists(targetPath).ShouldBeTrue();
				File.ReadAllBytes(sentinelPath).ShouldBe(contents);
			}
			finally
			{
				if (linkCreated)
					RemoveCreatedDirectoryLink(linkPath);

				if (!IsWindows || !disposeAttempted)
					fixture.Dispose();
				else
					CleanEmptySyntheticRoot(rootPath);

				if (sentinelCreated)
					CleanSyntheticFile(sentinelPath, contents);
				if (targetCreated && Directory.Exists(targetPath) &&
					(File.GetAttributes(targetPath) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == FileAttributes.Directory &&
					Directory.GetFileSystemEntries(targetPath).Length == 0)
					Directory.Delete(targetPath, false);
			}
		}

		private static void CleanSyntheticRemainder(IsolatedFileFixture fixture, string rootPath, string unreservedPath, byte[] expectedContents)
		{
			if (!Directory.Exists(rootPath))
				return;

			var rootAttributes = File.GetAttributes(rootPath);
			if ((rootAttributes & FileAttributes.Directory) == 0 || (rootAttributes & FileAttributes.ReparsePoint) != 0)
				return;

			if (File.Exists(unreservedPath))
			{
				var fileAttributes = File.GetAttributes(unreservedPath);
				if ((fileAttributes & FileAttributes.Directory) != 0 ||
					(fileAttributes & FileAttributes.ReparsePoint) != 0 ||
					!File.ReadAllBytes(unreservedPath).SequenceEqual(expectedContents))
					return;
				File.Delete(unreservedPath);
			}

			if (Directory.GetFileSystemEntries(rootPath).Length == 0)
			{
				if (IsWindows)
					Directory.Delete(rootPath, false);
				else
					fixture.Dispose();
			}
		}

		private static readonly bool IsWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;

		private static string ResolvePosixPath(string path)
		{
			var resolved = RealPathPosix(path, IntPtr.Zero);
			if (resolved == IntPtr.Zero)
				throw new IOException("Could not resolve the expected operating-system temporary directory.", new Win32Exception(Marshal.GetLastWin32Error()));
			try
			{
				return Marshal.PtrToStringAnsi(resolved);
			}
			finally
			{
				FreePosix(resolved);
			}
		}

		private static void CreateOutsideDirectoryLink(string linkPath, string targetPath)
		{
			if (!IsWindows)
			{
				if (CreateSymbolicLinkPosix(targetPath, linkPath) != 0)
					throw new IOException("Could not create the synthetic POSIX directory symlink.", new Win32Exception(Marshal.GetLastWin32Error()));
				return;
			}

			var command = "mklink /J \"" + linkPath + "\" \"" + targetPath + "\"";
			var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("ComSpec"), "/d /s /c \"" + command + "\"")
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true
			};
			using (var process = Process.Start(start))
			{
				var output = process.StandardOutput.ReadToEnd();
				var error = process.StandardError.ReadToEnd();
				process.WaitForExit();
				if (process.ExitCode != 0)
					throw new IOException("Could not create the synthetic Windows junction: " + output + error);
			}
		}

		private static void RemoveCreatedDirectoryLink(string linkPath)
		{
			if (!File.Exists(linkPath) && !Directory.Exists(linkPath))
				return;
			var attributes = File.GetAttributes(linkPath);
			if ((attributes & FileAttributes.ReparsePoint) == 0)
				throw new IOException("The synthetic link was replaced before test cleanup.");
			if ((attributes & FileAttributes.Directory) != 0)
				Directory.Delete(linkPath, false);
			else
				File.Delete(linkPath);
		}

		private static void CleanEmptySyntheticRoot(string rootPath)
		{
			if (!Directory.Exists(rootPath))
				return;
			var attributes = File.GetAttributes(rootPath);
			if ((attributes & FileAttributes.Directory) == 0 || (attributes & FileAttributes.ReparsePoint) != 0)
				return;
			if (Directory.GetFileSystemEntries(rootPath).Length == 0)
				Directory.Delete(rootPath, false);
		}

		private static void CleanSyntheticFile(string path, byte[] expectedContents)
		{
			if (!File.Exists(path))
				return;
			var attributes = File.GetAttributes(path);
			if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 ||
				!File.ReadAllBytes(path).SequenceEqual(expectedContents))
				return;
			File.Delete(path);
		}

		[DllImport("libc", EntryPoint = "symlink", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern int CreateSymbolicLinkPosix(string target, string link);

		[DllImport("libc", EntryPoint = "realpath", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
		private static extern IntPtr RealPathPosix(string path, IntPtr resolvedPath);

		[DllImport("libc", EntryPoint = "free", ExactSpelling = true)]
		private static extern void FreePosix(IntPtr pointer);
	}
}
