using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class AutomaticFileIntegrationTests
	{
		private static readonly DateTime AllocationTime = new DateTime(2031, 2, 3, 4, 5, 6, DateTimeKind.Utc);
		private static readonly Guid AllocationToken = new Guid("01234567-89ab-cdef-0123-456789abcdef");
		private static readonly string[] DependencyNames =
		{
			"_hostDirectoryProvider", "_localApplicationDataProvider", "_utcNowProvider",
			"_tokenProvider", "_createDirectory", "_openFile"
		};
		private static readonly Type[] DependencyTypes =
		{
			typeof(Func<string>), typeof(Func<string>), typeof(Func<DateTime>), typeof(Func<Guid>),
			typeof(Action<string>), typeof(Func<string, FileMode, FileAccess, FileShare, Stream>)
		};

		[Fact]
		public void ShouldKeepAssemblyCopiesAndPlatformDependenciesIsolated()
		{
			var firstAssembly = LoadProductCopy();
			var secondAssembly = LoadProductCopy();
			var firstLogger = ProductType(firstAssembly, "Logger");
			var secondLogger = ProductType(secondAssembly, "Logger");
			ReferenceEquals(firstAssembly, secondAssembly).ShouldBeFalse();
			ReferenceEquals(firstLogger, secondLogger).ShouldBeFalse();
			ReferenceEquals(firstLogger, typeof(Utilities.Logger)).ShouldBeFalse();
			ReferenceEquals(secondLogger, typeof(Utilities.Logger)).ShouldBeFalse();
			foreach (var name in new[] { "OrdinaryDestinations", "Destinations" })
			{
				var firstRegistry = StaticField(firstLogger, name);
				var secondRegistry = StaticField(secondLogger, name);
				firstRegistry.ShouldNotBeNull();
				secondRegistry.ShouldNotBeNull();
				ReferenceEquals(firstRegistry, secondRegistry).ShouldBeFalse();
				ReferenceEquals(firstRegistry, StaticField(typeof(Utilities.Logger), name)).ShouldBeFalse();
				((ICollection)firstRegistry).Count.ShouldBe(0);
				((ICollection)secondRegistry).Count.ShouldBe(0);
			}

			var originalCalls = new int[6];
			var replacementCalls = new int[6];
			var independentCalls = new int[6];
			var original = InertDependencies(originalCalls);
			var replacement = InertDependencies(replacementCalls);
			var independent = InertDependencies(independentCalls);
			var firstOwner = ConstructOwner(firstAssembly, original);
			var secondOwner = ConstructOwner(secondAssembly, independent);
			for (var index = 0; index < DependencyNames.Length; index++)
			{
				var field = OwnerField(firstOwner, DependencyNames[index]);
				field.IsInitOnly.ShouldBeTrue();
				field.IsStatic.ShouldBeFalse();
				field.FieldType.ShouldBe(DependencyTypes[index]);
				ReferenceEquals(field.GetValue(firstOwner), original[index]).ShouldBeTrue();
				field.SetValue(firstOwner, replacement[index]);
			}
			for (var index = 0; index < DependencyNames.Length; index++)
			{
				ReferenceEquals(OwnerField(firstOwner, DependencyNames[index]).GetValue(firstOwner), replacement[index]).ShouldBeTrue();
				ReferenceEquals(OwnerField(secondOwner, DependencyNames[index]).GetValue(secondOwner), independent[index]).ShouldBeTrue();
			}
			AssertUnusedOwner(firstOwner);
			AssertUnusedOwner(secondOwner);
			originalCalls.ShouldBe(new int[6]);
			replacementCalls.ShouldBe(new int[6]);
			independentCalls.ShouldBe(new int[6]);
			WriteOwner(firstOwner, "inert readiness").ShouldBeFalse();
			replacementCalls.ShouldBe(new[] { 1, 1, 1, 2, 2, 2 });
			originalCalls.ShouldBe(new int[6]);
			independentCalls.ShouldBe(new int[6]);
			AssertUnusedOwner(secondOwner);
			WriteOwner(firstOwner, "remembered readiness").ShouldBeFalse();
			replacementCalls.ShouldBe(new[] { 1, 1, 1, 2, 2, 2 });
			WriteOwner(secondOwner, "independent readiness").ShouldBeFalse();
			independentCalls.ShouldBe(new[] { 1, 1, 1, 2, 2, 2 });
			originalCalls.ShouldBe(new int[6]);
		}

		[Fact]
		public void ShouldExerciseDefaultDelegatesOnlyOnReservedOwnedPaths()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			{
				var owner = ConstructOwner(fixture.ProductAssembly, new object[0]);
				AssertUnusedOwner(owner);
				var host = (Func<string>)OwnerField(owner, DependencyNames[0]).GetValue(owner);
				var local = (Func<string>)OwnerField(owner, DependencyNames[1]).GetValue(owner);
				var clock = (Func<DateTime>)OwnerField(owner, DependencyNames[2]).GetValue(owner);
				var token = (Func<Guid>)OwnerField(owner, DependencyNames[3]).GetValue(owner);
				string.Equals(host(), AppContext.BaseDirectory, StringComparison.Ordinal).ShouldBeTrue();
				string.Equals(local(), Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.Ordinal).ShouldBeTrue();
				var before = DateTime.UtcNow;
				var sampled = clock();
				var after = DateTime.UtcNow;
				sampled.Kind.ShouldBe(DateTimeKind.Utc);
				sampled.ShouldBeInRange(before, after);
				token().ShouldNotBe(Guid.Empty);
				var prepare = (Action<string>)OwnerField(owner, DependencyNames[4]).GetValue(owner);
				var open = (Func<string, FileMode, FileAccess, FileShare, Stream>)OwnerField(owner, DependencyNames[5]).GetValue(owner);
				var directory = fixture.Files.GetPath("default-delegates");
				var path = fixture.Files.GetPath("default-delegates/probe.log");
				fixture.CreateDirectoryWith(prepare, directory);
				Directory.Exists(directory).ShouldBeTrue();
				Microsoft.Win32.SafeHandles.SafeFileHandle handle;
				using (var stream = fixture.OpenFileWith(open, path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
					var file = stream.ShouldBeOfType<FileStream>();
					handle = file.SafeFileHandle;
					file.CanWrite.ShouldBeTrue();
					file.CanRead.ShouldBeFalse();
					var bytes = Encoding.UTF8.GetBytes("default delegate bytes");
					file.Write(bytes, 0, bytes.Length);
					file.Flush();
				}
				handle.IsClosed.ShouldBeTrue();
				fixture.Files.ReadAllBytes(path).ShouldBe(Encoding.UTF8.GetBytes("default delegate bytes"));
				AssertUnusedOwner(owner);
			}
		}

		[Fact]
		public void ShouldShareNativeRecordsAcrossOrdinaryAndExactTypedRoutes()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			{
				var calls = new ConcurrentQueue<string>();
				var paths = new ConcurrentQueue<string>();
				Bind(fixture, calls, paths);
				var expected = new List<string>();
				var before = DateTimeOffset.UtcNow;
				const string suffix = " | entryLabels=[\"entry\"] | scopes=[{labels=[\"scope\"],properties=[(\"key\",\"value\")]}]";
				using (Scope(fixture, "scope", new[] { new KeyValuePair<string, object>("key", "value") }))
				{
					foreach (var mask in new[] { 1, 2, 4, 8, 16, 32, 9 })
					{
						Emit(fixture, null, null, "line\n\"", mask, Annotations(fixture, "entry"));
						expected.Add(Body(fixture, mask, "\"line\\n\\\"\"", suffix));
					}
					((ICollection)StaticField(ProductType(fixture.ProductAssembly, "Logger"), "OrdinaryDestinations")).Count.ShouldBe(0);
					var ordinary = Destination(fixture, null);
					var intercepted = new List<object>();
					using (fixture.ObserveDestination(ordinary, (sender, entry) => intercepted.Add(entry)))
					{
						Register(fixture, null, ordinary, Settings(fixture));
						Emit(fixture, typeof(string), "typed\nvalue", "typed", 32, Annotations(fixture, "entry"));
						Emit(fixture, typeof(string), null, null, 8, Annotations(fixture, "entry"));
						Emit(fixture, typeof(int), 0, "", 9, Annotations(fixture, "entry"));
						intercepted.ShouldBeEmpty();
					}
					expected.Add(Body(fixture, 32, "\"typed\"", " | metadata=\"typed\\nvalue\"" + suffix));
					expected.Add(Body(fixture, 8, "null", " | metadata=null" + suffix));
					expected.Add(Body(fixture, 9, "\"\"", " | metadata=\"0\"" + suffix));
				}
				var path = paths.Distinct().Single();
				Path.GetDirectoryName(path).ShouldBe(fixture.Files.PrimaryDirectory);
				Path.GetFileName(path).ShouldBe("Default Log 2031-02-03 04-05-06.0000000Z-0123456789abcdef0123456789abcdef.log");
				AssertRecords(fixture, path, expected.ToArray(), before, DateTimeOffset.UtcNow);
				paths.Count.ShouldBe(expected.Count);
				Count(calls, "host").ShouldBe(1);
				Count(calls, "local").ShouldBe(0);
				Count(calls, "clock").ShouldBe(1);
				Count(calls, "token").ShouldBe(1);
				var registry = (IDictionary)StaticField(ProductType(fixture.ProductAssembly, "Logger"), "Destinations");
				registry.Values.Cast<ICollection>().All(registrations => registrations.Count == 0).ShouldBeTrue();
			}
		}

		[Fact]
		public void ShouldSuppressOnlyTheEnabledRouteWithoutRescuingRejectionOrFailure()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var stderr = new StringWriter())
			{
				var previousError = Console.Error;
				Console.SetError(stderr);
				try
				{
					var calls = new ConcurrentQueue<string>();
					var paths = new ConcurrentQueue<string>();
					Bind(fixture, calls, paths);
					var ordinary = Destination(fixture, null);
					var typed = Destination(fixture, typeof(string));
					Register(fixture, null, ordinary, Settings(fixture, mask: 0));
					Register(fixture, typeof(string), typed, Settings(fixture, mode: "Exclude", label: "blocked"));
					var delivered = new List<object>();
					using (fixture.ObserveDestination(ordinary, (sender, entry) => delivered.Add(entry)))
					using (fixture.ObserveDestination(typed, (sender, entry) => delivered.Add(entry)))
					{
						Emit(fixture, null, null, "ordinary rejected");
						Emit(fixture, typeof(string), "secret", "label rejected", annotations: Annotations(fixture, "blocked"));
						calls.ShouldBeEmpty();
						delivered.ShouldBeEmpty();
						stderr.ToString().ShouldBe("");
						var before = DateTimeOffset.UtcNow;
						Emit(fixture, typeof(int), 7, "other route");
						SetSettings(fixture, null, ordinary, Settings(fixture, enabled: false));
						Emit(fixture, null, null, "disabled resumes");
						Remove(fixture, typeof(string), typed);
						Emit(fixture, typeof(string), "retained", "removed resumes");
						var path = paths.Distinct().Single();
						var retained = fixture.Files.ReadAllBytes(path);
						SetSettings(fixture, null, ordinary, Settings(fixture));
						using (fixture.ObserveDestination(ordinary, (sender, entry) => { throw new IOException("explicit output canary"); }))
						{
							var failure = Failure(fixture, () => Emit(fixture, null, null, "explicit failure"), 1);
							AssertGuidance(failure, false);
						}
						fixture.Files.ReadAllBytes(path).ShouldBe(retained);
						paths.Count.ShouldBe(3);
						Clear(fixture, null);
						Emit(fixture, null, null, "cleared resumes");
						AssertRecords(fixture, path, new[]
						{
							Body(fixture, 8, "\"other route\"", " | metadata=\"7\"" + EmptyContext),
							Body(fixture, 8, "\"disabled resumes\"", EmptyContext),
							Body(fixture, 8, "\"removed resumes\"", " | metadata=\"retained\"" + EmptyContext),
							Body(fixture, 8, "\"cleared resumes\"", EmptyContext)
						}, before, DateTimeOffset.UtcNow);
					}
				}
				finally { Console.SetError(previousError); }
			}
		}

		[Fact]
		public void ShouldKeepConfigurationLocalAndRenderingAheadOfEstablishment()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var stderr = new StringWriter())
			{
				var previousDirectory = Environment.CurrentDirectory;
				var previousError = Console.Error;
				Console.SetError(stderr);
				try
				{
					var calls = new ConcurrentQueue<string>();
					var paths = new ConcurrentQueue<string>();
					var reports = new List<object>();
					Bind(fixture, calls, paths);
					var firstDirectory = fixture.Files.GetPath("cwd-a");
					var secondDirectory = fixture.Files.GetPath("cwd-b");
					var selected = fixture.Files.GetPath("cwd-a/selected");
					fixture.Files.CreateDirectory(firstDirectory);
					fixture.Files.CreateDirectory(secondDirectory);
					using (fixture.SubscribeFailures(reports.Add))
					{
						Configure(fixture, fixture.Files.PrimaryDirectory);
						Environment.CurrentDirectory = firstDirectory;
						Configure(fixture, Path.Combine("staging", "..", "selected"));
						Should.Throw<ArgumentNullException>(() => Configure(fixture, null)).ParamName.ShouldBe("directory");
						Should.Throw<ArgumentException>(() => Configure(fixture, "")).ParamName.ShouldBe("directory");
						Should.Throw<ArgumentOutOfRangeException>(() => Emit(fixture, null, null, "invalid", 0)).ParamName.ShouldBe("level");
						Should.Throw<ArgumentNullException>(() => fixture.InvokePublic("Info", Type.EmptyTypes, new[] { typeof(string) }, new object[] { null })).ParamName.ShouldBe("message");
						var sequenceFailure = new IOException("scope enumeration canary");
						Should.Throw<IOException>(() => Scope(fixture, "unpublished", BrokenProperties(sequenceFailure))).ShouldBeSameAs(sequenceFailure);
						calls.ShouldBeEmpty();
						reports.ShouldBeEmpty();
						stderr.ToString().ShouldBe("");
						Directory.Exists(selected).ShouldBeFalse();
						var rendering = new MessageCallbackException(() => { throw new IOException("render canary"); });
						var failure = Failure(fixture, () => Emit(fixture, null, null, "unrendered", exception: rendering), 1);
						AssertGuidance(failure, false);
						calls.ShouldBeEmpty();
						reports.Count.ShouldBe(1);
						Environment.CurrentDirectory = secondDirectory;
						var before = DateTimeOffset.UtcNow;
						Emit(fixture, null, null, "last valid configuration");
						var path = paths.Single();
						Path.GetDirectoryName(path).ShouldBe(selected);
						Should.Throw<InvalidOperationException>(() => Configure(fixture, selected));
						Should.Throw<InvalidOperationException>(() => Configure(fixture, fixture.Files.PrimaryDirectory));
						reports.Count.ShouldBe(1);
						AssertRecords(fixture, path, new[] { Body(fixture, 8, "\"last valid configuration\"", EmptyContext) }, before, DateTimeOffset.UtcNow);
						Directory.GetFileSystemEntries(secondDirectory).ShouldBeEmpty();
					}
				}
				finally
				{
					Environment.CurrentDirectory = previousDirectory;
					Console.SetError(previousError);
				}
			}
		}

		[Fact]
		public void ShouldKeepCapturedAutomaticAndExplicitPlansDuringCallbackPublication()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var entered = new ManualResetEventSlim())
			using (var release = new ManualResetEventSlim())
			{
				var calls = new ConcurrentQueue<string>();
				var paths = new ConcurrentQueue<string>();
				Bind(fixture, calls, paths);
				var explicitDestination = Destination(fixture, typeof(string));
				var delivered = new List<object>();
				var renderingAt = default(DateTimeOffset);
				var before = DateTimeOffset.UtcNow;
				using (fixture.ObserveDestination(explicitDestination, (sender, entry) =>
				{
					delivered.Add(entry);
					Remove(fixture, typeof(string), explicitDestination);
				}))
				{
					var errors = fixture.Files.RunWorkers(new Action[]
					{
						() =>
						{
							IDisposable lateScope = null;
							using (Scope(fixture, "captured-scope", new KeyValuePair<string, object>[0]))
							{
								try
								{
									var exception = new MessageCallbackException(() =>
									{
										renderingAt = DateTimeOffset.UtcNow;
										lateScope = Scope(fixture, "late-scope", new KeyValuePair<string, object>[0]);
										entered.Set();
										if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("render release");
									});
									Emit(fixture, typeof(string), "captured metadata", "captured message", exception: exception);
								}
								finally { if (lateScope != null) lateScope.Dispose(); }
							}
						}
					}, () =>
					{
						try
						{
							entered.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
							Register(fixture, typeof(string), explicitDestination, Settings(fixture));
						}
						finally { release.Set(); }
					});
					errors.ShouldBe(new Exception[] { null });
					var path = paths.Single();
					var first = Records(fixture, path).Single();
					Timestamp(first).ShouldBeInRange(before, renderingAt);
					first.ShouldContain("captured message");
					first.ShouldContain(" | metadata=\"captured metadata\" | entryLabels=null | scopes=[{labels=[\"captured-scope\"],properties=[]}]");
					first.ShouldNotContain("late-scope");
					delivered.ShouldBeEmpty();
					Emit(fixture, typeof(string), "explicit metadata", "explicit self removal");
					delivered.Count.ShouldBe(1);
					Property(delivered.Single(), "RawMessage").ShouldBe("explicit self removal");
					Property(delivered.Single(), "Metadata").ShouldBe("explicit metadata");
					paths.Count.ShouldBe(1);
					Records(fixture, path).ShouldBe(new[] { first });
					Emit(fixture, typeof(string), "new capture", "automatic resumes");
					var resumed = Records(fixture, path);
					resumed.Length.ShouldBe(2);
					resumed[0].ShouldBe(first);
					RecordBody(resumed[1]).ShouldBe(Body(fixture, 8, "\"automatic resumes\"", " | metadata=\"new capture\"" + EmptyContext));
				}
			}
		}

		[Fact]
		public void ShouldCoordinateFirstUseWithoutBlockingRegistryPublicationOnFileIo()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var written = new ManualResetEventSlim())
			using (var release = new ManualResetEventSlim())
			using (var published = new ManualResetEventSlim())
			{
				var calls = new ConcurrentQueue<string>();
				var paths = new ConcurrentQueue<string>();
				var streams = new ConcurrentQueue<AutomaticFileSessionFixture.ObservedStream>();
				var blocked = 0;
				Bind(fixture, calls, paths, open: (path, mode, access, share) =>
				{
					var stream = fixture.Files.Open(path, mode, access, share);
					streams.Enqueue(stream);
					stream.AfterOperation = operation =>
					{
						if (operation == "Write" && Interlocked.CompareExchange(ref blocked, 1, 0) == 0)
						{
							written.Set();
							if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("write release");
						}
					};
					return stream;
				});
				var before = DateTimeOffset.UtcNow;
				var errors = fixture.Files.RunWorkers(new Action[]
				{
					() => Emit(fixture, null, null, "ordinary concurrent"),
					() => Emit(fixture, typeof(int), 42, "typed concurrent"),
					() =>
					{
						if (!written.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("write entry");
						Register(fixture, typeof(string), Destination(fixture, typeof(string)), Settings(fixture));
						Clear(fixture, typeof(string));
						published.Set();
					}
				}, () =>
				{
					try { published.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue(); }
					finally { release.Set(); }
				});
				errors.ShouldBe(new Exception[] { null, null, null });
				var records = Records(fixture, paths.Distinct().Single());
				records.Select(RecordBody).OrderBy(value => value, StringComparer.Ordinal).ShouldBe(new[]
				{
					Body(fixture, 8, "\"ordinary concurrent\"", EmptyContext),
					Body(fixture, 8, "\"typed concurrent\"", " | metadata=\"42\"" + EmptyContext)
				}.OrderBy(value => value, StringComparer.Ordinal));
				foreach (var record in records) Timestamp(record).ShouldBeInRange(before, DateTimeOffset.UtcNow);
				Count(calls, "host").ShouldBe(1);
				Count(calls, "clock").ShouldBe(1);
				Count(calls, "token").ShouldBe(1);
				Count(calls, "prepare").ShouldBe(1);
				Count(calls, "local").ShouldBe(0);
				streams.Count.ShouldBe(2);
				streams.All(stream => stream.HandleClosed).ShouldBeTrue();
			}
		}

		[Fact]
		public void ShouldRecoverInitiallyToTheHostAssociatedSecondaryWithoutReportingPrimaryFailure()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var stderr = new StringWriter())
			{
				var previousError = Console.Error;
				Console.SetError(stderr);
				try
				{
					var calls = new ConcurrentQueue<string>();
					var paths = new ConcurrentQueue<string>();
					var prepared = new List<string>();
					var reports = new List<object>();
					var host = fixture.Files.GetPath("host-key");
					var secondary = Secondary(fixture.Files.LocalRoot, host);
					Bind(fixture, calls, paths, host: () => host, prepare: directoryPath =>
					{
						prepared.Add(directoryPath);
						if (directoryPath == fixture.Files.PrimaryDirectory) throw new IOException("primary preparation canary");
						fixture.Files.CreateDirectory(directoryPath);
					});
					Configure(fixture, fixture.Files.PrimaryDirectory);
					var before = DateTimeOffset.UtcNow;
					using (fixture.SubscribeFailures(reports.Add))
					{
						Emit(fixture, null, null, "recovered");
						host = fixture.Files.GetPath("different-host");
						Emit(fixture, typeof(string), null, "continued");
					}
					reports.ShouldBeEmpty();
					stderr.ToString().ShouldBe("");
					prepared.ShouldBe(new[] { fixture.Files.PrimaryDirectory, secondary });
					var path = paths.Distinct().Single();
					Path.GetDirectoryName(path).ShouldBe(secondary);
					AssertRecords(fixture, path, new[]
					{
						Body(fixture, 8, "\"recovered\"", EmptyContext),
						Body(fixture, 8, "\"continued\"", " | metadata=null" + EmptyContext)
					}, before, DateTimeOffset.UtcNow);
					Count(calls, "host").ShouldBe(1);
					Count(calls, "local").ShouldBe(1);
					Count(calls, "clock").ShouldBe(1);
					Count(calls, "token").ShouldBe(1);
				}
				finally { Console.SetError(previousError); }
			}
		}

		[Fact]
		public void ShouldRememberDoubleFailureWithPositiveImplicitPositionsAndFreshReports()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var stderr = new StringWriter())
			{
				var previousError = Console.Error;
				Console.SetError(stderr);
				try
				{
					var calls = new ConcurrentQueue<string>();
					var paths = new ConcurrentQueue<string>();
					var available = false;
					Bind(fixture, calls, paths, open: (path, mode, access, share) =>
					{
						if (!available) throw new IOException("private allocation canary " + path);
						return fixture.Files.Open(path, mode, access, share);
					});
					foreach (var route in new[] { null, typeof(string) })
						for (var index = 0; index < 2; index++) Register(fixture, route, Destination(fixture, route), Settings(fixture, enabled: false));
					var notified = new List<object>();
					using (fixture.SubscribeFailures(notified.Add))
					{
						var first = Failure(fixture, () => Emit(fixture, null, null, "first private payload"), 3);
						AssertGuidance(first, true);
						paths.Count.ShouldBe(2);
						var frozen = calls.ToArray();
						available = true;
						var second = Failure(fixture, () => Emit(fixture, typeof(string), "private metadata", "remembered"), 3);
						var third = Failure(fixture, () => Emit(fixture, typeof(int), 0, "other route"), 1);
						AssertGuidance(second, true);
						AssertGuidance(third, true);
						Should.Throw<InvalidOperationException>(() => Configure(fixture, fixture.Files.PrimaryDirectory));
						calls.ToArray().ShouldBe(frozen);
						notified.Count.ShouldBe(3);
						new[] { first, second, third }.Select(error => Correlation(Property(error, "Report"))).Distinct().Count().ShouldBe(3);
						ReferenceEquals(first, second).ShouldBeFalse();
						ReferenceEquals(Property(first, "Report"), Property(second, "Report")).ShouldBeFalse();
						AssertReport(Property(first, "Report"), 3);
						for (var index = 0; index < notified.Count; index++)
							Correlation(notified[index]).ShouldBe(Correlation(Property(new[] { first, second, third }[index], "Report")));
					}
				}
				finally { Console.SetError(previousError); }
			}
		}

		[Fact]
		public void ShouldBypassRememberedFailureOnlyOnTheExplicitRouteAndStartFreshInAnotherCopy()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var stderr = new StringWriter())
			{
				var previousError = Console.Error;
				Console.SetError(stderr);
				try
				{
					var calls = new ConcurrentQueue<string>();
					var paths = new ConcurrentQueue<string>();
					Bind(fixture, calls, paths, prepare: path => { throw new IOException("unavailable roots"); });
					Failure(fixture, () => Emit(fixture, null, null, "initial failure"), 1);
					var frozen = calls.ToArray();
					var destination = Destination(fixture, null);
					var delivered = new List<object>();
					Register(fixture, null, destination, Settings(fixture, mask: 0));
					using (fixture.ObserveDestination(destination, (sender, entry) => delivered.Add(entry)))
					{
						var priorStderr = stderr.ToString();
						Emit(fixture, null, null, "deliberate rejection");
						delivered.ShouldBeEmpty();
						stderr.ToString().ShouldBe(priorStderr);
						Failure(fixture, () => Emit(fixture, typeof(string), "other", "still failed"), 1);
						SetSettings(fixture, null, destination, Settings(fixture));
						Emit(fixture, null, null, "healthy explicit");
						Property(delivered.Single(), "RawMessage").ShouldBe("healthy explicit");
						SetSettings(fixture, null, destination, Settings(fixture, enabled: false));
						AssertGuidance(Failure(fixture, () => Emit(fixture, null, null, "disabled"), 2), true);
						Remove(fixture, null, destination);
						Failure(fixture, () => Emit(fixture, null, null, "removed"), 1);
						Register(fixture, null, destination, Settings(fixture, mask: 0));
						Clear(fixture, null);
						Failure(fixture, () => Emit(fixture, null, null, "cleared"), 1);
					}
					calls.ToArray().ShouldBe(frozen);
					paths.ShouldBeEmpty();
					using (var fresh = new AutomaticFileIntegrationFixture())
					{
						ReferenceEquals(fresh.ProductAssembly, fixture.ProductAssembly).ShouldBeFalse();
						var freshPaths = new ConcurrentQueue<string>();
						Bind(fresh, new ConcurrentQueue<string>(), freshPaths);
						var before = DateTimeOffset.UtcNow;
						Emit(fresh, typeof(int), 7, "fresh loaded state");
						AssertRecords(fresh, freshPaths.Single(), new[] { Body(fresh, 8, "\"fresh loaded state\"", " | metadata=\"7\"" + EmptyContext) }, before, DateTimeOffset.UtcNow);
					}
					Failure(fixture, () => Emit(fixture, null, null, "original remains failed"), 1);
					calls.ToArray().ShouldBe(frozen);
				}
				finally { Console.SetError(previousError); }
			}
		}

		[Theory]
		[InlineData("Write")]
		[InlineData("Flush")]
		[InlineData("Dispose")]
		public void ShouldKeepTheSelectedPathAfterUncertainEffectsAndLaterOpenFailure(string failingOperation)
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var stderr = new StringWriter())
			{
				var previousError = Console.Error;
				var previousDirectory = Environment.CurrentDirectory;
				Console.SetError(stderr);
				try
				{
					var calls = new ConcurrentQueue<string>();
					var paths = new ConcurrentQueue<string>();
					var streams = new List<AutomaticFileSessionFixture.ObservedStream>();
					var failRecord = true;
					var failOpen = false;
					Bind(fixture, calls, paths, open: (path, mode, access, share) =>
					{
						if (failOpen) throw new IOException("selected open canary " + path);
						var stream = fixture.Files.Open(path, mode, access, share);
						streams.Add(stream);
						stream.AfterOperation = operation =>
						{
							if (failRecord && operation == failingOperation)
							{
								failRecord = false;
								throw new IOException("effects then throw canary");
							}
						};
						return stream;
					});
					var before = DateTimeOffset.UtcNow;
					AssertGuidance(Failure(fixture, () => Emit(fixture, typeof(string), "once", "uncertain"), 1), false);
					var selected = paths.Single();
					AssertRecords(fixture, selected, new[] { Body(fixture, 8, "\"uncertain\"", " | metadata=\"once\"" + EmptyContext) }, before, DateTimeOffset.UtcNow);
					Emit(fixture, null, null, "later independent");
					var retained = fixture.Files.ReadAllBytes(selected);
					failOpen = true;
					AssertGuidance(Failure(fixture, () => Emit(fixture, null, null, "failed open not replayed"), 1), false);
					fixture.Files.ReadAllBytes(selected).ShouldBe(retained);
					failOpen = false;
					Emit(fixture, typeof(int), 9, "after open failure");
					AssertRecords(fixture, selected, new[]
					{
						Body(fixture, 8, "\"uncertain\"", " | metadata=\"once\"" + EmptyContext),
						Body(fixture, 8, "\"later independent\"", EmptyContext),
						Body(fixture, 8, "\"after open failure\"", " | metadata=\"9\"" + EmptyContext)
					}, before, DateTimeOffset.UtcNow);
					streams.All(stream => stream.HandleClosed).ShouldBeTrue();
					fixture.Files.DeleteFile(selected);
					var changedDirectory = fixture.Files.GetPath("changed-cwd");
					fixture.Files.CreateDirectory(changedDirectory);
					Environment.CurrentDirectory = changedDirectory;
					Emit(fixture, null, null, "recreated");
					AssertRecords(fixture, selected, new[] { Body(fixture, 8, "\"recreated\"", EmptyContext) }, before, DateTimeOffset.UtcNow);
					paths.Count.ShouldBe(5);
					paths.Distinct().ShouldBe(new[] { selected });
					Count(calls, "host").ShouldBe(1);
					Count(calls, "local").ShouldBe(0);
					Count(calls, "clock").ShouldBe(1);
					Count(calls, "token").ShouldBe(1);
					Directory.GetFileSystemEntries(changedDirectory).ShouldBeEmpty();
				}
				finally
				{
					Environment.CurrentDirectory = previousDirectory;
					Console.SetError(previousError);
				}
			}
		}

		[Fact]
		public void ShouldIsolateThrowingAndRecursiveReportersWithoutLeakingInitialFailureInputs()
		{
			using (var fixture = new AutomaticFileIntegrationFixture())
			using (var stderr = new StringWriter())
			using (var throwingError = new ThrowingErrorWriter())
			{
				var previousError = Console.Error;
				Console.SetError(stderr);
				try
				{
					var calls = new ConcurrentQueue<string>();
					var paths = new ConcurrentQueue<string>();
					var host = fixture.Files.GetPath("private-host-canary");
					var secondary = Secondary(fixture.Files.LocalRoot, host);
					var cause = new IOException("RAW-CAUSE-CANARY " + host);
					cause.Data["private-data"] = "RAW-DATA-CANARY";
					Bind(fixture, calls, paths, host: () => host, prepare: path => { throw cause; });
					var order = new List<string>();
					var reports = new List<object>();
					var nested = new List<Exception>();
					var diagnosticTexts = new List<string>();
					using (fixture.SubscribeFailures(report => { order.Add("throw"); throw new IOException("REPORTER-CANARY"); }))
					using (fixture.SubscribeFailures(report =>
					{
						order.Add("recursive");
						var beforeNested = stderr.ToString();
						var attemptsBeforeNested = throwingError.Attempts;
						nested.Add(Failure(fixture, () => Emit(fixture, typeof(string), "NESTED-METADATA-CANARY", "NESTED-PAYLOAD-CANARY"), 1));
						if (stderr.ToString() != beforeNested || throwingError.Attempts != attemptsBeforeNested)
							diagnosticTexts.Add("unexpected nested stderr");
					}))
					using (fixture.SubscribeFailures(report => { order.Add("last"); reports.Add(report); }))
					using (Scope(fixture, "SCOPE-CANARY", new[] { new KeyValuePair<string, object>("PRIVATE-KEY-CANARY", "PRIVATE-VALUE-CANARY") }))
					{
						var first = Failure(fixture, () => Emit(fixture, typeof(string), "METADATA-CANARY", "PAYLOAD-CANARY", annotations: Annotations(fixture, "LABEL-CANARY"), exception: new Exception("INPUT-EXCEPTION-CANARY")), 1);
						var frozen = calls.ToArray();
						order.ShouldBe(new[] { "throw", "recursive", "last" });
						reports.Count.ShouldBe(1);
						nested.Count.ShouldBe(1);
						AssertBoundedStderr(stderr.ToString());
						AssertGuidanceText(stderr.ToString());
						diagnosticTexts.Add(stderr.ToString());
						Console.SetError(throwingError);
						var second = Failure(fixture, () => Emit(fixture, null, null, "remembered private payload"), 1);
						throwingError.Attempts.ShouldBe(1);
						stderr.GetStringBuilder().Clear();
						Console.SetError(stderr);
						var third = Failure(fixture, () => Emit(fixture, typeof(int), 0, "notification restored"), 1);
						order.ShouldBe(Enumerable.Range(0, 3).SelectMany(index => new[] { "throw", "recursive", "last" }));
						reports.Count.ShouldBe(3);
						nested.Count.ShouldBe(3);
						AssertBoundedStderr(stderr.ToString());
						diagnosticTexts.Add(stderr.ToString());
						diagnosticTexts.Count.ShouldBe(2);
						calls.ToArray().ShouldBe(frozen);
						var errors = new[] { first, second, third }.Concat(nested).ToArray();
						errors.Select(error => Correlation(Property(error, "Report"))).Distinct().Count().ShouldBe(6);
						var forbidden = new[]
						{
							host, host.Replace('\\', '/'), fixture.Files.LocalRoot, secondary, Path.GetFileName(secondary), Path.GetFileName(secondary).Substring(4),
							"RAW-CAUSE-CANARY", "RAW-DATA-CANARY", "REPORTER-CANARY", "METADATA-CANARY", "PAYLOAD-CANARY", "SCOPE-CANARY",
							"PRIVATE-KEY-CANARY", "PRIVATE-VALUE-CANARY", "LABEL-CANARY", "INPUT-EXCEPTION-CANARY"
						};
						foreach (var error in errors)
						{
							AssertGuidance(error, true);
							AssertReport(Property(error, "Report"), 1);
							foreach (var canary in forbidden)
							{
								error.Message.ShouldNotContain(canary);
								error.ToString().ShouldNotContain(canary);
								foreach (var diagnostic in diagnosticTexts) diagnostic.ShouldNotContain(canary);
							}
						}
						for (var index = 0; index < reports.Count; index++)
							Correlation(reports[index]).ShouldBe(Correlation(Property(new[] { first, second, third }[index], "Report")));
					}
				}
				finally { Console.SetError(previousError); }
			}
		}

		private const string EmptyContext = " | entryLabels=null | scopes=[]";

		private static Assembly LoadProductCopy()
		{
			return Assembly.Load(File.ReadAllBytes(typeof(Utilities.Logger).Assembly.Location));
		}

		private static Type ProductType(Assembly assembly, string name)
		{
			return assembly.GetType("ProphetsWay.Utilities." + name, true);
		}

		private static object StaticField(Type type, string name)
		{
			return type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
		}

		private static FieldInfo OwnerField(object owner, string name)
		{
			var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
			field.ShouldNotBeNull();
			return field;
		}

		private static object ConstructOwner(Assembly assembly, object[] dependencies)
		{
			var constructor = ProductType(assembly, "AutomaticFileSession").GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic,
				null, dependencies.Length == 0 ? Type.EmptyTypes : DependencyTypes, null);
			constructor.ShouldNotBeNull();
			return Unwrap(() => constructor.Invoke(dependencies));
		}

		private static void AssertUnusedOwner(object owner)
		{
			OwnerField(owner, "_selectedPath").GetValue(owner).ShouldBeNull();
			OwnerField(owner, "_hostDirectoryOverride").GetValue(owner).ShouldBeNull();
			OwnerField(owner, "_state").GetValue(owner).ToString().ShouldBe("Uninitialized");
		}

		private static bool WriteOwner(object owner, string record)
		{
			return (bool)Unwrap(() => owner.GetType().GetMethod("TryWriteRecord", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(owner, new object[] { record }));
		}

		private static object[] InertDependencies(int[] calls)
		{
			return new object[]
			{
				new Func<string>(() => { calls[0]++; return @"C:\temp\logger tests\inert-host"; }),
				new Func<string>(() => { calls[1]++; return @"C:\temp\logger tests\inert-local"; }),
				new Func<DateTime>(() => { calls[2]++; return AllocationTime; }),
				new Func<Guid>(() => { calls[3]++; return AllocationToken; }),
				new Action<string>(path => { calls[4]++; }),
				new Func<string, FileMode, FileAccess, FileShare, Stream>((path, mode, access, share) => { calls[5]++; throw new IOException("inert open only"); })
			};
		}

		private static object Unwrap(Func<object> invocation)
		{
			try { return invocation(); }
			catch (TargetInvocationException failure) when (failure.InnerException != null)
			{
				ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
				throw;
			}
		}

		private static void Bind(AutomaticFileIntegrationFixture fixture, ConcurrentQueue<string> calls, ConcurrentQueue<string> paths,
			Func<string> host = null, Action<string> prepare = null, Func<string, FileMode, FileAccess, FileShare, Stream> open = null)
		{
			fixture.BindUnusedOwner(
				() => { calls.Enqueue("host"); return host == null ? fixture.Files.PrimaryDirectory : host(); },
				() => { calls.Enqueue("local"); return fixture.Files.LocalRoot; },
				() => { calls.Enqueue("clock"); return AllocationTime; },
				() => { calls.Enqueue("token"); return AllocationToken; },
				path => { calls.Enqueue("prepare"); if (prepare == null) fixture.Files.CreateDirectory(path); else prepare(path); },
				(path, mode, access, share) =>
				{
					calls.Enqueue("open"); paths.Enqueue(path);
					return open == null ? fixture.Files.Open(path, mode, access, share) : open(path, mode, access, share);
				});
		}

		private static int Count(ConcurrentQueue<string> calls, string name)
		{
			return calls.Count(value => value == name);
		}

		private static object Level(AutomaticFileIntegrationFixture fixture, int mask)
		{
			return Enum.ToObject(ProductType(fixture.ProductAssembly, "LogLevels"), mask);
		}

		private static void Emit(AutomaticFileIntegrationFixture fixture, Type route, object metadata, string message,
			int mask = 8, object annotations = null, Exception exception = null)
		{
			var annotationType = ProductType(fixture.ProductAssembly, "LogAnnotations");
			var levelType = ProductType(fixture.ProductAssembly, "LogLevels");
			if (route == null)
				fixture.InvokePublic("LogAnnotated", Type.EmptyTypes, new[] { annotationType, levelType, typeof(string), typeof(Exception) }, annotations, Level(fixture, mask), message, exception);
			else
				fixture.InvokePublic("LogAnnotated", new[] { route }, new[] { annotationType, levelType, route, typeof(string), typeof(Exception) }, annotations, Level(fixture, mask), metadata, message, exception);
		}

		private static void Configure(AutomaticFileIntegrationFixture fixture, string directory)
		{
			fixture.InvokePublic("ConfigureAutomaticFileHostDirectory", Type.EmptyTypes, new[] { typeof(string) }, new object[] { directory });
		}

		private static object Annotations(AutomaticFileIntegrationFixture fixture, params string[] labels)
		{
			return Activator.CreateInstance(ProductType(fixture.ProductAssembly, "LogAnnotations"), new object[] { Labels(fixture, labels) });
		}

		private static Array Labels(AutomaticFileIntegrationFixture fixture, string[] labels)
		{
			var type = ProductType(fixture.ProductAssembly, "SensitivityLabel");
			var values = Array.CreateInstance(type, labels.Length);
			for (var index = 0; index < labels.Length; index++) values.SetValue(Activator.CreateInstance(type, labels[index]), index);
			return values;
		}

		private static IDisposable Scope(AutomaticFileIntegrationFixture fixture, string label, IEnumerable<KeyValuePair<string, object>> properties)
		{
			return (IDisposable)fixture.InvokePublic("BeginScope", Type.EmptyTypes,
				new[] { ProductType(fixture.ProductAssembly, "LogAnnotations"), typeof(IEnumerable<KeyValuePair<string, object>>) },
				Annotations(fixture, label), properties);
		}

		private static object Settings(AutomaticFileIntegrationFixture fixture, bool enabled = true, int mask = 63, string mode = "NoFilter", string label = null)
		{
			var policy = Activator.CreateInstance(ProductType(fixture.ProductAssembly, "DestinationLabelPolicy"),
				Enum.Parse(ProductType(fixture.ProductAssembly, "LabelFilterMode"), mode), Labels(fixture, label == null ? new string[0] : new[] { label }));
			return Activator.CreateInstance(ProductType(fixture.ProductAssembly, "DestinationRegistrationSettings"), enabled, Level(fixture, mask), policy);
		}

		private static object Destination(AutomaticFileIntegrationFixture fixture, Type route)
		{
			var type = route == null ? ProductType(fixture.ProductAssembly, "LoggerDestinations.EventDestination")
				: ProductType(fixture.ProductAssembly, "LoggerDestinations.GenericEventDestination`1").MakeGenericType(route);
			return Activator.CreateInstance(type, new object[] { 63 });
		}

		private static Type DestinationInterface(AutomaticFileIntegrationFixture fixture, Type route)
		{
			return route == null ? ProductType(fixture.ProductAssembly, "ILoggingDestination")
				: ProductType(fixture.ProductAssembly, "Generics.ILoggingDestination`1").MakeGenericType(route);
		}

		private static void Register(AutomaticFileIntegrationFixture fixture, Type route, object destination, object settings)
		{
			fixture.InvokePublic("AddDestination", route == null ? Type.EmptyTypes : new[] { route },
				new[] { DestinationInterface(fixture, route), ProductType(fixture.ProductAssembly, "DestinationRegistrationSettings") }, destination, settings);
		}

		private static void SetSettings(AutomaticFileIntegrationFixture fixture, Type route, object destination, object settings)
		{
			fixture.InvokePublic("SetDestinationSettings", route == null ? Type.EmptyTypes : new[] { route },
				new[] { DestinationInterface(fixture, route), ProductType(fixture.ProductAssembly, "DestinationRegistrationSettings") }, destination, settings);
		}

		private static void Remove(AutomaticFileIntegrationFixture fixture, Type route, object destination)
		{
			fixture.InvokePublic("RemoveDestination", route == null ? Type.EmptyTypes : new[] { route }, new[] { DestinationInterface(fixture, route) }, destination);
		}

		private static void Clear(AutomaticFileIntegrationFixture fixture, Type route)
		{
			fixture.InvokePublic("ClearDestinations", route == null ? Type.EmptyTypes : new[] { route }, Type.EmptyTypes);
		}

		private static object Property(object value, string name)
		{
			return value.GetType().GetProperty(name).GetValue(value, null);
		}

		private static Exception Failure(AutomaticFileIntegrationFixture fixture, Action act, int position)
		{
			var failure = Record.Exception(act);
			failure.ShouldNotBeNull();
			failure.GetType().ShouldBe(ProductType(fixture.ProductAssembly, "LogDispatchException"));
			failure.InnerException.ShouldBeNull();
			failure.HelpLink.ShouldBeNull();
			failure.Data.Count.ShouldBe(0);
			failure.Source.ShouldBe("ProphetsWay.Logger");
			failure.StackTrace.ShouldBeNull();
			AssertReport(Property(failure, "Report"), position);
			return failure;
		}

		private static Guid Correlation(object report)
		{
			return (Guid)Property(report, "CorrelationId");
		}

		private static void AssertReport(object report, int position)
		{
			Correlation(report).ShouldNotBe(Guid.Empty);
			Property(report, "CoreCaptureFailureCount").ShouldBe(0);
			Property(report, "OverflowCount").ShouldBe(0);
			var failures = (IList)Property(report, "Failures");
			failures.Count.ShouldBe(1);
			Property(failures[0], "RegistrationId").ShouldBe(position);
			Property(failures[0], "Stage").ToString().ShouldBe("Output");
			Should.Throw<NotSupportedException>(() => failures.Clear());
			failures.Count.ShouldBe(1);
		}

		private static void AssertGuidance(Exception error, bool initial)
		{
			foreach (var text in new[] { error.Message, error.ToString() })
			{
				if (initial) AssertGuidanceText(text);
				else text.ShouldNotContain("LocalApplicationData");
			}
		}

		private static void AssertGuidanceText(string text)
		{
			text.ShouldContain("LocalApplicationData", Case.Insensitive);
			text.ShouldContain("base", Case.Insensitive);
			text.ShouldContain("destination", Case.Insensitive);
		}

		private static void AssertBoundedStderr(string text)
		{
			text.Length.ShouldBeInRange(1, 512);
			text.EndsWith("\n", StringComparison.Ordinal).ShouldBeTrue();
		}

		private static string Body(AutomaticFileIntegrationFixture fixture, int mask, string messageToken, string suffix)
		{
			return Level(fixture, mask).ToString().PadLeft(12) + ":  " + messageToken + suffix;
		}

		private static string[] Records(AutomaticFileIntegrationFixture fixture, string path)
		{
			var bytes = fixture.Files.ReadAllBytes(path);
			var text = Encoding.UTF8.GetString(bytes);
			text.EndsWith(Environment.NewLine, StringComparison.Ordinal).ShouldBeTrue();
			var records = text.Substring(0, text.Length - Environment.NewLine.Length).Split(new[] { Environment.NewLine }, StringSplitOptions.None);
			records.All(record => record.Length > 0 && !record.Contains("\r") && !record.Contains("\n") && record[0] != '\ufeff').ShouldBeTrue();
			bytes.ShouldBe(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, records) + Environment.NewLine));
			return records;
		}

		private static DateTimeOffset Timestamp(string record)
		{
			var separator = record.IndexOf(" :: ", StringComparison.Ordinal);
			separator.ShouldBeGreaterThan(0);
			var timestamp = DateTimeOffset.ParseExact(record.Substring(0, separator), "O", CultureInfo.InvariantCulture, DateTimeStyles.None);
			timestamp.Offset.ShouldBe(TimeSpan.Zero);
			return timestamp;
		}

		private static string RecordBody(string record)
		{
			var separator = record.IndexOf(" :: ", StringComparison.Ordinal);
			separator.ShouldBeGreaterThan(0);
			return record.Substring(separator + 4);
		}

		private static void AssertRecords(AutomaticFileIntegrationFixture fixture, string path, string[] expected, DateTimeOffset before, DateTimeOffset after)
		{
			var records = Records(fixture, path);
			records.Select(RecordBody).ShouldBe(expected);
			foreach (var record in records) Timestamp(record).ShouldBeInRange(before, after);
		}

		private static string Secondary(string localRoot, string syntheticHost)
		{
			var key = Path.GetFullPath(syntheticHost).TrimEnd('\\', '/').Replace('\\', '/');
			using (var algorithm = SHA256.Create())
				return Path.Combine(localRoot, "app-" + BitConverter.ToString(algorithm.ComputeHash(new UTF8Encoding(false, true).GetBytes(key))).Replace("-", "").ToLowerInvariant());
		}

		private static IEnumerable<KeyValuePair<string, object>> BrokenProperties(Exception failure)
		{
			yield return new KeyValuePair<string, object>("partial", "never published");
			throw failure;
		}

		private sealed class MessageCallbackException : Exception
		{
			private readonly Action _onRead;
			internal MessageCallbackException(Action onRead) { _onRead = onRead; }
			public override string Message
			{
				get { _onRead(); return "rendered exception"; }
			}
		}

		private sealed class ThrowingErrorWriter : StringWriter
		{
			internal int Attempts;
			public override void Write(string value)
			{
				Attempts++;
				throw new IOException("stderr reporter canary");
			}
		}
	}
}