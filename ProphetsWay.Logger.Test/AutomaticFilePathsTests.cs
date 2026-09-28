using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	public class AutomaticFilePathsTests
	{
		[Theory]
		[InlineData("C:/Apps/staging/../Ledger/", "/apps/staging/../Ledger/")]
		[InlineData("C:\\", "/")]
		[InlineData("\\\\host\\share\\", "/srv/share/")]
		public void ShouldCaptureTheBclNormalizedFullyQualifiedHostExactlyOnce(string windowsPath, string unixPath)
		{
			var supplied = ForPlatform(windowsPath, unixPath);
			var calls = 0;

			var captured = Capture(() =>
			{
				calls++;
				return supplied;
			});

			captured.ShouldBe(Path.GetFullPath(supplied));
			calls.ShouldBe(1);
		}

		[Theory]
		[InlineData(null, null)]
		[InlineData("", "")]
		[InlineData("relative", "relative")]
		[InlineData("C:folder", "C:folder")]
		[InlineData("\\folder", "\\folder")]
		[InlineData("/folder", "folder")]
		[InlineData("C:\\Apps\\invalid\0path", "/apps/invalid\0path")]
		public void ShouldReturnUnavailableForMissingIncompleteOrInvalidHostRoots(string windowsPath, string unixPath)
		{
			var supplied = ForPlatform(windowsPath, unixPath);
			var calls = 0;

			var captured = Capture(() =>
			{
				calls++;
				return supplied;
			});

			captured.ShouldBeNull();
			calls.ShouldBe(1);
			var secondaryCalls = 0;
			Resolve(supplied, () =>
			{
				secondaryCalls++;
				return ForPlatform("C:\\Local", "/local");
			}).ShouldBeNull();
			secondaryCalls.ShouldBe(0);
		}

		[Fact]
		public void ShouldReturnUnavailableWhenHostRetrievalThrowsWithoutRetrying()
		{
			var calls = 0;

			var captured = Capture(() =>
			{
				calls++;
				throw new InvalidOperationException("synthetic host retrieval failure");
			});

			captured.ShouldBeNull();
			calls.ShouldBe(1);
		}

		[Theory]
		[InlineData("C:\\Apps\\Ledger\\", "/apps/Ledger/", "C:/Apps/Ledger", "/apps/Ledger")]
		[InlineData("C:/Apps/Ledger///", "/apps/Ledger///", "C:/Apps/Ledger", "/apps/Ledger")]
		[InlineData("C:\\", "/", "C:/", "/")]
		[InlineData("C:\\Apps\\ledger\\", "/apps/ledger/", "C:/Apps/ledger", "/apps/ledger")]
		[InlineData("C:\\Other\\Ledger\\", "/other/Ledger/", "C:/Other/Ledger", "/other/Ledger")]
		[InlineData("C:\\Apps\\caf\u00e9-\U0001d11e\\", "/apps/caf\u00e9-\U0001d11e/", "C:/Apps/caf\u00e9-\U0001d11e", "/apps/caf\u00e9-\U0001d11e")]
		[InlineData("C:\\Apps\\cafe\u0301-\U0001d11e\\", "/apps/cafe\u0301-\U0001d11e/", "C:/Apps/cafe\u0301-\U0001d11e", "/apps/cafe\u0301-\U0001d11e")]
		[InlineData("C:\\Apps\\%APPDATA%\\ Ledger\\", "/apps/%APPDATA%/ Ledger/", "C:/Apps/%APPDATA%/ Ledger", "/apps/%APPDATA%/ Ledger")]
		public void ShouldUseTheCompleteOrdinalHostKeyForOneExactSecondaryComponent(string windowsHost, string unixHost, string windowsKey, string unixKey)
		{
			var host = ForPlatform(windowsHost, unixHost);
			var local = ForPlatform("C:/Profiles/staging/../Local/", "/profiles/staging/../Local/");
			var calls = new List<string>();
			var captured = Capture(() =>
			{
				calls.Add("host");
				return host;
			});

			var result = Resolve(captured, () =>
			{
				calls.Add("local");
				return local;
			});

			captured.ShouldBe(Path.GetFullPath(host));
			result.ShouldBe(Path.Combine(Path.GetFullPath(local), ComponentForKey(ForPlatform(windowsKey, unixKey))));
			calls.ShouldBe(new[] { "host", "local" });
		}

		[Theory]
		[InlineData(0xd800)]
		[InlineData(0xdc00)]
		public void ShouldReturnUnavailableForAnUnpairedHostSurrogateBeforeReadingTheSecondaryRoot(int codeUnit)
		{
			var host = ForPlatform("C:\\Apps\\invalid", "/apps/invalid") + (char)codeUnit;
			var calls = 0;

			var result = Resolve(host, () =>
			{
				calls++;
				return ForPlatform("C:\\Local", "/local");
			});

			result.ShouldBeNull();
			calls.ShouldBe(0);
		}

		[Theory]
		[InlineData(null, null)]
		[InlineData("", "")]
		[InlineData("relative", "relative")]
		[InlineData("C:folder", "C:folder")]
		[InlineData("\\folder", "\\folder")]
		[InlineData("/folder", "folder")]
		[InlineData("C:\\Local\\invalid\0path", "/local/invalid\0path")]
		public void ShouldReturnUnavailableForAnUnusableSecondaryRootWithoutSubstitution(string windowsPath, string unixPath)
		{
			var host = ForPlatform("C:\\Apps\\Ledger", "/apps/Ledger");
			var calls = 0;

			var result = Resolve(host, () =>
			{
				calls++;
				return ForPlatform(windowsPath, unixPath);
			});

			result.ShouldBeNull();
			calls.ShouldBe(1);
		}

		[Fact]
		public void ShouldReturnUnavailableWhenSecondaryRetrievalThrowsWithoutRetrying()
		{
			var calls = 0;

			var result = Resolve(ForPlatform("C:\\Apps\\Ledger", "/apps/Ledger"), () =>
			{
				calls++;
				throw new InvalidOperationException("synthetic secondary retrieval failure");
			});

			result.ShouldBeNull();
			calls.ShouldBe(1);
		}

		[Fact]
		public void ShouldFollowTheExecutingBclForLongSecondaryRootNormalizationAndCombination()
		{
			var host = ForPlatform("C:\\Apps\\Ledger", "/apps/Ledger");
			var local = ForPlatform("C:\\Local\\", "/local/") + new string('a', 4096);
			var component = ComponentForKey(ForPlatform("C:/Apps/Ledger", "/apps/Ledger"));
			string expected = null;
			var frameworkFailure = Record.Exception(() => expected = Path.Combine(Path.GetFullPath(local), component));
			var calls = 0;

			var result = Resolve(host, () =>
			{
				calls++;
				return local;
			});

			result.ShouldBe(frameworkFailure == null ? expected : null);
			calls.ShouldBe(1);
		}

		[Fact]
		public void ShouldReuseTheCapturedHostAndShareTheDirectoryForEqualKeys()
		{
			var suppliedHost = ForPlatform("C:\\Apps\\Ledger\\", "/apps/Ledger/");
			var local = ForPlatform("C:\\Local", "/local");
			var hostCalls = 0;
			var localCalls = 0;
			var captured = Capture(() =>
			{
				hostCalls++;
				return suppliedHost;
			});
			var first = Resolve(captured, () =>
			{
				localCalls++;
				return local;
			});
			suppliedHost = ForPlatform("C:\\Other\\Changed", "/other/Changed");

			var second = Resolve(captured, () =>
			{
				localCalls++;
				return local;
			});

			first.ShouldBe(Path.Combine(Path.GetFullPath(local), ComponentForKey(ForPlatform("C:/Apps/Ledger", "/apps/Ledger"))));
			second.ShouldBe(first);
			hostCalls.ShouldBe(1);
			localCalls.ShouldBe(2);
		}

		private static string ComponentForKey(string key)
		{
			using (var sha256 = SHA256.Create())
			{
				return "app-" + BitConverter.ToString(sha256.ComputeHash(new UTF8Encoding(false, true).GetBytes(key)))
					.Replace("-", "").ToLowerInvariant();
			}
		}

		private static string ForPlatform(string windowsPath, string unixPath)
		{
			return Path.DirectorySeparatorChar == '\\' ? windowsPath : unixPath;
		}

		private static string Capture(Func<string> getter)
		{
			return Invoke("CaptureHostDirectory", new[] { typeof(Func<string>) }, new object[] { getter });
		}

		private static string Resolve(string capturedHost, Func<string> getter)
		{
			return Invoke("ResolveSecondaryDirectory", new[] { typeof(string), typeof(Func<string>) }, new object[] { capturedHost, getter });
		}

		private static string Invoke(string operation, Type[] parameterTypes, object[] arguments)
		{
			var resolver = typeof(Utilities.LogLevels).Assembly.GetType("ProphetsWay.Utilities.AutomaticFilePaths", false);
			resolver.ShouldNotBeNull("S09 requires the actual internal AutomaticFilePaths type in the Logger assembly.");
			resolver.IsNotPublic.ShouldBeTrue("The resolver must not become public API.");
			var method = resolver.GetMethod(operation, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly,
				null, parameterTypes, null);
			method.ShouldNotBeNull("S09 requires the reviewed internal operation: " + operation);
			method.IsAssembly.ShouldBeTrue("The reviewed operation must be internal.");
			method.ReturnType.ShouldBe(typeof(string));
			try
			{
				return (string)method.Invoke(null, arguments);
			}
			catch (TargetInvocationException exception) when (exception.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
				throw;
			}
		}

		[Theory]
		[InlineData("\\??\\C:\\Apps\\Ledger", "/apps/Ledger", "\\??\\C:\\", "/", "/??/C:/Apps/Ledger", "/apps/Ledger")]
		[InlineData("\\??\\UNC\\host\\share\\Ledger", "/srv/share/Ledger", "\\??\\UNC\\host\\share", "/", "/??/UNC/host/share/Ledger", "/srv/share/Ledger")]
		public void ShouldCaptureSupportedNtHostsAndUseTheirCompleteKeys(string windowsHost, string unixHost, string windowsRoot, string unixRoot, string windowsKey, string unixKey)
		{
			var host = ForPlatform(windowsHost, unixHost);
			var completeRoot = ForPlatform(windowsRoot, unixRoot);
			var local = ForPlatform("C:\\Local", "/local");
			string expectedHost = null;
			var frameworkFailure = Record.Exception(() =>
			{
				var root = Path.GetPathRoot(host);
				if (root == completeRoot || root == completeRoot + Path.DirectorySeparatorChar)
					expectedHost = Path.GetFullPath(host);
			});
			if (frameworkFailure != null)
				expectedHost = null;
			var calls = new List<string>();

			var captured = Capture(() =>
			{
				calls.Add("host");
				return host;
			});
			var result = Resolve(captured, () =>
			{
				calls.Add("local");
				return local;
			});

			captured.ShouldBe(expectedHost);
			result.ShouldBe(expectedHost == null ? null : Path.Combine(Path.GetFullPath(local), ComponentForKey(ForPlatform(windowsKey, unixKey))));
			calls.ShouldBe(expectedHost == null ? new[] { "host" } : new[] { "host", "local" });
		}

		[Theory]
		[InlineData("\\??\\C:\\Local", "/local", "\\??\\C:\\", "/")]
		[InlineData("\\??\\UNC\\host\\share\\Local", "/srv/share/Local", "\\??\\UNC\\host\\share", "/")]
		public void ShouldUseSupportedNtSecondaryRootsWithoutSubstitution(string windowsLocal, string unixLocal, string windowsRoot, string unixRoot)
		{
			var host = ForPlatform("C:\\Apps\\Ledger", "/apps/Ledger");
			var local = ForPlatform(windowsLocal, unixLocal);
			var completeRoot = ForPlatform(windowsRoot, unixRoot);
			var component = ComponentForKey(ForPlatform("C:/Apps/Ledger", "/apps/Ledger"));
			string expected = null;
			var frameworkFailure = Record.Exception(() =>
			{
				var root = Path.GetPathRoot(local);
				if (root == completeRoot || root == completeRoot + Path.DirectorySeparatorChar)
					expected = Path.Combine(Path.GetFullPath(local), component);
			});
			if (frameworkFailure != null)
				expected = null;
			var calls = new List<string>();

			var captured = Capture(() =>
			{
				calls.Add("host");
				return host;
			});
			var result = Resolve(captured, () =>
			{
				calls.Add("local");
				return local;
			});

			captured.ShouldBe(Path.GetFullPath(host));
			result.ShouldBe(expected);
			calls.ShouldBe(new[] { "host", "local" });
		}

		[Theory]
		[InlineData("\\??\\C:folder", "relative")]
		[InlineData("\\??\\UNC\\host\\", "relative/share")]
		public void ShouldRejectIncompleteNtHostsBeforeReadingTheSecondaryRoot(string windowsHost, string unixHost)
		{
			var host = ForPlatform(windowsHost, unixHost);
			var hostCalls = 0;
			var secondaryCalls = 0;

			var captured = Capture(() =>
			{
				hostCalls++;
				return host;
			});
			var result = Resolve(host, () =>
			{
				secondaryCalls++;
				return ForPlatform("C:\\Local", "/local");
			});

			captured.ShouldBeNull();
			result.ShouldBeNull();
			hostCalls.ShouldBe(1);
			secondaryCalls.ShouldBe(0);
		}
	}
}