using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Tests
{
	public class IsolatedFileFixtureTests
	{
		[Fact]
		public void ObserveCreatedEntriesRegistersOnlyReservedOrdinaryCreationsAndAllowsMissingTargets()
		{
			string root;
			using (var fixture = new IsolatedFileFixture())
			{
				root = fixture.RootPath;
				var path = fixture.GetPath("created/nested/entry.log");
				fixture.GetPath("not-created/entry.log");
				var contents = new byte[] { 51, 52, 53 };
				Directory.CreateDirectory(Path.GetDirectoryName(path));
				using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
					stream.Write(contents, 0, contents.Length);

				fixture.ObserveCreatedEntries();
				fixture.ObserveCreatedEntries();

				File.ReadAllBytes(fixture.GetPath("created/nested/entry.log")).ShouldBe(contents);
				File.Exists(fixture.GetPath("not-created/entry.log")).ShouldBeFalse();
				using (var handle = fixture.OpenExclusive("created/nested/entry.log"))
					handle.Length.ShouldBe((long)contents.Length);
			}
			Directory.Exists(root).ShouldBeFalse();
		}

		[Fact]
		public void ObserveDeletedFileRejectsAStillPresentFileWithoutDeletingItOrGrantingReplacement()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var contents = new byte[] { 54, 55, 56 };
				fixture.WriteAllBytes("entry.log", contents);
				var path = fixture.GetPath("entry.log");

				Should.Throw<IOException>(() => fixture.ObserveDeletedFile("entry.log"));

				File.ReadAllBytes(path).ShouldBe(contents);
				AssertObservationRejectsReplacementAndRestoresOriginal(fixture, path);
				fixture.ObserveCreatedEntries();
			}
		}

		[Fact]
		public void ObserveDeletedFileRetiresOnlyTheAbsentSelectedFileBeforeObservedRecreation()
		{
			string root;
			using (var fixture = new IsolatedFileFixture())
			{
				root = fixture.RootPath;
				var original = new byte[] { 57, 58, 59 };
				var replacement = new byte[] { 60, 61, 62 };
				fixture.WriteAllBytes("entry.log", original);
				fixture.WriteAllBytes("sibling.log", original);
				var path = fixture.GetPath("entry.log");
				var sibling = fixture.GetPath("sibling.log");
				File.Delete(path);

				fixture.ObserveDeletedFile("entry.log");
				File.Exists(path).ShouldBeFalse();
				AssertObservationRejectsReplacementAndRestoresOriginal(fixture, sibling);
				fixture.ObserveCreatedEntries();
				using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
					stream.Write(replacement, 0, replacement.Length);
				fixture.ObserveCreatedEntries();

				File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(replacement);
				File.ReadAllBytes(fixture.GetPath("sibling.log")).ShouldBe(original);
			}
			Directory.Exists(root).ShouldBeFalse();
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ObserveDeletedFileRejectsAnAbsentFileThatWasNeverOwned(bool reserveMissingPath)
		{
			using (var fixture = new IsolatedFileFixture())
			{
				if (reserveMissingPath)
					fixture.GetPath("never-owned.log");

				Should.Throw<IOException>(() => fixture.ObserveDeletedFile("never-owned.log"));

				File.Exists(Path.Combine(fixture.RootPath, "never-owned.log")).ShouldBeFalse();
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void RepeatedCreationObservationsRejectChangedIdentityOrKindWithoutRefreshingIt(bool replaceWithDirectory)
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var path = fixture.GetPath("entry.log");
				using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
					stream.WriteByte(63);
				fixture.ObserveCreatedEntries();
				fixture.ObserveCreatedEntries();

				AssertObservationRejectsReplacementAndRestoresOriginal(fixture, path, replaceWithDirectory);
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void CreationObservationNeverAdoptsUnreservedOrPreexistingEntries(bool attemptLateReservation)
		{
			var fixture = new IsolatedFileFixture();
			var root = fixture.RootPath;
			var path = Path.Combine(root, "unreserved.log");
			var contents = new byte[] { 64, 65, 66 };
			try
			{
				using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
					stream.Write(contents, 0, contents.Length);
				if (attemptLateReservation)
				{
					// Refusal at reservation is sufficient; accepting it must not authorize adoption.
					var reservationFailure = Record.Exception(() => fixture.GetPath("unreserved.log"));
					if (reservationFailure != null)
						reservationFailure.ShouldBeOfType<IOException>();
				}
				// Ignoring an unreserved entry and explicitly refusing it are both safe.
				var observationFailure = Record.Exception(() => fixture.ObserveCreatedEntries());
				if (observationFailure != null)
					observationFailure.ShouldBeOfType<IOException>();

				File.ReadAllBytes(path).ShouldBe(contents);
				Should.Throw<IOException>(() => fixture.Dispose());
				File.ReadAllBytes(path).ShouldBe(contents);
			}
			finally
			{
				CleanSyntheticRemainder(fixture, root, path, contents);
			}
		}

		[Fact]
		public void CreationObservationRejectsAReservedLinkWithoutTouchingItsSyntheticTarget()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				fixture.CreateDirectory("target");
				var contents = new byte[] { 67, 68, 69 };
				fixture.WriteAllBytes("target/sentinel.log", contents);
				var target = fixture.GetPath("target");
				var sentinel = fixture.GetPath("target/sentinel.log");
				var link = fixture.GetPath("reserved-link");
				var created = false;
				try
				{
					CreateOutsideDirectoryLink(link, target);
					created = true;

					Should.Throw<IOException>(() => fixture.ObserveCreatedEntries());

					(File.GetAttributes(link) & FileAttributes.ReparsePoint).ShouldNotBe((FileAttributes)0);
					File.ReadAllBytes(sentinel).ShouldBe(contents);
				}
				finally
				{
					if (created)
						RemoveCreatedDirectoryLink(link);
				}
			}
		}

		private static void AssertObservationRejectsReplacementAndRestoresOriginal(IsolatedFileFixture fixture, string path, bool replaceWithDirectory = false)
		{
			// Keep the original identity alive in another fresh owned child, avoiding inode reuse
			// without introducing an unrelated unreserved-entry failure in the observed fixture.
			using (var holdingFixture = new IsolatedFileFixture())
			{
				var held = holdingFixture.GetPath("held.log");
				var replacement = new byte[] { 70, 71, 72 };
				File.Move(path, held);
				var replacementCreated = false;
				try
				{
					if (replaceWithDirectory)
					{
						Directory.CreateDirectory(path);
						replacementCreated = true;
					}
					else
					{
						using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
						{
							replacementCreated = true;
							stream.Write(replacement, 0, replacement.Length);
						}
					}
					Should.Throw<IOException>(() => fixture.ObserveCreatedEntries());
					Should.Throw<IOException>(() => fixture.ObserveCreatedEntries());
					if (replaceWithDirectory)
						Directory.Exists(path).ShouldBeTrue();
					else
						File.ReadAllBytes(path).ShouldBe(replacement);
				}
				finally
				{
					if (replacementCreated)
					{
						if (replaceWithDirectory)
							CleanEmptySyntheticRoot(path);
						else
							CleanSyntheticFile(path, replacement);
					}
					File.Move(held, path);
				}
			}
			// Successful comparison after restoring the original also detects a failed refresh.
			fixture.ObserveCreatedEntries();
		}

		[Fact]
		public void ParentPreparationCreatesMissingAncestryOnlyInsideAFreshSyntheticChild()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var first = fixture.GetPath("parent-probe");
				var parent = fixture.GetPath("parent-probe/approved-parent");
				var sentinel = new byte[] { 31, 32, 33 };
				fixture.WriteAllBytes("sibling.log", sentinel);
				try
				{
					Directory.Exists(first).ShouldBeFalse();
					PrepareSyntheticParent(parent);

					Directory.Exists(parent).ShouldBeTrue();
					(File.GetAttributes(first) & FileAttributes.ReparsePoint).ShouldBe((FileAttributes)0);
					(File.GetAttributes(parent) & FileAttributes.ReparsePoint).ShouldBe((FileAttributes)0);
					File.ReadAllBytes(Path.Combine(fixture.RootPath, "sibling.log")).ShouldBe(sentinel);
					Directory.GetFileSystemEntries(parent).ShouldBeEmpty();
				}
				finally
				{
					// The probe owns these names, not the real approved parent. Never recurse.
					CleanEmptySyntheticRoot(parent);
					CleanEmptySyntheticRoot(first);
				}
			}
		}

		[Fact]
		public void ParentPreparationPreservesExistingDirectoriesAndTheirEntries()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				fixture.CreateDirectory("existing-parent");
				var parent = fixture.GetPath("existing-parent");
				var sentinel = new byte[] { 34, 35, 36 };
				fixture.WriteAllBytes("existing-parent/sentinel.log", sentinel);

				PrepareSyntheticParent(parent);
				PrepareSyntheticParent(parent);

				Directory.Exists(parent).ShouldBeTrue();
				File.ReadAllBytes(Path.Combine(parent, "sentinel.log")).ShouldBe(sentinel);
				Directory.GetFileSystemEntries(parent).Select(Path.GetFileName).ShouldBe(new[] { "sentinel.log" });
			}
		}

		[Fact]
		public void ParentPreparationRejectsAFileInItsAncestryWithoutChangingIt()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var sentinel = new byte[] { 37, 38, 39 };
				fixture.WriteAllBytes("parent-file.log", sentinel);
				var file = fixture.GetPath("parent-file.log");

				Should.Throw<IOException>(() => PrepareSyntheticParent(Path.Combine(file, "approved-parent")));

				File.ReadAllBytes(file).ShouldBe(sentinel);
			}
		}

		[Fact]
		public void ParentPreparationRejectsALinkWithoutCreatingDirectoriesInItsTarget()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				fixture.CreateDirectory("parent-target");
				var target = fixture.GetPath("parent-target");
				var link = fixture.GetPath("parent-link");
				var sentinel = new byte[] { 40, 41, 42 };
				fixture.WriteAllBytes("parent-target/sentinel.log", sentinel);
				var linkCreated = false;
				try
				{
					CreateOutsideDirectoryLink(link, target);
					linkCreated = true;

					Should.Throw<IOException>(() => PrepareSyntheticParent(Path.Combine(link, "approved-parent")));

					Directory.Exists(Path.Combine(target, "approved-parent")).ShouldBeFalse();
					(File.GetAttributes(link) & FileAttributes.ReparsePoint).ShouldNotBe((FileAttributes)0);
					File.ReadAllBytes(Path.Combine(target, "sentinel.log")).ShouldBe(sentinel);
				}
				finally
				{
					if (linkCreated)
						RemoveCreatedDirectoryLink(link);
					CleanEmptySyntheticRoot(Path.Combine(target, "approved-parent"));
				}
			}
		}

		[Fact]
		public void DarwinStatDeclarationMatchesInode64Layout_ProxyNotNativeRuntimeEvidence()
		{
			var fixtureType = typeof(IsolatedFileFixture);
			var layout = fixtureType.GetNestedType("MacStat", BindingFlags.NonPublic);
			layout.ShouldNotBeNull();
			Marshal.SizeOf(layout).ShouldBe(144);
			Marshal.OffsetOf(layout, "Mode").ToInt32().ShouldBe(4);
			Marshal.OffsetOf(layout, "Inode").ToInt32().ShouldBe(8);
			var method = fixtureType.GetMethod("FStatMacPosix", BindingFlags.Static | BindingFlags.NonPublic);
			method.ShouldNotBeNull();
			method.GetCustomAttribute<DllImportAttribute>().EntryPoint.ShouldBe("fstat$INODE64",
				"The declared Darwin stat layout uses a 64-bit inode; a legacy fstat binding writes a different layout.");
		}

		[Fact]
		public void DarwinX64DirectoryEntryDeclarationIncludesInode64_ProxyNotNativeRuntimeEvidence()
		{
			typeof(IsolatedFileFixture).GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
				.Select(method => method.GetCustomAttribute<DllImportAttribute>())
				.Where(import => import != null)
				.Any(import => import.EntryPoint == "readdir$INODE64").ShouldBeTrue(
					"This checks only that the X64 symbol is declared; it is not native-runtime evidence.");
		}

		[Fact]
		public void RepeatedFixtureEnumerationReturnsAllCurrentRootEntriesWithoutDotNames()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				fixture.CreateDirectory("owned");
				fixture.WriteAllBytes("first.log", new byte[] { 43 });
				var expected = new[] { "first.log", "owned" };
				EnumerateFixtureRoot(fixture).OrderBy(name => name, StringComparer.Ordinal).ShouldBe(expected);
				EnumerateFixtureRoot(fixture).OrderBy(name => name, StringComparer.Ordinal).ShouldBe(expected);

				fixture.WriteAllBytes("later.log", new byte[] { 44 });
				EnumerateFixtureRoot(fixture).OrderBy(name => name, StringComparer.Ordinal)
					.ShouldBe(new[] { "first.log", "later.log", "owned" });
			}
		}

		private static IEnumerable<string> EnumerateFixtureRoot(IsolatedFileFixture fixture)
		{
			var type = typeof(IsolatedFileFixture);
			if (IsWindows)
			{
				// Exercise the Windows fixture collector, not a substitute POSIX emulator.
				var entries = new List<KeyValuePair<string, bool>>();
				InvokeFixtureMethod(type.GetMethod("CollectOwnedEntries", BindingFlags.Instance | BindingFlags.NonPublic),
					fixture, new object[] { fixture.RootPath, entries });
				return entries.Where(entry => Path.GetDirectoryName(entry.Key) == fixture.RootPath)
					.Select(entry => Path.GetFileName(entry.Key)).ToArray();
			}

			var handle = (Microsoft.Win32.SafeHandles.SafeFileHandle)type
				.GetField("_ownership", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fixture);
			return (IEnumerable<string>)InvokeFixtureMethod(
				type.GetMethod("EnumeratePosixNames", BindingFlags.Static | BindingFlags.NonPublic),
				null, new object[] { handle.DangerousGetHandle().ToInt32() });
		}

		private static void PrepareSyntheticParent(string parent)
		{
			// Proposed narrow seam: private/internal static void PrepareParentDirectory(string path).
			// Until it exists, exercise the actual current parent-preparation step so missing
			// ancestry fails with a filesystem exception, not a missing-method/compile error.
			// The constructor must use the new seam; ordinary ancestry validation must stay noncreating.
			var type = typeof(IsolatedFileFixture);
			var method = type.GetMethod("PrepareParentDirectory", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
				?? type.GetMethod("ValidateDirectoryAncestry", BindingFlags.Static | BindingFlags.NonPublic);
			InvokeFixtureMethod(method, null, new object[] { parent });
		}

		private static object InvokeFixtureMethod(MethodInfo method, object target, object[] arguments)
		{
			method.ShouldNotBeNull();
			try
			{
				return method.Invoke(target, arguments);
			}
			catch (TargetInvocationException failure) when (failure.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
				throw;
			}
		}

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
