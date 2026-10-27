using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ProphetsWay.Utilities
{
	internal static class AutomaticFilePaths
	{
		internal static string CaptureHostDirectory(Func<string> hostDirectoryProvider)
		{
			try
			{
				return NormalizeDirectory(hostDirectoryProvider());
			}
			catch (Exception)
			{
				return null;
			}
		}

		internal static string ResolveSecondaryDirectory(string capturedHostDirectory, Func<string> secondaryDirectoryProvider)
		{
			try
			{
				var fullHostDirectory = NormalizeDirectory(capturedHostDirectory);
				if (fullHostDirectory == null)
					return null;

				var rootLength = Path.GetPathRoot(fullHostDirectory).Length;
				var keyLength = fullHostDirectory.Length;
				while (keyLength > rootLength && IsDirectorySeparator(fullHostDirectory[keyLength - 1]))
					keyLength--;

				var key = fullHostDirectory.Substring(0, keyLength);
				if (Path.DirectorySeparatorChar == '\\')
					key = key.Replace('\\', '/');

				var keyBytes = new UTF8Encoding(false, true).GetBytes(key);
				var component = new StringBuilder("app-", 68);
				using (var sha256 = SHA256.Create())
				{
					foreach (var digestByte in sha256.ComputeHash(keyBytes))
						component.Append(digestByte.ToString("x2", CultureInfo.InvariantCulture));
				}

				var secondaryRoot = NormalizeDirectory(secondaryDirectoryProvider());
				return secondaryRoot == null ? null : Path.Combine(secondaryRoot, component.ToString());
			}
			catch (Exception)
			{
				return null;
			}
		}

		private static string NormalizeDirectory(string directory)
		{
			if (string.IsNullOrEmpty(directory) || !HasCompleteRoot(Path.GetPathRoot(directory)))
				return null;

			return Path.GetFullPath(directory);
		}

		private static bool HasCompleteRoot(string root)
		{
			if (string.IsNullOrEmpty(root))
				return false;

			if (Path.DirectorySeparatorChar != '\\')
				return root[0] == '/';

			if (root.Length >= 3 && root[1] == ':' && IsDirectorySeparator(root[2]))
				return true;

			var isNtPrefix = root.StartsWith("\\??\\", StringComparison.Ordinal);
			if (root.Length < 2 || !IsDirectorySeparator(root[0]) || (!IsDirectorySeparator(root[1]) && !isNtPrefix))
				return false;

			var serverStart = 2;
			if (isNtPrefix || (root.Length >= 4 && (root[2] == '?' || root[2] == '.') && IsDirectorySeparator(root[3])))
			{
				if (root.Length >= 7 && string.Compare(root, 4, "UNC", 0, 3, StringComparison.OrdinalIgnoreCase) == 0
					&& (root.Length == 7 || IsDirectorySeparator(root[7])))
				{
					serverStart = 8;
				}
				else if (root.Length >= 6 && root[5] == ':')
				{
					return root.Length >= 7 && IsDirectorySeparator(root[6]);
				}
				else
				{
					return root.Length > 4 && !IsDirectorySeparator(root[4]);
				}
			}

			var serverEnd = serverStart;
			while (serverEnd < root.Length && !IsDirectorySeparator(root[serverEnd]))
				serverEnd++;

			return serverEnd > serverStart && serverEnd + 1 < root.Length
				&& !IsDirectorySeparator(root[serverEnd + 1]);
		}

		private static bool IsDirectorySeparator(char character)
		{
			return character == Path.DirectorySeparatorChar || character == Path.AltDirectorySeparatorChar;
		}
	}
}