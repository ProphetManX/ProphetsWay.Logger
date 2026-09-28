using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class FileDestinationTests
	{
		[Fact]
		public void ShouldCreateNewFileForLogging()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var fi = new FileInfo(fixture.GetPath("entry.log"));
				var dest = new FileDestination(fi.FullName, LogLevels.Debug, resetFile: true);
				try
				{
					Utilities.Logger.AddDestination(dest);
					Utilities.Logger.Debug("Hello World!");
					fi.Refresh();
					fi.Exists.Should().BeTrue();
				}
				finally
				{
					Utilities.Logger.RemoveDestination(dest);
				}
			}
		}

		[Fact]
		public void ShouldWriteMessageToFile()
		{
			const string msg = "Hello World!";
			string contents;
			using (var fixture = new IsolatedFileFixture())
			{
				var fi = new FileInfo(fixture.GetPath("entry.log"));
				var dest = new FileDestination(fi.FullName, LogLevels.Debug, resetFile: true);
				try
				{
					Utilities.Logger.AddDestination(dest);
					Utilities.Logger.Debug(msg);
					using (var tr = fi.OpenText())
						contents = tr.ReadToEnd();
					contents.Should().Contain(msg);
				}
				finally
				{
					Utilities.Logger.RemoveDestination(dest);
				}
			}
		}

		[Fact]
		public void ShouldDeleteExistingLogFile()
		{
			const string initialMsg = "This is a default Text File.";
			const string msg = "Hello World!";
			string contents;
			using (var fixture = new IsolatedFileFixture())
			{
				fixture.WriteAllBytes("entry.log", Encoding.UTF8.GetBytes(initialMsg));
				var fi = new FileInfo(fixture.GetPath("entry.log"));
				var dest = new FileDestination(fi.FullName, LogLevels.Debug, resetFile: true);
				try
				{
					Utilities.Logger.AddDestination(dest);
					Utilities.Logger.Debug(msg);
					using (var tr = fi.OpenText())
						contents = tr.ReadToEnd();
					contents.Should().NotContain(initialMsg).And.Contain(msg);
				}
				finally
				{
					Utilities.Logger.RemoveDestination(dest);
				}
			}
		}

		[Fact]
		public void ShouldAppendToExistingLogFile()
		{
			const string initialMsg = "This is a default Text File.";
			const string msg = "Hello World!";
			string contents;
			using (var fixture = new IsolatedFileFixture())
			{
				fixture.WriteAllBytes("entry.log", Encoding.UTF8.GetBytes(initialMsg));
				var fi = new FileInfo(fixture.GetPath("entry.log"));
				var dest = new FileDestination(fi.FullName, LogLevels.Debug, resetFile: false);
				try
				{
					Utilities.Logger.AddDestination(dest);
					Utilities.Logger.Debug(msg);
					using (var tr = fi.OpenText())
						contents = tr.ReadToEnd();
					contents.Should().Contain(initialMsg).And.Contain(msg);
				}
				finally
				{
					Utilities.Logger.RemoveDestination(dest);
				}
			}
		}
		[Fact]
		public void ShouldExposeOnlyTheAcceptedExplicitFileSurfaceAndDefaults()
		{
			var type = typeof(FileDestination);
			type.BaseType.ShouldBe(typeof(TextBasedDestination));
			type.IsAbstract.ShouldBeFalse();
			type.IsSealed.ShouldBeFalse();
			type.GetConstructors().Length.ShouldBe(3);
			foreach (var maskType in new[] { typeof(LogLevels), typeof(string), typeof(int) })
			{
				var constructor = type.GetConstructor(new[] { typeof(string), maskType, typeof(bool), typeof(FileDestination.EncodingOptions) });
				constructor.ShouldNotBeNull();
				var parameters = constructor.GetParameters();
				var maskName = maskType == typeof(LogLevels) ? "reportingLevel" : maskType == typeof(string) ? "strReportingLevel" : "intReportingLevel";
				parameters.Select(parameter => parameter.Name).ShouldBe(new[] { "fileName", maskName, "resetFile", "encoder" });
				parameters[0].IsOptional.ShouldBeFalse();
				parameters[1].IsOptional.ShouldBe(maskType == typeof(LogLevels));
				if (maskType == typeof(LogLevels))
					parameters[1].DefaultValue.ShouldBe(LogLevels.Debug);
				parameters[2].IsOptional.ShouldBeTrue();
				parameters[2].DefaultValue.ShouldBe(false);
				parameters[3].IsOptional.ShouldBeTrue();
				parameters[3].DefaultValue.ShouldBe(FileDestination.EncodingOptions.UTF8);
			}
			var declared = BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
			var methods = type.GetMethods(declared).Where(method => method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly).ToArray();
			methods.Select(method => method.Name).ShouldBe(new[] { "PrintLogEntry" });
			methods[0].IsFamily.ShouldBeTrue();
			methods[0].IsVirtual.ShouldBeTrue();
			methods[0].IsFinal.ShouldBeFalse();
			methods[0].ReturnType.ShouldBe(typeof(void));
			methods[0].GetParameters().Select(parameter => parameter.ParameterType).ShouldBe(new[] { typeof(string) });
			type.GetFields(declared).Where(field => field.IsPublic || field.IsFamily || field.IsFamilyOrAssembly).ShouldBeEmpty();
			type.GetProperties(declared).Where(property => property.GetAccessors(true).Any(accessor => accessor.IsPublic || accessor.IsFamily || accessor.IsFamilyOrAssembly)).ShouldBeEmpty();
			type.GetEvents(declared).Where(item => item.GetAddMethod(true).IsPublic || item.GetAddMethod(true).IsFamily || item.GetAddMethod(true).IsFamilyOrAssembly).ShouldBeEmpty();
			type.GetNestedTypes(BindingFlags.Public).ShouldBe(new[] { typeof(FileDestination.EncodingOptions) });
			Enum.GetUnderlyingType(typeof(FileDestination.EncodingOptions)).ShouldBe(typeof(int));
			typeof(FileDestination.EncodingOptions).IsDefined(typeof(FlagsAttribute), false).ShouldBeFalse();
			Enum.GetNames(typeof(FileDestination.EncodingOptions)).ShouldBe(new[] { "ASCII", "BigEndianUnicode", "Unicode", "UTF8", "UTF32" });
			Enum.GetValues(typeof(FileDestination.EncodingOptions)).Cast<FileDestination.EncodingOptions>().Select(value => (int)value).ShouldBe(new[] { 0, 1, 2, 3, 4 });
		}

		[Theory]
		[InlineData("enum")]
		[InlineData("string")]
		[InlineData("integer")]
		public void ShouldAppendWithResetOmittedFromEachPublicConstructor(string overload)
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var prefix = new byte[] { 0, 255, 128, 65 };
				fixture.WriteAllBytes("entry.log", prefix);
				string path = fixture.GetPath("entry.log");
				FileDestination destination;
				switch (overload)
				{
					case "enum": destination = new FileDestination(path); break;
					case "string": destination = new FileDestination(path, strReportingLevel: "Debug"); break;
					default: destination = new FileDestination(path, intReportingLevel: (int)LogLevels.Debug); break;
				}
				File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(prefix);
				var observer = new CompletedRecordObserver();
				try
				{
					Utilities.Logger.AddDestination(destination);
					Utilities.Logger.AddDestination(observer);
					Utilities.Logger.Debug("default append");
					observer.Records.Count.ShouldBe(1);
					File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(prefix.Concat(Encoding.UTF8.GetBytes(observer.Records.Single() + Environment.NewLine)).ToArray());
				}
				finally
				{
					Utilities.Logger.RemoveDestination(observer);
					Utilities.Logger.RemoveDestination(destination);
				}
			}
		}

		[Theory]
		[InlineData("enum")]
		[InlineData("string")]
		[InlineData("integer")]
		public void ShouldResetOnlyTheSelectedFileDuringConstructionAndAppendLaterEntries(string overload)
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var prefix = Encoding.UTF8.GetBytes("synthetic old contents");
				fixture.WriteAllBytes("entry.log", prefix);
				fixture.WriteAllBytes("sibling.log", prefix);
				string path = fixture.GetPath("entry.log");
				FileDestination destination;
				switch (overload)
				{
					case "enum": destination = new FileDestination(path, resetFile: true); break;
					case "string": destination = new FileDestination(path, strReportingLevel: "Debug", resetFile: true); break;
					default: destination = new FileDestination(path, intReportingLevel: (int)LogLevels.Debug, resetFile: true); break;
				}
				File.Exists(fixture.GetPath("entry.log")).ShouldBeFalse();
				File.ReadAllBytes(fixture.GetPath("sibling.log")).ShouldBe(prefix);
				var observer = new CompletedRecordObserver();
				try
				{
					Utilities.Logger.AddDestination(destination);
					Utilities.Logger.AddDestination(observer);
					Utilities.Logger.Info("first");
					Utilities.Logger.Info("second");
					observer.Records.Count.ShouldBe(2);
					File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(Encoding.UTF8.GetBytes(string.Concat(observer.Records.Select(record => record + Environment.NewLine))));
					File.ReadAllBytes(fixture.GetPath("sibling.log")).ShouldBe(prefix);
				}
				finally
				{
					Utilities.Logger.RemoveDestination(observer);
					Utilities.Logger.RemoveDestination(destination);
				}
			}
		}

		[Theory]
		[InlineData("enum", -1)]
		[InlineData("enum", 5)]
		[InlineData("string", -1)]
		[InlineData("string", 5)]
		[InlineData("integer", -1)]
		[InlineData("integer", 5)]
		public void ShouldRejectUndefinedEncodingBeforeDeletingOrCreatingDirectories(string overload, int encoder)
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var prefix = new byte[] { 8, 0, 255, 13, 10 };
				fixture.WriteAllBytes("sentinel.log", prefix);
				foreach (var relativePath in new[] { "sentinel.log", "missing/entry.log" })
				{
					string path = fixture.GetPath(relativePath);
					var failure = Should.Throw<ArgumentOutOfRangeException>(() =>
					{
						switch (overload)
						{
							case "enum": new FileDestination(path, LogLevels.Debug, true, (FileDestination.EncodingOptions)encoder); break;
							case "string": new FileDestination(path, strReportingLevel: "Debug", resetFile: true, encoder: (FileDestination.EncodingOptions)encoder); break;
							default: new FileDestination(path, intReportingLevel: (int)LogLevels.Debug, resetFile: true, encoder: (FileDestination.EncodingOptions)encoder); break;
						}
					});
					failure.ParamName.ShouldBe("encoder");
					File.ReadAllBytes(fixture.GetPath("sentinel.log")).ShouldBe(prefix);
					Directory.Exists(fixture.GetPath("missing")).ShouldBeFalse();
				}
			}
		}

		[Theory]
		[InlineData("enum", "-1")]
		[InlineData("enum", "64")]
		[InlineData("integer", "-1")]
		[InlineData("integer", "64")]
		[InlineData("string", null)]
		[InlineData("string", "debug")]
		[InlineData("string", "64")]
		public void ShouldRejectInvalidSeverityBeforeFilesystemEffects(string overload, string mask)
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var prefix = new byte[] { 1, 2, 3 };
				fixture.WriteAllBytes("sentinel.log", prefix);
				foreach (var relativePath in new[] { "sentinel.log", "missing/entry.log" })
				{
					string path = fixture.GetPath(relativePath);
					var failure = Record.Exception(() =>
					{
						switch (overload)
						{
							case "enum": new FileDestination(path, (LogLevels)int.Parse(mask), true); break;
							case "integer": new FileDestination(path, intReportingLevel: int.Parse(mask), resetFile: true); break;
							default: new FileDestination(path, strReportingLevel: mask, resetFile: true); break;
						}
					});
					if (overload != "string")
						failure.ShouldBeOfType<ArgumentOutOfRangeException>().ParamName.ShouldBe(overload == "enum" ? "reportingLevel" : "intReportingLevel");
					else if (mask == null)
						failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("strReportingLevel");
					else
						failure.ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("strReportingLevel");
					File.ReadAllBytes(fixture.GetPath("sentinel.log")).ShouldBe(prefix);
					Directory.Exists(fixture.GetPath("missing")).ShouldBeFalse();
				}
			}
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("invalid\0.log")]
		public void ShouldRejectInvalidPathArgumentsWithoutCreatingEntries(string fileName)
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var previousDirectory = Environment.CurrentDirectory;
				try
				{
					fixture.CreateDirectory("cwd");
					Environment.CurrentDirectory = fixture.GetPath("cwd");
					var failure = Record.Exception(() => new FileDestination(fileName, resetFile: true));
					if (fileName == null)
						failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("fileName");
					else
						failure.ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("fileName");
					Directory.GetFileSystemEntries(fixture.GetPath("cwd")).ShouldBeEmpty();
				}
				finally
				{
					Environment.CurrentDirectory = previousDirectory;
				}
			}
		}

		[Theory]
		[InlineData(FileDestination.EncodingOptions.ASCII)]
		[InlineData(FileDestination.EncodingOptions.BigEndianUnicode)]
		[InlineData(FileDestination.EncodingOptions.Unicode)]
		[InlineData(FileDestination.EncodingOptions.UTF8)]
		[InlineData(FileDestination.EncodingOptions.UTF32)]
		public void ShouldWriteExactCompletedRecordBytesWithOneSuffixAndNoPreamble(FileDestination.EncodingOptions option)
		{
			var encodings = new Dictionary<FileDestination.EncodingOptions, Encoding>
			{
				{ FileDestination.EncodingOptions.ASCII, Encoding.ASCII },
				{ FileDestination.EncodingOptions.BigEndianUnicode, Encoding.BigEndianUnicode },
				{ FileDestination.EncodingOptions.Unicode, Encoding.Unicode },
				{ FileDestination.EncodingOptions.UTF8, Encoding.UTF8 },
				{ FileDestination.EncodingOptions.UTF32, Encoding.UTF32 }
			};
			using (var fixture = new IsolatedFileFixture())
			using (Utilities.Logger.BeginScope(new LogAnnotations(new[] { new SensitivityLabel("scope-private") }),
				new[] { new KeyValuePair<string, object>("key\nprivate", "value\r\n\u2028\u00e9") }))
			{
				var destination = new ObservedFileDestination(fixture.GetPath("entry.log"), encoder: option);
				File.Exists(fixture.GetPath("entry.log")).ShouldBeFalse();
				var exception = new InvalidOperationException("outer\nprivate", new Exception("inner\rprivate"));
				destination.Log(LogLevels.InformationOnly, "message\r\n\t\"\\n\u0085\u2028\u2029\u00e9\ud800", exception);
				destination.Records.Count.ShouldBe(1);
				File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(CompletedBytes(destination, encodings[option]));
				destination.Log(LogLevels.InformationOnly, "message\r\n\t\"\\n\u0085\u2028\u2029\u00e9\ud800", exception);
				destination.Records.Count.ShouldBe(2);
				File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(CompletedBytes(destination, encodings[option]));
				using (var handle = fixture.OpenExclusive("entry.log"))
					handle.Length.ShouldBe((long)CompletedBytes(destination, encodings[option]).Length);
			}
		}

		[Fact]
		public void ShouldPreserveOpaqueExistingBytesWithoutSeparatorRepairOrTranscoding()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				var prefix = new byte[] { 239, 187, 191, 0, 255, 65, 128 };
				fixture.WriteAllBytes("entry.log", prefix);
				var destination = new ObservedFileDestination(fixture.GetPath("entry.log"), encoder: FileDestination.EncodingOptions.BigEndianUnicode);
				destination.Log(LogLevels.InformationOnly, "appended\u00e9");
				destination.Records.Count.ShouldBe(1);
				File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(prefix.Concat(CompletedBytes(destination, Encoding.BigEndianUnicode)).ToArray());
			}
		}

		[Fact]
		public void ShouldKeepConstructionTimeRelativePathWhenRecreatingADeletedFile()
		{
			using (var fixture = new IsolatedFileFixture())
			{
				fixture.CreateDirectory("A");
				fixture.CreateDirectory("B");
				string firstDirectory = fixture.GetPath("A");
				string secondDirectory = fixture.GetPath("B");
				fixture.GetPath("A/logs/entry.log");
				fixture.GetPath("B/logs/entry.log");
				var previousDirectory = Environment.CurrentDirectory;
				try
				{
					Environment.CurrentDirectory = firstDirectory;
					var destination = new ObservedFileDestination(Path.Combine("logs", "entry.log"));
					Directory.Exists(fixture.GetPath("A/logs")).ShouldBeTrue();
					File.Exists(fixture.GetPath("A/logs/entry.log")).ShouldBeFalse();
					Environment.CurrentDirectory = secondDirectory;
					destination.Log(LogLevels.InformationOnly, "before removal");
					destination.Records.Count.ShouldBe(1);
					File.ReadAllBytes(fixture.GetPath("A/logs/entry.log")).ShouldBe(CompletedBytes(destination, Encoding.UTF8));
					File.Exists(fixture.GetPath("B/logs/entry.log")).ShouldBeFalse();
					fixture.DeleteFile("A/logs/entry.log");
					destination.Log(LogLevels.InformationOnly, "after removal");
					destination.Records.Count.ShouldBe(2);
					File.ReadAllBytes(fixture.GetPath("A/logs/entry.log")).ShouldBe(Encoding.UTF8.GetBytes(destination.Records.Last() + Environment.NewLine));
					File.Exists(fixture.GetPath("B/logs/entry.log")).ShouldBeFalse();
				}
				finally
				{
					Environment.CurrentDirectory = previousDirectory;
				}
			}
		}

		[Theory]
		[InlineData("severity", false)]
		[InlineData("severity", true)]
		[InlineData("label", false)]
		[InlineData("label", true)]
		[InlineData("registration severity", false)]
		[InlineData("registration severity", true)]
		[InlineData("registration label", false)]
		[InlineData("registration label", true)]
		[InlineData("zero", false)]
		[InlineData("zero", true)]
		public void ShouldRejectEntriesBeforeFormattingOrFileEffects(string gate, bool existingFile)
		{
			using (var fixture = new IsolatedFileFixture())
			using (Utilities.Logger.BeginScope(new LogAnnotations(new[] { new SensitivityLabel("denied-private") })))
			{
				var prefix = new byte[] { 7, 0, 255 };
				if (existingFile)
					fixture.WriteAllBytes("entry.log", prefix);
				var deny = new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { new SensitivityLabel("denied-private") });
				var allow = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);
				var mask = gate == "zero" ? (LogLevels)0 : gate == "severity" ? LogLevels.ErrorOnly : LogLevels.Trace;
				var destination = new ObservedFileDestination(fixture.GetPath("entry.log"), mask)
				{
					LabelPolicy = gate == "label" ? deny : allow
				};
				var reports = new List<LogFailureReport>();
				Action<LogFailureReport> observer = reports.Add;
				Utilities.Logger.DispatchFailed += observer;
				try
				{
					if (gate.StartsWith("registration", StringComparison.Ordinal))
					{
						Utilities.Logger.AddDestination(destination, new DestinationRegistrationSettings(true,
							gate == "registration severity" ? LogLevels.ErrorOnly : LogLevels.Trace,
							gate == "registration label" ? deny : allow));
						Utilities.Logger.Info("denied payload");
					}
					else
						destination.Log(LogLevels.InformationOnly, "denied payload");
					destination.MassageCalls.ShouldBe(0);
					destination.Records.ShouldBeEmpty();
					reports.ShouldBeEmpty();
					if (existingFile)
						File.ReadAllBytes(fixture.GetPath("entry.log")).ShouldBe(prefix);
					else
						File.Exists(fixture.GetPath("entry.log")).ShouldBeFalse();
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.RemoveDestination(destination);
				}
			}
		}

		[Fact]
		public async Task ShouldSerializeFileWritesWithoutSerializingConcurrentRendering()
		{
			using (var fixture = new IsolatedFileFixture())
			using (var barrier = new Barrier(4))
			{
				var destination = new ObservedFileDestination(fixture.GetPath("entry.log"))
				{
					BeforeMassage = () =>
					{
						if (!barrier.SignalAndWait(TimeSpan.FromSeconds(10)))
							throw new TimeoutException("Concurrent native rendering did not reach the test gate.");
					}
				};
				var workers = Enumerable.Range(0, 4).Select(index => Task.Run(() =>
					destination.Log(LogLevels.InformationOnly, "worker-" + index + ":" + new string((char)('A' + index), 8192)),
					TestContext.Current.CancellationToken)).ToArray();
				await Task.WhenAll(workers);
				destination.MassageCalls.ShouldBe(4);
				destination.Records.Count.ShouldBe(4);
				var content = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.GetPath("entry.log")));
				var lines = content.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
				lines.Length.ShouldBe(5);
				lines.Last().ShouldBe("");
				lines.Take(4).OrderBy(line => line, StringComparer.Ordinal)
					.ShouldBe(destination.Records.OrderBy(record => record, StringComparer.Ordinal));
				using (var handle = fixture.OpenExclusive("entry.log"))
					handle.Length.ShouldBe((long)CompletedBytes(destination, Encoding.UTF8).Length);
			}
		}

		[Theory]
		[InlineData("prepare", false)]
		[InlineData("prepare", true)]
		[InlineData("reset", false)]
		[InlineData("reset", true)]
		public void ShouldPreserveOrdinaryConstructionFailuresWithoutDiagnosticSideOutput(string operation, bool throwingConsole)
		{
			using (var fixture = new IsolatedFileFixture())
			using (var output = new ObservedWriter(throwingConsole))
			using (var error = new ObservedWriter(false))
			{
				var prefix = new byte[] { 9, 8, 7 };
				var reports = new List<LogFailureReport>();
				Action<LogFailureReport> observer = reports.Add;
				var previousOutput = Console.Out;
				var previousError = Console.Error;
				FileStream held = null;
				try
				{
					string target;
					Exception frameworkFailure;
					if (operation == "prepare")
					{
						fixture.WriteAllBytes("obstacle", prefix);
						target = fixture.GetPath("obstacle/child/entry.log");
						frameworkFailure = Record.Exception(() => { Directory.CreateDirectory(fixture.GetPath("obstacle/child")); });
					}
					else
					{
						fixture.WriteAllBytes("entry.log", prefix);
						target = fixture.GetPath("entry.log");
						held = fixture.OpenExclusive("entry.log");
						frameworkFailure = Record.Exception(() => { File.Delete(fixture.GetPath("entry.log")); });
					}
					frameworkFailure.ShouldBeOfType<IOException>();
					Console.SetOut(output);
					Console.SetError(error);
					Utilities.Logger.DispatchFailed += observer;
					var failure = Record.Exception(() => new FileDestination(target, resetFile: true));
					failure.ShouldBeOfType<IOException>();
					failure.GetType().ShouldBe(frameworkFailure.GetType());
					failure.HResult.ShouldBe(frameworkFailure.HResult);
					output.Attempts.ShouldBe(0);
					error.Attempts.ShouldBe(0);
					output.ToString().ShouldBe("");
					error.ToString().ShouldBe("");
					reports.ShouldBeEmpty();
					if (held != null)
					{
						held.Dispose();
						held = null;
					}
					File.ReadAllBytes(fixture.GetPath(operation == "prepare" ? "obstacle" : "entry.log")).ShouldBe(prefix);
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= observer;
					Console.SetOut(previousOutput);
					Console.SetError(previousError);
					if (held != null)
						held.Dispose();
				}
			}
		}

		[Theory]
		[InlineData("direct", false)]
		[InlineData("direct", true)]
		[InlineData("first", false)]
		[InlineData("first", true)]
		[InlineData("last", false)]
		[InlineData("last", true)]
		public void ShouldReportRealOutputFailureThenAllowANewCallAtTheSamePath(string route, bool throwingReporters)
		{
			using (var fixture = new IsolatedFileFixture())
			using (var output = new ObservedWriter(false))
			using (var error = new ObservedWriter(throwingReporters))
			using (Utilities.Logger.BeginScope(new LogAnnotations(new[] { new SensitivityLabel("label-private") }),
				new[] { new KeyValuePair<string, object>("key-private", "value-private") }))
			{
				string path = fixture.GetPath("path-private.log");
				var failed = new ObservedFileDestination(path);
				var good = new ObservedFileDestination(fixture.GetPath("good.log"));
				fixture.CreateDirectory("path-private.log");
				var reports = new List<LogFailureReport>();
				var goodAttemptsAtNotification = -1;
				Action<LogFailureReport> observer = report =>
				{
					reports.Add(report);
					goodAttemptsAtNotification = good.Records.Count;
					if (throwingReporters)
						throw new InvalidOperationException("reporter-private");
				};
				var previousOutput = Console.Out;
				var previousError = Console.Error;
				try
				{
					Console.SetOut(output);
					Console.SetError(error);
					Utilities.Logger.DispatchFailed += observer;
					if (route != "direct")
					{
						Utilities.Logger.AddDestination(route == "first" ? failed : good);
						Utilities.Logger.AddDestination(route == "first" ? good : failed);
					}
					var failure = Should.Throw<LogDispatchException>(() =>
					{
						if (route == "direct")
							failed.Log(LogLevels.InformationOnly, "message-private", new Exception("cause-private"));
						else
							Utilities.Logger.LogAnnotated(null, LogLevels.InformationOnly, "message-private", new Exception("cause-private"));
					});
					failed.Records.Count.ShouldBe(1);
					good.Records.Count.ShouldBe(route == "direct" ? 0 : 1);
					goodAttemptsAtNotification.ShouldBe(route == "direct" ? 0 : 1);
					var report = reports.ShouldHaveSingleItem();
					AssertSafeOutputFailure(failure, report, error.ToString(), path);
					report.Failures.Single().RegistrationId.ShouldBe(route == "last" ? 2 : 1);
					output.Attempts.ShouldBe(0);
					if (route != "direct")
						File.ReadAllBytes(fixture.GetPath("good.log")).ShouldBe(CompletedBytes(good, Encoding.UTF8));
					fixture.DeleteDirectory("path-private.log");
					if (route == "direct")
						failed.Log(LogLevels.InformationOnly, "new independent call");
					else
						Utilities.Logger.Info("new independent call");
					failed.Records.Count.ShouldBe(2);
					reports.Count.ShouldBe(1);
					File.ReadAllBytes(fixture.GetPath("path-private.log")).ShouldBe(Encoding.UTF8.GetBytes(failed.Records.Last() + Environment.NewLine));
					if (route != "direct")
					{
						good.Records.Count.ShouldBe(2);
						File.ReadAllBytes(fixture.GetPath("good.log")).ShouldBe(CompletedBytes(good, Encoding.UTF8));
					}
					using (var handle = fixture.OpenExclusive("path-private.log"))
						handle.Length.ShouldBe((long)Encoding.UTF8.GetByteCount(failed.Records.Last() + Environment.NewLine));
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.RemoveDestination(failed);
					Utilities.Logger.RemoveDestination(good);
					Console.SetOut(previousOutput);
					Console.SetError(previousError);
				}
			}
		}

		private static void AssertSafeOutputFailure(LogDispatchException failure, LogFailureReport report, string diagnostic, string path)
		{
			failure.Report.CorrelationId.ShouldBe(report.CorrelationId);
			report.CorrelationId.ShouldNotBe(Guid.Empty);
			report.CoreCaptureFailureCount.ShouldBe(0);
			report.OverflowCount.ShouldBe(0);
			report.Failures.ShouldHaveSingleItem().Stage.ShouldBe(LogFailureStage.Output);
			failure.InnerException.ShouldBeNull();
			failure.StackTrace.ShouldBeNull();
			failure.HelpLink.ShouldBeNull();
			failure.Data.Count.ShouldBe(0);
			diagnostic.Length.ShouldBeInRange(1, 512);
			var safe = diagnostic + failure.Message + failure.ToString();
			foreach (var canary in new[] { path, "path-private", "message-private", "cause-private", "reporter-private", "label-private", "key-private", "value-private" })
				safe.ShouldNotContain(canary, Case.Sensitive);
		}

		private sealed class ObservedWriter : StringWriter
		{
			private readonly bool _throwOnWrite;
			public int Attempts;
			public ObservedWriter(bool throwOnWrite) { _throwOnWrite = throwOnWrite; }
			public override void Write(char value)
			{
				Attempts++;
				base.Write(value);
				if (_throwOnWrite) throw new InvalidOperationException("reporter-private");
			}
			public override void Write(string value)
			{
				Attempts++;
				base.Write(value);
				if (_throwOnWrite) throw new InvalidOperationException("reporter-private");
			}
			public override void WriteLine(string value)
			{
				Attempts++;
				base.Write(value);
				if (_throwOnWrite) throw new InvalidOperationException("reporter-private");
				base.WriteLine();
			}
		}

		private static byte[] CompletedBytes(ObservedFileDestination destination, Encoding encoding)
		{
			return encoding.GetBytes(string.Concat(destination.Records.Select(record => record + Environment.NewLine)));
		}

		private sealed class CompletedRecordObserver : TextBasedDestination
		{
			public readonly List<string> Records = new List<string>();
			public CompletedRecordObserver() : base(LogLevels.Trace) { }
			protected override void PrintLogEntry(string message) { Records.Add(message); }
		}

		private sealed class ObservedFileDestination : FileDestination
		{
			public readonly ConcurrentQueue<string> Records = new ConcurrentQueue<string>();
			public int MassageCalls;
			public Action BeforeMassage { get; set; }
			public ObservedFileDestination(string path, LogLevels reportingLevel = LogLevels.Trace, bool resetFile = false, EncodingOptions encoder = EncodingOptions.UTF8)
				: base(path, reportingLevel, resetFile, encoder) { }
			protected override string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
			{
				Interlocked.Increment(ref MassageCalls);
				if (BeforeMassage != null)
					BeforeMassage();
				return base.MassageLogStatement(level, message, ex);
			}
			protected override void PrintLogEntry(string message)
			{
				Records.Enqueue(message);
				base.PrintLogEntry(message);
			}
		}
	}
}