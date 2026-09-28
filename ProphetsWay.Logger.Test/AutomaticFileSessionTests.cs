using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class AutomaticFileSessionTests
	{
		private static readonly DateTime AllocationTime = new DateTime(2031, 2, 3, 4, 5, 6, DateTimeKind.Utc).AddTicks(1234567);
		private static readonly Guid FirstToken = new Guid("01234567-89ab-cdef-0123-456789abcdef");
		private static readonly Guid SecondToken = new Guid("abcdef01-2345-6789-abcd-ef0123456789");
		private static readonly Type[] DependencyTypes =
		{
			typeof(Func<string>), typeof(Func<string>), typeof(Func<DateTime>), typeof(Func<Guid>),
			typeof(Action<string>), typeof(Func<string, FileMode, FileAccess, FileShare, Stream>)
		};

		[Fact]
		public void ShouldExposeTheExactInternalBoundaryWithoutCallingDefaultOutput()
		{
			var type = SessionType();
			type.IsSealed.ShouldBeTrue();
			var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
			type.GetConstructors(flags).Where(candidateConstructor => !candidateConstructor.IsPrivate).Count().ShouldBe(2);
			var constructor = type.GetConstructor(flags, null, Type.EmptyTypes, null);
			constructor.ShouldNotBeNull();
			constructor.IsAssembly.ShouldBeTrue();
			Unwrap(() => constructor.Invoke(new object[0])).GetType().ShouldBe(type);
			var injected = type.GetConstructor(flags, null, DependencyTypes, null);
			injected.ShouldNotBeNull();
			injected.IsAssembly.ShouldBeTrue();
			injected.GetParameters().Select(parameter => parameter.Name).ShouldBe(new[]
			{
				"hostDirectoryProvider", "localApplicationDataProvider", "utcNowProvider", "tokenProvider", "createDirectory", "openFile"
			});
			type.GetMethods(flags).Where(method => !method.IsPrivate).Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal)
				.ShouldBe(new[] { "ConfigureHostDirectory", "TryWriteRecord" });
			Operation("ConfigureHostDirectory").ReturnType.ShouldBe(typeof(void));
			Operation("TryWriteRecord").ReturnType.ShouldBe(typeof(bool));
			type.GetFields(flags).Where(field => !field.IsPrivate).ShouldBeEmpty();
			type.GetProperties(flags).Where(property => property.GetAccessors(true).Any(accessor => !accessor.IsPrivate)).ShouldBeEmpty();
			type.GetEvents(flags).Where(item => !item.GetAddMethod(true).IsPrivate).ShouldBeEmpty();
		}

		[Fact]
		public void ShouldInvokeNoDependencyDuringConstructionOrConfiguration()
		{
			var calls = 0;
			var owner = Construct(InertDependencies(() => calls++));
			Configure(owner, "first configuration");
			Configure(owner, "second configuration");
			Configure(owner, "second configuration");
			calls.ShouldBe(0);
		}

		[Theory]
		[InlineData(0, "hostDirectoryProvider")]
		[InlineData(1, "localApplicationDataProvider")]
		[InlineData(2, "utcNowProvider")]
		[InlineData(3, "tokenProvider")]
		[InlineData(4, "createDirectory")]
		[InlineData(5, "openFile")]
		public void ShouldRejectEachMissingDependencyWithoutInvokingTheOthers(int index, string parameter)
		{
			var calls = 0;
			var dependencies = InertDependencies(() => calls++);
			dependencies[index] = null;
			Should.Throw<ArgumentNullException>(() => Construct(dependencies)).ParamName.ShouldBe(parameter);
			calls.ShouldBe(0);
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("invalid\0directory")]
		public void ShouldPreserveTheLastConfigurationAfterALocalArgumentError(string invalid)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var owner = Create(fixture, open: (path, mode, access, share) =>
				{
					requests.Add(Tuple.Create(path, mode, access, share));
					return fixture.Open(path, mode, access, share);
				});
				Configure(owner, fixture.PrimaryDirectory);
				var failure = Record.Exception(() => Configure(owner, invalid));
				if (invalid == null)
					failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("directory");
				else
					failure.ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("directory");
				Directory.Exists(fixture.PrimaryDirectory).ShouldBeFalse();
				requests.ShouldBeEmpty();
				Write(owner, "retained").ShouldBeTrue();
				requests.ShouldBe(new[] { Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew) });
				fixture.ReadAllBytes(requests[0].Item1).ShouldBe(Bytes("retained"));
			}
		}

		[Fact]
		public void ShouldReplaceConfigurationAndFreezeItsCallTimeRelativeNormalization()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var firstDirectory = fixture.GetPath("cwd-a");
				var secondDirectory = fixture.GetPath("cwd-b");
				fixture.CreateDirectory(firstDirectory);
				fixture.CreateDirectory(secondDirectory);
				var expectedRoot = fixture.GetPath("cwd-a/logs");
				var prepared = new List<string>();
				var requests = new List<string>();
				var owner = Create(fixture, prepare: path => { prepared.Add(path); fixture.CreateDirectory(path); },
					open: (path, mode, access, share) => { requests.Add(path); return fixture.Open(path, mode, access, share); });
				var previousDirectory = Environment.CurrentDirectory;
				try
				{
					Configure(owner, fixture.PrimaryDirectory);
					Environment.CurrentDirectory = firstDirectory;
					Configure(owner, Path.Combine("staging", "..", "logs"));
					Configure(owner, Path.Combine(".", "logs"));
					prepared.ShouldBeEmpty();
					requests.ShouldBeEmpty();
					Directory.Exists(expectedRoot).ShouldBeFalse();
					Environment.CurrentDirectory = secondDirectory;
					Write(owner, "first").ShouldBeTrue();
					Should.Throw<InvalidOperationException>(() => Configure(owner, expectedRoot));
					Should.Throw<InvalidOperationException>(() => Configure(owner, fixture.PrimaryDirectory));
					Write(owner, "second").ShouldBeTrue();
					prepared.ShouldBe(new[] { expectedRoot });
					requests.ShouldBe(new[] { Candidate(expectedRoot, FirstToken), Candidate(expectedRoot, FirstToken) });
					fixture.ReadAllBytes(requests[0]).ShouldBe(Bytes("first").Concat(Bytes("second")).ToArray());
					Directory.Exists(fixture.PrimaryDirectory).ShouldBeFalse();
					Directory.GetFileSystemEntries(secondDirectory).ShouldBeEmpty();
				}
				finally
				{
					Environment.CurrentDirectory = previousDirectory;
				}
			}
		}

		[Fact]
		public void ShouldNotTrimConfigurationBeforeBclNormalization()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				fixture.CreateDirectory(fixture.PrimaryDirectory);
				var previousDirectory = Environment.CurrentDirectory;
				try
				{
					Environment.CurrentDirectory = fixture.PrimaryDirectory;
					const string supplied = "  session directory";
					var expected = new DirectoryInfo(supplied).FullName;
					var prepared = new List<string>();
					var owner = Construct(new object[]
					{
						new Func<string>(() => null), new Func<string>(() => { throw new InvalidOperationException("local must remain unused"); }),
						new Func<DateTime>(() => AllocationTime), new Func<Guid>(() => FirstToken),
						new Action<string>(path => { prepared.Add(path); throw new DirectoryNotFoundException("synthetic preparation failure"); }),
						new Func<string, FileMode, FileAccess, FileShare, Stream>((path, mode, access, share) => { throw new InvalidOperationException("no prepared root"); })
					});
					Configure(owner, supplied);
					prepared.ShouldBeEmpty();
					Write(owner, "not stored").ShouldBeFalse();
					prepared.ShouldBe(new[] { expected });
					Directory.GetFileSystemEntries(fixture.PrimaryDirectory).ShouldBeEmpty();
				}
				finally { Environment.CurrentDirectory = previousDirectory; }
			}
		}

		[Fact]
		public void ShouldCaptureHostOnceAndUseItsKeyInsteadOfTheOverrideForRecovery()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var host = fixture.GetPath("host");
				var expectedSecondary = Secondary(fixture.LocalRoot, host);
				var calls = new List<string>();
				var prepared = new List<string>();
				var requests = new List<string>();
				var owner = Create(fixture, host: () => { calls.Add("host"); return host; },
					local: () => { calls.Add("local"); return fixture.LocalRoot; },
					prepare: path =>
					{
						prepared.Add(path);
						if (path == fixture.PrimaryDirectory) throw new UnauthorizedAccessException("synthetic primary failure");
						fixture.CreateDirectory(path);
					},
					open: (path, mode, access, share) => { requests.Add(path); return fixture.Open(path, mode, access, share); });
				Configure(owner, fixture.PrimaryDirectory);
				Write(owner, "recovered").ShouldBeTrue();
				host = fixture.GetPath("changed-host");
				Write(owner, "later").ShouldBeTrue();
				calls.ShouldBe(new[] { "host", "local" });
				prepared.ShouldBe(new[] { fixture.PrimaryDirectory, expectedSecondary });
				requests.ShouldBe(new[] { Candidate(expectedSecondary, FirstToken), Candidate(expectedSecondary, FirstToken) });
				fixture.ReadAllBytes(requests[0]).ShouldBe(Bytes("recovered").Concat(Bytes("later")).ToArray());
			}
		}

		[Theory]
		[InlineData("null")]
		[InlineData("relative")]
		[InlineData("throws")]
		public void ShouldEstablishAnOverrideDespiteAnUnavailableHostWithoutReadingLocalData(string unavailable)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var hostCalls = 0;
				var localCalls = 0;
				var owner = Create(fixture, host: () =>
				{
					hostCalls++;
					if (unavailable == "throws") throw new IOException("synthetic host failure");
					return unavailable == "null" ? null : "relative";
				}, local: () => { localCalls++; return fixture.LocalRoot; });
				Configure(owner, fixture.PrimaryDirectory);
				Write(owner, "override").ShouldBeTrue();
				Write(owner, "again").ShouldBeTrue();
				hostCalls.ShouldBe(1);
				localCalls.ShouldBe(0);
				fixture.ReadAllBytes(Candidate(fixture.PrimaryDirectory, FirstToken)).ShouldBe(Bytes("override").Concat(Bytes("again")).ToArray());
			}
		}

		[Theory]
		[InlineData("null")]
		[InlineData("relative")]
		[InlineData("unpaired")]
		public void ShouldRememberUnavailableSecondaryNamingWithoutReadingItsLocalRoot(string unavailable)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var hostCalls = 0;
				var localCalls = 0;
				var prepareCalls = 0;
				var openCalls = 0;
				var owner = Create(fixture, host: ()
					=> { hostCalls++; return unavailable == "null" ? null : unavailable == "relative" ? "relative" : fixture.PrimaryDirectory + "\ud800"; },
					local: () => { localCalls++; return fixture.LocalRoot; },
					prepare: path => { prepareCalls++; throw new IOException("synthetic preparation failure"); },
					open: (path, mode, access, share) => { openCalls++; return fixture.Open(path, mode, access, share); });
				Configure(owner, fixture.PrimaryDirectory);
				Write(owner, "first rejected").ShouldBeFalse();
				Write(owner, "second rejected").ShouldBeFalse();
				hostCalls.ShouldBe(1);
				localCalls.ShouldBe(0);
				prepareCalls.ShouldBe(1);
				openCalls.ShouldBe(0);
			}
		}

		[Fact]
		public void ShouldRememberDoubleFailureWithoutProbesAndAllowAnIndependentOwnerToEstablish()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var calls = new List<string>();
				var requests = new List<string>();
				var owner = Create(fixture,
					host: () => { calls.Add("host"); return fixture.PrimaryDirectory; },
					local: () => { calls.Add("local"); return fixture.LocalRoot; },
					clock: () => { calls.Add("clock"); return AllocationTime; },
					token: () => { calls.Add("token"); return FirstToken; },
					prepare: path => { calls.Add("prepare"); fixture.CreateDirectory(path); },
					open: (path, mode, access, share) => { calls.Add("open"); requests.Add(path); throw new IOException("synthetic reservation failure"); });
				Write(owner, "never stored").ShouldBeFalse();
				requests.ShouldBe(new[] { Candidate(fixture.PrimaryDirectory, FirstToken), Candidate(Secondary(fixture.LocalRoot, fixture.PrimaryDirectory), FirstToken) });
				var frozenCalls = calls.ToArray();
				Write(owner, "not retried").ShouldBeFalse();
				Should.Throw<InvalidOperationException>(() => Configure(owner, fixture.PrimaryDirectory));
				calls.ShouldBe(frozenCalls);
				var fresh = Create(fixture);
				Write(fresh, "independent").ShouldBeTrue();
				fixture.ReadAllBytes(Candidate(fixture.PrimaryDirectory, FirstToken)).ShouldBe(Bytes("independent"));
				Write(owner, "still rejected").ShouldBeFalse();
				calls.ShouldBe(frozenCalls);
			}
		}

		[Fact]
		public void ShouldReuseOneUtcAcrossARealCollisionAndSecondaryRecoveryWithoutClobbering()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var sentinel = new byte[] { 0, 255, 17, 128 };
				fixture.CreateDirectory(fixture.PrimaryDirectory);
				var occupied = Candidate(fixture.PrimaryDirectory, Guid.Empty);
				fixture.Seed(occupied, sentinel);
				var tokens = new Queue<Guid>(new[] { Guid.Empty, FirstToken, SecondToken });
				var clockCalls = 0;
				var tokenCalls = 0;
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var owner = Create(fixture, clock: () => { clockCalls++; return AllocationTime; },
					token: () => { tokenCalls++; return tokens.Dequeue(); },
					open: (path, mode, access, share) =>
					{
						requests.Add(Tuple.Create(path, mode, access, share));
						if (path == Candidate(fixture.PrimaryDirectory, FirstToken)) throw new UnauthorizedAccessException("synthetic second primary failure");
						return fixture.Open(path, mode, access, share);
					});
				Write(owner, "recovered once").ShouldBeTrue();
				var secondary = Secondary(fixture.LocalRoot, fixture.PrimaryDirectory);
				requests.ShouldBe(new[]
				{
					Request(fixture.PrimaryDirectory, Guid.Empty, FileMode.CreateNew),
					Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew),
					Request(secondary, SecondToken, FileMode.CreateNew)
				});
				clockCalls.ShouldBe(1);
				tokenCalls.ShouldBe(3);
				fixture.ReadAllBytes(occupied).ShouldBe(sentinel);
				fixture.ReadAllBytes(Candidate(secondary, SecondToken)).ShouldBe(Bytes("recovered once"));
			}
		}

		[Theory]
		[InlineData(unchecked((int)0x80070050), true)]
		[InlineData(unchecked((int)0x800700B7), true)]
		[InlineData(unchecked((int)0x80131620), false)]
		[InlineData(unchecked((int)0x80130050), false)]
		[InlineData(0x000000B7, false)]
		public void ShouldRetryOnlyPositivelyIdentifiedWindowsCollisionCodes(int errorCode, bool collision)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var tokens = new Queue<Guid>(new[] { FirstToken, SecondToken });
				var localCalls = 0;
				var owner = Create(fixture, local: () => { localCalls++; return fixture.LocalRoot; }, token: () => tokens.Dequeue(),
					open: (path, mode, access, share) =>
					{
						requests.Add(Tuple.Create(path, mode, access, share));
						if (requests.Count == 1) throw new IOException("already exists: synthetic misleading message", errorCode);
						return fixture.Open(path, mode, access, share);
					});
				Write(owner, "classified").ShouldBeTrue();
				var chosenRoot = collision ? fixture.PrimaryDirectory : Secondary(fixture.LocalRoot, fixture.PrimaryDirectory);
				requests.ShouldBe(new[] { Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew), Request(chosenRoot, SecondToken, FileMode.CreateNew) });
				localCalls.ShouldBe(collision ? 0 : 1);
				fixture.ReadAllBytes(Candidate(chosenRoot, SecondToken)).ShouldBe(Bytes("classified"));
			}
		}

		[Fact]
		public void ShouldResolveSecondaryAfterAClockFailureWithoutResamplingOrOpeningAFile()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var calls = new List<string>();
				var opens = 0;
				var owner = Create(fixture, host: () => { calls.Add("host"); return fixture.PrimaryDirectory; },
					local: () => { calls.Add("local"); return fixture.LocalRoot; },
					clock: () => { calls.Add("clock"); throw new IOException("synthetic clock failure"); },
					token: () => { calls.Add("token"); return FirstToken; },
					prepare: path => { calls.Add("prepare"); fixture.CreateDirectory(path); },
					open: (path, mode, access, share) => { opens++; return fixture.Open(path, mode, access, share); });
				Write(owner, "no allocation time").ShouldBeFalse();
				calls.Count(call => call == "clock").ShouldBe(1);
				calls.Count(call => call == "host").ShouldBe(1);
				calls.Count(call => call == "local").ShouldBe(1);
				opens.ShouldBe(0);
				var frozenCalls = calls.ToArray();
				Write(owner, "no retry").ShouldBeFalse();
				calls.ShouldBe(frozenCalls);
				opens.ShouldBe(0);
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldTreatTokenFailuresAsInitialRootFailures(bool bothFail)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var clockCalls = 0;
				var tokenCalls = 0;
				var localCalls = 0;
				var requests = new List<string>();
				var owner = Create(fixture, clock: () => { clockCalls++; return AllocationTime; },
					local: () => { localCalls++; return fixture.LocalRoot; },
					token: () => { tokenCalls++; if (tokenCalls == 1 || bothFail) throw new IOException("synthetic token failure"); return FirstToken; },
					open: (path, mode, access, share) => { requests.Add(path); return fixture.Open(path, mode, access, share); });
				Write(owner, "token recovery").ShouldBe(!bothFail);
				clockCalls.ShouldBe(1);
				tokenCalls.ShouldBe(2);
				localCalls.ShouldBe(1);
				if (bothFail)
				{
					requests.ShouldBeEmpty();
					Write(owner, "remembered").ShouldBeFalse();
					tokenCalls.ShouldBe(2);
				}
				else
				{
					requests.ShouldBe(new[] { Candidate(Secondary(fixture.LocalRoot, fixture.PrimaryDirectory), FirstToken) });
					fixture.ReadAllBytes(requests[0]).ShouldBe(Bytes("token recovery"));
				}
			}
		}

		[Fact]
		public void ShouldLeaveAnIncidentalFileWhenTheOpenerClosesItsHandleAndThenThrows()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var requests = new List<string>();
				var owner = Create(fixture, open: (path, mode, access, share) =>
				{
					requests.Add(path);
					var stream = fixture.Open(path, mode, access, share);
					if (requests.Count != 1) return stream;
					stream.Dispose();
					throw new IOException("synthetic failure before ownership transfer");
				});
				Write(owner, "secondary only").ShouldBeTrue();
				requests.ShouldBe(new[] { Candidate(fixture.PrimaryDirectory, FirstToken), Candidate(Secondary(fixture.LocalRoot, fixture.PrimaryDirectory), FirstToken) });
				fixture.ReadAllBytes(requests[0]).ShouldBeEmpty();
				fixture.ReadAllBytes(requests[1]).ShouldBe(Bytes("secondary only"));
			}
		}

		[Theory]
		[InlineData("")]
		[InlineData("line\r\nline\t\"\\")]
		[InlineData("\u00e9\U0001d11e\ud800\0\u2028")]
		public void ShouldWriteExactRepeatedUtf8SuffixesThroughOneOwnedHandlePerAttempt(string record)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var streams = new List<AutomaticFileSessionFixture.ObservedStream>();
				var localCalls = 0;
				var owner = Create(fixture, local: () => { localCalls++; return fixture.LocalRoot; },
					open: (path, mode, access, share) =>
					{
						requests.Add(Tuple.Create(path, mode, access, share));
						var stream = fixture.Open(path, mode, access, share);
						streams.Add(stream);
						return stream;
					});
				Write(owner, record).ShouldBeTrue();
				Write(owner, record).ShouldBeTrue();
				requests.ShouldBe(new[] { Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew), Request(fixture.PrimaryDirectory, FirstToken, FileMode.OpenOrCreate) });
				fixture.ReadAllBytes(requests[0].Item1).ShouldBe(Bytes(record).Concat(Bytes(record)).ToArray());
				localCalls.ShouldBe(0);
				foreach (var stream in streams)
				{
					stream.SeekCalls.ShouldBeGreaterThan(0);
					stream.WriteCalls.ShouldBeGreaterThan(0);
					stream.FlushCalls.ShouldBeGreaterThan(0);
					stream.ReadCalls.ShouldBe(0);
					stream.DisposeCalls.ShouldBe(1);
					stream.HandleClosed.ShouldBeTrue();
				}
			}
		}

		[Fact]
		public void ShouldAppendToOpaqueReplacementAndRecreateOnlyTheSelectedPath()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var prepared = new List<string>();
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var allocations = 0;
				var owner = Create(fixture, token: () => { allocations++; return FirstToken; },
					prepare: path => { prepared.Add(path); fixture.CreateDirectory(path); },
					open: (path, mode, access, share) => { requests.Add(Tuple.Create(path, mode, access, share)); return fixture.Open(path, mode, access, share); });
				var selected = Candidate(fixture.PrimaryDirectory, FirstToken);
				Write(owner, "original").ShouldBeTrue();
				fixture.DeleteFile(selected);
				var opaque = new byte[] { 239, 187, 191, 255, 0, 128, 65 };
				fixture.Seed(selected, opaque);
				Write(owner, "appended").ShouldBeTrue();
				fixture.ReadAllBytes(selected).ShouldBe(opaque.Concat(Bytes("appended")).ToArray());
				fixture.DeleteFile(selected);
				Write(owner, "recreated").ShouldBeTrue();
				fixture.ReadAllBytes(selected).ShouldBe(Bytes("recreated"));
				prepared.ShouldBe(new[] { fixture.PrimaryDirectory });
				allocations.ShouldBe(1);
				requests.ShouldBe(new[]
				{
					Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew),
					Request(fixture.PrimaryDirectory, FirstToken, FileMode.OpenOrCreate),
					Request(fixture.PrimaryDirectory, FirstToken, FileMode.OpenOrCreate)
				});
			}
		}

		[Theory]
		[InlineData("Seek")]
		[InlineData("Write")]
		[InlineData("Flush")]
		[InlineData("Dispose")]
		[InlineData("WriteAndDispose")]
		public void ShouldPreserveSelectionAndNeverReplayAfterAnActualStreamEffectThenFailure(string phase)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var streams = new List<AutomaticFileSessionFixture.ObservedStream>();
				var failure = new IOException("synthetic record boundary failure", unchecked((int)0x80070050));
				var allocations = 0;
				var localCalls = 0;
				var owner = Create(fixture, token: () => { allocations++; return FirstToken; },
					local: () => { localCalls++; return fixture.LocalRoot; },
					open: (path, mode, access, share) =>
					{
						requests.Add(Tuple.Create(path, mode, access, share));
						var stream = fixture.Open(path, mode, access, share);
						streams.Add(stream);
						if (streams.Count == 1)
							stream.AfterOperation = operation =>
							{
								var targeted = operation == phase || phase == "WriteAndDispose" && (operation == "Write" || operation == "Dispose");
								if (targeted && (operation != "Write" && operation != "Flush" || stream.Position == Bytes("uncertain").Length))
									throw failure;
							};
						return stream;
					});
				Record.Exception(() => Write(owner, "uncertain")).ShouldBeSameAs(failure);
				requests.Count.ShouldBe(1);
				streams[0].DisposeCalls.ShouldBe(1);
				streams[0].HandleClosed.ShouldBeTrue();
				var retained = phase == "Seek" ? new byte[0] : Bytes("uncertain");
				var selected = Candidate(fixture.PrimaryDirectory, FirstToken);
				fixture.ReadAllBytes(selected).ShouldBe(retained);
				Write(owner, "independent later record").ShouldBeTrue();
				fixture.ReadAllBytes(selected).ShouldBe(retained.Concat(Bytes("independent later record")).ToArray());
				requests.ShouldBe(new[] { Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew), Request(fixture.PrimaryDirectory, FirstToken, FileMode.OpenOrCreate) });
				allocations.ShouldBe(1);
				localCalls.ShouldBe(0);
				streams[1].DisposeCalls.ShouldBe(1);
				streams[1].HandleClosed.ShouldBeTrue();
			}
		}

		[Fact]
		public void ShouldPropagateALaterOpenFailureWithoutReturningFalseOrReselecting()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var prepared = new List<string>();
				var failure = new IOException("synthetic later open failure");
				var localCalls = 0;
				var owner = Create(fixture, local: () => { localCalls++; return fixture.LocalRoot; },
					prepare: path => { prepared.Add(path); fixture.CreateDirectory(path); },
					open: (path, mode, access, share) =>
					{
						requests.Add(Tuple.Create(path, mode, access, share));
						if (requests.Count == 2) throw failure;
						return fixture.Open(path, mode, access, share);
					});
				Write(owner, "first").ShouldBeTrue();
				Record.Exception(() => Write(owner, "not opened")).ShouldBeSameAs(failure);
				Write(owner, "third").ShouldBeTrue();
				requests.ShouldBe(new[]
				{
					Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew),
					Request(fixture.PrimaryDirectory, FirstToken, FileMode.OpenOrCreate),
					Request(fixture.PrimaryDirectory, FirstToken, FileMode.OpenOrCreate)
				});
				prepared.ShouldBe(new[] { fixture.PrimaryDirectory });
				localCalls.ShouldBe(0);
				fixture.ReadAllBytes(requests[0].Item1).ShouldBe(Bytes("first").Concat(Bytes("third")).ToArray());
			}
		}

		[Fact]
		public void ShouldNotRepairAMissingSelectedParentOrUseSecondary()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var preparations = 0;
				var localCalls = 0;
				var owner = Create(fixture, local: () => { localCalls++; return fixture.LocalRoot; },
					prepare: path => { preparations++; fixture.CreateDirectory(path); });
				var selected = Candidate(fixture.PrimaryDirectory, FirstToken);
				Write(owner, "before parent removal").ShouldBeTrue();
				fixture.DeleteFile(selected);
				fixture.DeleteDirectory(fixture.PrimaryDirectory);
				Should.Throw<DirectoryNotFoundException>(() => Write(owner, "missing parent"));
				Directory.Exists(fixture.PrimaryDirectory).ShouldBeFalse();
				preparations.ShouldBe(1);
				localCalls.ShouldBe(0);
				fixture.CreateDirectory(fixture.PrimaryDirectory);
				Write(owner, "after external parent recreation").ShouldBeTrue();
				fixture.ReadAllBytes(selected).ShouldBe(Bytes("after external parent recreation"));
				preparations.ShouldBe(1);
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldSerializeConcurrentFirstRecordsThroughDisposalEvenWhenTheFirstCloseThrows(bool firstCloseFails)
		{
			using (var fixture = new AutomaticFileSessionFixture())
			using (var started = new CountdownEvent(4))
			using (var closing = new ManualResetEventSlim())
			using (var overlappingOpen = new ManualResetEventSlim())
			using (var release = new ManualResetEventSlim())
			{
				var requests = new List<Tuple<string, FileMode, FileAccess, FileShare>>();
				var streams = new List<AutomaticFileSessionFixture.ObservedStream>();
				var observationGate = new object();
				var hostCalls = 0;
				var tokenCalls = 0;
				var closeCalls = 0;
				var failure = new IOException("synthetic first close failure");
				var records = Enumerable.Range(0, 4).Select(index => "worker-" + index + ":" + new string((char)('A' + index), 8192)).ToArray();
				var results = new bool[records.Length];
				var owner = Create(fixture, host: () => { Interlocked.Increment(ref hostCalls); return fixture.PrimaryDirectory; },
					token: () => { Interlocked.Increment(ref tokenCalls); return FirstToken; },
					open: (path, mode, access, share) =>
					{
						lock (observationGate)
						{
							requests.Add(Tuple.Create(path, mode, access, share));
							if (requests.Count > 1) overlappingOpen.Set();
						}
						var stream = fixture.Open(path, mode, access, share);
						lock (observationGate) streams.Add(stream);
						stream.AfterOperation = phase =>
						{
							if (phase != "Dispose" || Interlocked.Increment(ref closeCalls) != 1) return;
							closing.Set();
							if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Session close release was not signaled.");
							if (firstCloseFails) throw failure;
						};
						return stream;
					});
				var workers = records.Select((record, index) => new Action(() => { started.Signal(); results[index] = Write(owner, record); })).ToArray();
				var errors = fixture.RunWorkers(workers, () =>
				{
					try
					{
						started.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
						closing.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
						overlappingOpen.Wait(TimeSpan.FromMilliseconds(250)).ShouldBeFalse("A waiting writer must not open while disposal is still in progress.");
						lock (observationGate) requests.Count.ShouldBe(1, "The owner gate must include stream disposal.");
					}
					finally { release.Set(); }
				});
				errors.Length.ShouldBe(4);
				errors.Count(error => error != null).ShouldBe(firstCloseFails ? 1 : 0);
				if (firstCloseFails) errors.Single(error => error != null).ShouldBeSameAs(failure);
				results.Count(result => result).ShouldBe(firstCloseFails ? 3 : 4);
				hostCalls.ShouldBe(1);
				tokenCalls.ShouldBe(1);
				requests.Count.ShouldBe(4);
				requests[0].ShouldBe(Request(fixture.PrimaryDirectory, FirstToken, FileMode.CreateNew));
				requests.Skip(1).ShouldBe(Enumerable.Repeat(Request(fixture.PrimaryDirectory, FirstToken, FileMode.OpenOrCreate), 3));
				var bytes = fixture.ReadAllBytes(Candidate(fixture.PrimaryDirectory, FirstToken));
				bytes.Length.ShouldBe(records.Sum(record => Bytes(record).Length));
				var lines = Encoding.UTF8.GetString(bytes).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
				lines.Last().ShouldBe("");
				lines.Take(lines.Length - 1).OrderBy(line => line, StringComparer.Ordinal).ShouldBe(records.OrderBy(record => record, StringComparer.Ordinal));
				foreach (var stream in streams)
				{
					stream.DisposeCalls.ShouldBe(1);
					stream.HandleClosed.ShouldBeTrue();
				}
			}
		}

		[Fact]
		public void ShouldRejectConfigurationRacingAnAlreadyStartedInitializationWithoutChangingItsRoot()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			using (var entered = new ManualResetEventSlim())
			using (var configureStarted = new ManualResetEventSlim())
			using (var release = new ManualResetEventSlim())
			{
				var requests = new List<string>();
				var result = false;
				var owner = Create(fixture, host: () =>
				{
					entered.Set();
					if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Session initialization release was not signaled.");
					return fixture.PrimaryDirectory;
				}, open: (path, mode, access, share) => { requests.Add(path); return fixture.Open(path, mode, access, share); });
				Configure(owner, fixture.PrimaryDirectory);
				var replacement = fixture.GetPath("replacement");
				var errors = fixture.RunWorkers(new Action[]
				{
					() => { result = Write(owner, "frozen root"); },
					() =>
					{
						if (!entered.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Initialization did not start.");
						configureStarted.Set();
						Configure(owner, replacement);
					}
				}, () =>
				{
					try { configureStarted.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue(); }
					finally { release.Set(); }
				});
				errors.Length.ShouldBe(2);
				errors[0].ShouldBeNull();
				errors[1].ShouldBeOfType<InvalidOperationException>();
				result.ShouldBeTrue();
				requests.ShouldBe(new[] { Candidate(fixture.PrimaryDirectory, FirstToken) });
				fixture.ReadAllBytes(requests[0]).ShouldBe(Bytes("frozen root"));
				Directory.Exists(replacement).ShouldBeFalse();
			}
		}

		[Fact]
		public void ShouldNotKeepACaughtEstablishmentCauseAliveThroughOwnerState()
		{
			using (var fixture = new AutomaticFileSessionFixture())
			{
				var observation = EstablishFailureWithWeakCause(fixture.PrimaryDirectory);
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();
				observation.Item2.IsAlive.ShouldBeFalse("Remembered failure may retain bounded state, not the caught cause.");
				Write(observation.Item1, "remembered").ShouldBeFalse();
				GC.KeepAlive(observation.Item1);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private static Tuple<object, WeakReference> EstablishFailureWithWeakCause(string directory)
		{
			WeakReference cause = null;
			var owner = Construct(new object[]
			{
				new Func<string>(() => null), new Func<string>(() => null), new Func<DateTime>(() => AllocationTime), new Func<Guid>(() => FirstToken),
				new Action<string>(path =>
				{
					var failure = new IOException("synthetic raw cause canary");
					cause = new WeakReference(failure);
					throw failure;
				}),
				new Func<string, FileMode, FileAccess, FileShare, Stream>((path, mode, access, share) => { throw new InvalidOperationException("No prepared root."); })
			});
			Configure(owner, directory);
			Write(owner, "not retained").ShouldBeFalse();
			cause.ShouldNotBeNull();
			return Tuple.Create(owner, cause);
		}

		private static object Create(AutomaticFileSessionFixture fixture, Func<string> host = null, Func<string> local = null,
			Func<DateTime> clock = null, Func<Guid> token = null, Action<string> prepare = null,
			Func<string, FileMode, FileAccess, FileShare, Stream> open = null)
		{
			return Construct(new object[]
			{
				host ?? (() => fixture.PrimaryDirectory), local ?? (() => fixture.LocalRoot), clock ?? (() => AllocationTime), token ?? (() => FirstToken),
				prepare ?? new Action<string>(fixture.CreateDirectory), open ?? new Func<string, FileMode, FileAccess, FileShare, Stream>(fixture.Open)
			});
		}

		private static object[] InertDependencies(Action accessed)
		{
			return new object[]
			{
				new Func<string>(() => { accessed(); throw new InvalidOperationException("Unexpected host lookup."); }),
				new Func<string>(() => { accessed(); throw new InvalidOperationException("Unexpected local lookup."); }),
				new Func<DateTime>(() => { accessed(); throw new InvalidOperationException("Unexpected clock lookup."); }),
				new Func<Guid>(() => { accessed(); throw new InvalidOperationException("Unexpected token lookup."); }),
				new Action<string>(path => { accessed(); throw new InvalidOperationException("Unexpected directory preparation."); }),
				new Func<string, FileMode, FileAccess, FileShare, Stream>((path, mode, access, share) => { accessed(); throw new InvalidOperationException("Unexpected open."); })
			};
		}

		private static string Candidate(string root, Guid token)
		{
			return Path.Combine(root, "Default Log 2031-02-03 04-05-06.1234567Z-" + token.ToString("N") + ".log");
		}

		private static Tuple<string, FileMode, FileAccess, FileShare> Request(string root, Guid token, FileMode mode)
		{
			return Tuple.Create(Candidate(root, token), mode, FileAccess.Write, FileShare.None);
		}

		private static string Secondary(string localRoot, string host)
		{
			using (var sha256 = SHA256.Create())
			{
				var key = Path.GetFullPath(host).Replace('\\', '/');
				return Path.Combine(localRoot, "app-" + BitConverter.ToString(sha256.ComputeHash(new UTF8Encoding(false, true).GetBytes(key))).Replace("-", "").ToLowerInvariant());
			}
		}

		private static byte[] Bytes(string record)
		{
			return Encoding.UTF8.GetBytes(record + Environment.NewLine);
		}

		private static Type SessionType()
		{
			var type = typeof(Utilities.Logger).Assembly.GetType("ProphetsWay.Utilities.AutomaticFileSession", false);
			type.ShouldNotBeNull("B01-B12 require the actual internal AutomaticFileSession owner in the Logger assembly.");
			type.IsNotPublic.ShouldBeTrue();
			return type;
		}

		private static object Construct(object[] dependencies)
		{
			var constructor = SessionType().GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, DependencyTypes, null);
			constructor.ShouldNotBeNull("B12 requires the exact six-delegate constructor.");
			constructor.IsAssembly.ShouldBeTrue();
			return Unwrap(() => constructor.Invoke(dependencies));
		}

		private static MethodInfo Operation(string name)
		{
			var method = SessionType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, new[] { typeof(string) }, null);
			method.ShouldNotBeNull("B12 requires the reviewed internal operation: " + name);
			method.IsAssembly.ShouldBeTrue();
			return method;
		}

		private static void Configure(object owner, string directory)
		{
			Unwrap(() => Operation("ConfigureHostDirectory").Invoke(owner, new object[] { directory }));
		}

		private static bool Write(object owner, string record)
		{
			return (bool)Unwrap(() => Operation("TryWriteRecord").Invoke(owner, new object[] { record }));
		}

		private static object Unwrap(Func<object> invoke)
		{
			try { return invoke(); }
			catch (TargetInvocationException exception) when (exception.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
				throw;
			}
		}
	}
}