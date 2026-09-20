using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class LoggerTests
	{
		[Fact]
		public void ShouldTriggerDebugOnDebug()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Debug);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			try
			{
				Utilities.Logger.Debug("Hello World!");
				triggered.Should().BeTrue();
			}
			finally { Utilities.Logger.RemoveDestination(dest); }
		}

		[Fact]
		public void ShouldTriggerInfoOnInfo()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Information);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			try
			{
				Utilities.Logger.Info("Hello World!");
				triggered.Should().BeTrue();
			}
			finally { Utilities.Logger.RemoveDestination(dest); }
		}

		[Fact]
		public void ShouldTriggerCriticalOnCritical()
		{
			var triggered = false;
			var dest = new EventDestination((LogLevels)1);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);
			try
			{
				Emit("Critical", "Hello World!", new Exception("synthetic detail"));
				triggered.ShouldBeTrue();
			}
			finally { Utilities.Logger.RemoveDestination(dest); }
		}

		[Fact]
		public void ShouldTriggerWarnOnWarn()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Warning);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			try
			{
				Utilities.Logger.Warn("Hello World!");
				triggered.Should().BeTrue();
			}
			finally { Utilities.Logger.RemoveDestination(dest); }
		}

		[Fact]
		public void ShouldTriggerErrorOnError()
		{
			var triggered = false;
			var dest = new EventDestination(LogLevels.Error);
			dest.LoggingEvent += (sender, args) => triggered = true;
			Utilities.Logger.AddDestination(dest);

			try
			{
				Utilities.Logger.Error(new Exception("Hello World!"));
				triggered.Should().BeTrue();
			}
			finally { Utilities.Logger.RemoveDestination(dest); }
		}

		[Theory]
		[InlineData("Trace", 32)]
		[InlineData("Debug", 16)]
		[InlineData("Info", 8)]
		[InlineData("Warn", 4)]
		[InlineData("Error", 2)]
		[InlineData("Critical", 1)]
		public void ShouldEmitExactSeverityAndPreserveSuppliedContent(string helper, int bit)
		{
			var destination = new EventDestination((LogLevels)63);
			EventDestination.LoggerEventArgs result = null;
			var calls = 0;
			destination.LoggingEvent += (sender, args) => { calls++; result = args; };
			Utilities.Logger.AddDestination(destination);
			try
			{
				foreach (var message in new[] { "Synthetic CONTEXT", "", " \t " })
				{
					calls = 0;
					result = null;
					var exception = helper == "Error" || helper == "Critical" || (helper == "Warn" && message.Length > 0) ? new Exception("Synthetic DETAIL") : null;
					Emit(helper, message, exception);
					calls.ShouldBe(1);
					result.ShouldNotBeNull();
					result.LogLevel.ShouldBe((LogLevels)bit);
					result.RawMessage.ShouldBe(message);
					result.Exception.ShouldBeSameAs(exception);
					if (exception == null) result.Message.ShouldBe(message);
					else
					{
						result.Message.ShouldContain(message, Case.Sensitive);
						result.Message.ShouldContain(exception.Message, Case.Sensitive);
					}
				}
			}
			finally { Utilities.Logger.RemoveDestination(destination); }
		}

		[Theory]
		[InlineData("Trace", "message")]
		[InlineData("Debug", "message")]
		[InlineData("Info", "message")]
		[InlineData("Warn", "message")]
		[InlineData("Error", "ex")]
		[InlineData("Critical", "ex")]
		[InlineData("Critical", "message")]
		public void ShouldValidateRequiredArgumentsBeforeRecipientEligibility(string helper, string parameter)
		{
			var destination = new GuardDestination();
			Utilities.Logger.AddDestination(destination);
			try
			{
				var failure = Record.Exception(() => Emit(helper, parameter == "message" ? null : "synthetic context", parameter == "ex" ? null : new Exception("synthetic detail")));
				destination.ValidationCalls.ShouldBe(0);
				destination.LogCalls.ShouldBe(0);
				failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe(parameter);
			}
			finally { Utilities.Logger.RemoveDestination(destination); }
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldKeepAbsentErrorContextNull(bool explicitlyNull)
		{
			var destination = new EventDestination((LogLevels)63);
			EventDestination.LoggerEventArgs result = null;
			destination.LoggingEvent += (sender, args) => result = args;
			Utilities.Logger.AddDestination(destination);
			try
			{
				var exception = new Exception("synthetic error detail");
				if (explicitlyNull) Utilities.Logger.Error(exception, (string)null);
				else Utilities.Logger.Error(exception);
				result.ShouldNotBeNull();
				result.RawMessage.ShouldBeNull();
				result.Exception.ShouldBeSameAs(exception);
				result.Message.ShouldContain(exception.Message, Case.Sensitive);
				result.LogLevel.ShouldBe((LogLevels)2);
			}
			finally { Utilities.Logger.RemoveDestination(destination); }
		}

		[Fact]
		public void ShouldRejectNullRegistrationWithoutDisturbingTheExplicitRoute()
		{
			var destination = new EventDestination((LogLevels)63);
			string received = null;
			destination.LoggingEvent += (sender, args) => received = args.RawMessage;
			Utilities.Logger.AddDestination(destination);
			try
			{
				var failure = Record.Exception(() => Utilities.Logger.AddDestination((ILoggingDestination)null));
				failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("newDest");
				Utilities.Logger.Debug("route remains usable");
				received.ShouldBe("route remains usable");
			}
			finally
			{
				Utilities.Logger.RemoveDestination((ILoggingDestination)null);
				Utilities.Logger.RemoveDestination(destination);
			}
		}

		[Fact]
		public void ShouldKeepAnExplicitRejectAllDestinationActive()
		{
			var destination = new EventDestination((LogLevels)0);
			var callbacks = 0;
			destination.LoggingEvent += (sender, args) => callbacks++;
			Utilities.Logger.AddDestination(destination);
			try
			{
				Utilities.Logger.Debug("synthetic denied entry");
				Utilities.Logger.Error(new Exception("synthetic denied detail"));
				callbacks.ShouldBe(0);
				destination.ValidateMessageLevel((LogLevels)16).ShouldBeFalse();
			}
			finally { Utilities.Logger.RemoveDestination(destination); }
		}

		[Theory]
		[InlineData("ordinary", "Trace")]
		[InlineData("ordinary", "Debug")]
		[InlineData("ordinary", "Info")]
		[InlineData("ordinary", "Warn")]
		[InlineData("ordinary", "Error")]
		[InlineData("ordinary", "Critical")]
		[InlineData("typed", "Trace")]
		[InlineData("typed", "Debug")]
		[InlineData("typed", "Info")]
		[InlineData("typed", "Warn")]
		[InlineData("typed", "Error")]
		[InlineData("typed", "Critical")]
		[InlineData("metadata", "Trace")]
		[InlineData("metadata", "Debug")]
		[InlineData("metadata", "Info")]
		[InlineData("metadata", "Warn")]
		[InlineData("metadata", "Error")]
		[InlineData("metadata", "Critical")]
		public void ShouldExposeEachReviewedHelperSignature(string family, string helper)
		{
			var extension = family == "metadata";
			var generic = family != "ordinary";
			var owner = extension ? typeof(Utilities.Generics.MetadataExtensions) : typeof(Utilities.Logger);
			var methods = owner.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(method => method.Name == helper && method.IsGenericMethodDefinition == generic).ToArray();
			methods.Length.ShouldBe(1, "B11 requires exactly one reviewed signature for this helper family");
			var methodInfo = methods[0];
			methodInfo.ReturnType.ShouldBe(typeof(void));
			methodInfo.IsDefined(typeof(ExtensionAttribute), false).ShouldBe(extension);
			Type metadataType = null;
			if (generic)
			{
				methodInfo.GetGenericArguments().Length.ShouldBe(1);
				metadataType = methodInfo.GetGenericArguments()[0];
				metadataType.GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
				metadataType.GetGenericParameterConstraints().ShouldBe(extension ? new[] { typeof(Utilities.Generics.ILoggerMetadata) } : Type.EmptyTypes);
			}
			var errorStyle = helper == "Error" || helper == "Critical";
			var names = extension
				? (errorStyle ? new[] { "metadata", "ex", "message" } : helper == "Warn" ? new[] { "metadata", "message", "ex" } : new[] { "metadata", "message" })
				: generic
					? (errorStyle ? new[] { "ex", "metadata", "message" } : helper == "Warn" ? new[] { "message", "metadata", "ex" } : new[] { "message", "metadata" })
					: (errorStyle ? new[] { "ex", "message" } : helper == "Warn" ? new[] { "message", "ex" } : new[] { "message" });
			var parameters = methodInfo.GetParameters();
			parameters.Select(parameter => parameter.Name).ShouldBe(names);
			foreach (var parameter in parameters)
			{
				parameter.ParameterType.ShouldBe(parameter.Name == "metadata" ? metadataType : parameter.Name == "message" ? typeof(string) : typeof(Exception));
				var optional = helper == "Warn" && parameter.Name == "ex" || helper == "Error" && parameter.Name == "message";
				parameter.IsOptional.ShouldBe(optional);
				if (optional) parameter.DefaultValue.ShouldBeNull();
			}
		}

		[Fact]
		public void ShouldRemoveSecurityHelpersWithoutAddingAPublicRawEntrypoint()
		{
			typeof(Utilities.Logger).GetMethods().Where(method => method.Name == "Security" || method.Name == "Log").ShouldBeEmpty();
			typeof(Utilities.Generics.MetadataExtensions).GetMethods().Where(method => method.Name == "Security").ShouldBeEmpty();
		}

		[Fact]
		public void ShouldRespectCustomRecipientEligibilityBeforeRawHandoff()
		{
			var rejected = new HandoffDestination(false);
			var accepted = new HandoffDestination(true);
			const string message = "Synthetic RAW ordinary context";
			var exception = new Exception("Synthetic RAW ordinary detail");
			try
			{
				Utilities.Logger.AddDestination(rejected);
				Utilities.Logger.AddDestination(accepted);
				Utilities.Logger.Warn(message, exception);

				rejected.ValidationCalls.ShouldBeGreaterThan(0);
				rejected.ValidatedLevel.ShouldBe(LogLevels.WarningOnly);
				rejected.LogCalls.ShouldBe(0);
				rejected.ReceivedMessage.ShouldBeNull();
				rejected.ReceivedException.ShouldBeNull();
				accepted.ValidationCalls.ShouldBeGreaterThan(0);
				accepted.ValidatedLevel.ShouldBe(LogLevels.WarningOnly);
				accepted.LogCalls.ShouldBe(1);
				accepted.ReceivedLevel.ShouldBe(LogLevels.WarningOnly);
				accepted.ReceivedMessage.ShouldBe(message);
				accepted.ReceivedException.ShouldBeSameAs(exception);
			}
			finally
			{
				Utilities.Logger.RemoveDestination(rejected);
				Utilities.Logger.RemoveDestination(accepted);
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldRejectDuplicateReferenceWithoutChangingMembership(bool typed)
		{
			var received = new List<string>();
			var destination = new RegistrationRecipient("recipient", received);
			try
			{
				ClearRegistration(typed);
				AddRegistration(typed, destination);
				var failure = Record.Exception(() => AddRegistration(typed, destination));
				destination.ValidationCalls.ShouldBe(0);
				received.ShouldBeEmpty();
				LogRegistration(typed, "entry");
				failure.ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("newDest");
				received.ShouldBe(new[] { "recipient:entry" });
			}
			finally { ClearRegistration(typed); }
		}

		[Theory]
		[InlineData(false, false)]
		[InlineData(false, true)]
		[InlineData(true, false)]
		[InlineData(true, true)]
		public void ShouldUseOnlyReferenceIdentityAndAppendReaddedRecipients(bool typed, bool hostile)
		{
			var received = new List<string>();
			var sentinel = new RegistrationRecipient("sentinel", received);
			var first = new RegistrationRecipient("first", received) { EqualToPeers = true, HostileEquality = hostile };
			var second = new RegistrationRecipient("second", received) { EqualToPeers = true, HostileEquality = hostile };
			var absent = new RegistrationRecipient("absent", received) { EqualToPeers = true, HostileEquality = hostile };
			try
			{
				ClearRegistration(typed);
				AddRegistration(typed, sentinel);
				AddRegistration(typed, first);
				AddRegistration(typed, second);
				RemoveRegistration(typed, absent);
				LogRegistration(typed, "before");
				received.ShouldBe(new[] { "sentinel:before", "first:before", "second:before" });
				received.Clear();
				RemoveRegistration(typed, first);
				RemoveRegistration(typed, first);
				RemoveRegistration(typed, null);
				AddRegistration(typed, first);
				LogRegistration(typed, "after");
				received.ShouldBe(new[] { "sentinel:after", "second:after", "first:after" });
				new[] { sentinel, first, second, absent }.Sum(item => item.EqualityCalls).ShouldBe(0);
				ClearRegistration(typed);
				ClearRegistration(typed);
				AddRegistration(typed, first);
				received.Clear();
				LogRegistration(typed, "reset");
				received.ShouldBe(new[] { "first:reset" });
				new[] { sentinel, first, second, absent }.Sum(item => item.Disposals).ShouldBe(0);
			}
			finally { ClearRegistration(typed); }
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldCaptureBeforeEligibilityAndRecaptureForRecursiveLogging(bool typed)
		{
			var received = new List<string>();
			var first = new RegistrationRecipient("first", received);
			var removed = new RegistrationRecipient("removed", received);
			var retained = new RegistrationRecipient("retained", received);
			var added = new RegistrationRecipient("added", received);
			var mutated = false;
			first.OnEligibility = () =>
			{
				if (mutated) return;
				mutated = true;
				RemoveRegistration(typed, first);
				RemoveRegistration(typed, removed);
				AddRegistration(typed, added);
				LogRegistration(typed, "inner");
			};
			try
			{
				ClearRegistration(typed);
				AddRegistration(typed, first);
				AddRegistration(typed, removed);
				AddRegistration(typed, retained);
				LogRegistration(typed, "outer");
				received.ShouldBe(new[] { "retained:inner", "added:inner", "first:outer", "removed:outer", "retained:outer" });
				received.Clear();
				LogRegistration(typed, "later");
				received.ShouldBe(new[] { "retained:later", "added:later" });
				first.Disposals.ShouldBe(0);
				removed.Disposals.ShouldBe(0);
			}
			finally { ClearRegistration(typed); }
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldRetainCapturedOrderWhenCallbackClearsBorrowedRecipients(bool typed)
		{
			var received = new List<string>();
			var first = new RegistrationRecipient("first", received);
			var second = new RegistrationRecipient("second", received);
			var replacement = new RegistrationRecipient("replacement", received);
			first.OnLog = () =>
			{
				ClearRegistration(typed);
				AddRegistration(typed, replacement);
			};
			try
			{
				ClearRegistration(typed);
				AddRegistration(typed, first);
				AddRegistration(typed, second);
				LogRegistration(typed, "outer");
				received.ShouldBe(new[] { "first:outer", "second:outer" });
				received.Clear();
				LogRegistration(typed, "later");
				received.ShouldBe(new[] { "replacement:later" });
				ClearRegistration(typed);
				new[] { first, second, replacement }.Sum(item => item.Disposals).ShouldBe(0);
			}
			finally { ClearRegistration(typed); }
		}

		[Theory]
		[InlineData(false, false)]
		[InlineData(false, true)]
		[InlineData(true, false)]
		[InlineData(true, true)]
		public void ShouldPublishWithoutWaitingForEligibilityOrCallback(bool typed, bool eligibility)
		{
			var received = new List<string>();
			var first = new RegistrationRecipient("first", received);
			var second = new RegistrationRecipient("second", received);
			var added = new RegistrationRecipient("added", received);
			using (var entered = new ManualResetEventSlim())
			using (var release = new ManualResetEventSlim())
			using (var published = new ManualResetEventSlim())
			{
				var blocked = 0;
				Action pause = () =>
				{
					if (Interlocked.Exchange(ref blocked, 1) != 0) return;
					entered.Set();
					if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test release was not signaled.");
				};
				if (eligibility) first.OnEligibility = pause;
				else first.OnLog = pause;
				Exception producerFailure = null;
				Exception writerFailure = null;
				var producer = new Thread(() => { try { LogRegistration(typed, "outer"); } catch (Exception failure) { producerFailure = failure; } }) { IsBackground = true };
				var writer = new Thread(() =>
				{
					try
					{
						RemoveRegistration(typed, second);
						AddRegistration(typed, added);
					}
					catch (Exception failure) { writerFailure = failure; }
					finally { published.Set(); }
				}) { IsBackground = true };
				var writerStarted = false;
				var observedEntry = false;
				var publishedBeforeRelease = false;
				var producerJoined = false;
				var writerJoined = false;
				try
				{
					ClearRegistration(typed);
					AddRegistration(typed, first);
					AddRegistration(typed, second);
					producer.Start();
					try
					{
						observedEntry = entered.Wait(TimeSpan.FromSeconds(5));
						if (observedEntry)
						{
							writer.Start();
							writerStarted = true;
							publishedBeforeRelease = published.Wait(TimeSpan.FromSeconds(5));
						}
					}
					finally
					{
						release.Set();
						producerJoined = producer.Join(TimeSpan.FromSeconds(5));
						writerJoined = !writerStarted || writer.Join(TimeSpan.FromSeconds(5));
						if (!producerJoined || !writerJoined)
							Environment.FailFast("Registration specification worker did not terminate after release; aborting the test host before shared teardown.");
					}
					producerJoined.ShouldBeTrue();
					writerJoined.ShouldBeTrue();
					observedEntry.ShouldBeTrue();
					publishedBeforeRelease.ShouldBeTrue("publication must not depend on releasing user code; the timeout is a deadlock guard");
					writerFailure.ShouldBeNull();
					producerFailure.ShouldBeNull();
					received.ShouldBe(new[] { "first:outer", "second:outer" });
					received.Clear();
					LogRegistration(typed, "later");
					received.ShouldBe(new[] { "first:later", "added:later" });
					second.Disposals.ShouldBe(0);
				}
				finally { ClearRegistration(typed); }
			}
		}

		[Theory]
		[InlineData(false, false)]
		[InlineData(false, true)]
		[InlineData(true, false)]
		[InlineData(true, true)]
		public void ShouldCoordinateCompetingAddsWithoutLosingMembership(bool typed, bool sameReference)
		{
			var received = new List<string>();
			var sentinel = new RegistrationRecipient("sentinel", received);
			var first = new RegistrationRecipient("first", received);
			var second = sameReference ? first : new RegistrationRecipient("second", received);
			var failures = new Exception[2];
			using (var ready = new CountdownEvent(2))
			using (var start = new ManualResetEventSlim())
			{
				var recipients = new[] { first, second };
				var workers = Enumerable.Range(0, 2).Select(index => new Thread(() =>
				{
					try
					{
						ready.Signal();
						if (!start.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test start was not signaled.");
						AddRegistration(typed, recipients[index]);
					}
					catch (Exception failure) { failures[index] = failure; }
				}) { IsBackground = true }).ToArray();
				var started = new List<Thread>();
				var joined = true;
				try
				{
					ClearRegistration(typed);
					AddRegistration(typed, sentinel);
					try
					{
						foreach (var worker in workers) { worker.Start(); started.Add(worker); }
						ready.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
					}
					finally
					{
						start.Set();
						foreach (var worker in started) joined &= worker.Join(TimeSpan.FromSeconds(5));
						if (!joined)
							Environment.FailFast("Registration specification add worker did not terminate after release; aborting the test host before shared teardown.");
					}
					joined.ShouldBeTrue();
					if (sameReference)
					{
						failures.Count(failure => failure == null).ShouldBe(1);
						failures.Single(failure => failure != null).ShouldBeOfType<ArgumentException>().ParamName.ShouldBe("newDest");
					}
					else failures.ShouldAllBe(failure => failure == null);
					LogRegistration(typed, "entry");
					received[0].ShouldBe("sentinel:entry");
					received.Skip(1).OrderBy(value => value).ShouldBe(sameReference ? new[] { "first:entry" } : new[] { "first:entry", "second:entry" });
				}
				finally { ClearRegistration(typed); }
			}
		}

		[Theory]
		[InlineData(true, true)]
		[InlineData(true, false)]
		[InlineData(false, true)]
		[InlineData(false, false)]
		public void ShouldContinueOrdinaryAttemptsBeforeReportingEitherCallbackFailure(bool eligibilityFailure, bool failingFirst)
		{
			var attempts = new List<string>();
			var failing = new FailureDestination("failing", attempts);
			var healthy = new FailureDestination("healthy", attempts);
			var cause = new InvalidOperationException("synthetic callback failure");
			if (eligibilityFailure) failing.OnEligibility = () => { throw cause; };
			else failing.OnLog = () => { throw cause; };
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => { attempts.Add("notice"); reports.Add(report); };
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(failingFirst ? failing : healthy);
					Utilities.Logger.AddDestination(failingFirst ? healthy : failing);
					Utilities.Logger.DispatchFailed += observer;
					var payload = new Exception("synthetic payload");
					var failure = Record.Exception(() => Utilities.Logger.Warn("unchanged context", payload));
					var failedAttempts = eligibilityFailure ? new[] { "failing:eligibility" } : new[] { "failing:eligibility", "failing:log" };
					var healthyAttempts = new[] { "healthy:eligibility", "healthy:log" };
					attempts.ShouldBe((failingFirst ? failedAttempts.Concat(healthyAttempts) : healthyAttempts.Concat(failedAttempts)).Concat(new[] { "notice" }));
					failing.ValidationCalls.ShouldBe(1);
					failing.LogCalls.ShouldBe(eligibilityFailure ? 0 : 1);
					if (eligibilityFailure)
					{
						failing.ReceivedMessage.ShouldBeNull();
						failing.ReceivedException.ShouldBeNull();
					}
					healthy.ValidationCalls.ShouldBe(1);
					healthy.LogCalls.ShouldBe(1);
					healthy.ReceivedLevel.ShouldBe(LogLevels.WarningOnly);
					healthy.ReceivedMessage.ShouldBe("unchanged context");
					healthy.ReceivedException.ShouldBeSameAs(payload);
					(failure is LogDispatchException).ShouldBeTrue("B07 requires safe mandatory propagation after the healthy attempt and notification");
					reports.Count.ShouldBe(1);
					var stage = eligibilityFailure ? LogFailureStage.Eligibility : LogFailureStage.Output;
					AssertFailureReport(reports[0], new[] { failingFirst ? 1 : 2 }, new[] { stage }, 0);
					AssertEquivalentFailureReports(reports[0], ((LogDispatchException)failure).Report);
					stderr.ToString().Length.ShouldBeInRange(1, 512);
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Theory]
		[InlineData(1)]
		[InlineData(8)]
		[InlineData(9)]
		public void ShouldBoundOriginalFailuresWithoutTruncatingTheCapturedAttempts(int count)
		{
			var attempts = new List<string>();
			var rejected = new FailureDestination("rejected", attempts) { OnEligibility = () => false };
			var healthy = new FailureDestination("healthy", attempts);
			var tail = new FailureDestination("tail", attempts);
			var failed = Enumerable.Range(0, count).Select(index => new FailureDestination("failure" + index, attempts)).ToArray();
			for (var index = 0; index < count; index++)
			{
				if (index % 2 == 0) failed[index].OnEligibility = () => { throw new InvalidOperationException("synthetic eligibility"); };
				else failed[index].OnLog = () => { throw new AggregateException(new Exception("first synthetic cause"), new Exception("second synthetic cause")); };
			}
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => reports.Add(report);
			Action<LogFailureReport> brokenObserver = report => { throw new InvalidOperationException("synthetic observer failure"); };
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(rejected);
					Utilities.Logger.AddDestination(healthy);
					foreach (var destination in failed) Utilities.Logger.AddDestination(destination);
					Utilities.Logger.AddDestination(tail);
					Utilities.Logger.DispatchFailed += brokenObserver;
					Utilities.Logger.DispatchFailed += observer;
					var failure = Record.Exception(() => Utilities.Logger.Trace("bounded attempts"));
					failed.Select(destination => destination.ValidationCalls).ShouldBe(Enumerable.Repeat(1, count));
					failed.Select(destination => destination.LogCalls).ShouldBe(Enumerable.Range(0, count).Select(index => index % 2));
					rejected.LogCalls.ShouldBe(0);
					healthy.LogCalls.ShouldBe(1);
					tail.LogCalls.ShouldBe(1, "B08 bounds retained descriptors, never attempts");
					tail.ReceivedMessage.ShouldBe("bounded attempts");
					(failure is LogDispatchException).ShouldBeTrue("B07 requires a safe failure even with successful recipients");
					reports.Count.ShouldBe(1);
					var retained = Math.Min(count, 8);
					AssertFailureReport(reports[0], Enumerable.Range(3, retained).ToArray(), Enumerable.Range(0, retained).Select(index => index % 2 == 0 ? LogFailureStage.Eligibility : LogFailureStage.Output).ToArray(), count - retained);
					AssertEquivalentFailureReports(reports[0], ((LogDispatchException)failure).Report);
					stderr.ToString().Length.ShouldBeInRange(1, 512);
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= brokenObserver;
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Fact]
		public void ShouldRetainAnImmutableReportWhileLaterCapturesAssignFreshScopedIdentifiers()
		{
			var attempts = new List<string>();
			var rejected = new FailureDestination("rejected", attempts) { OnEligibility = () => false };
			var failing = new FailureDestination("failing", attempts) { OnLog = () => { throw new InvalidOperationException("synthetic output"); } };
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(rejected);
					Utilities.Logger.AddDestination(failing);
					var firstFailure = Record.Exception(() => Utilities.Logger.Debug("first capture"));
					(firstFailure is LogDispatchException).ShouldBeTrue("B19 provides a safe report without any event subscriber");
					var first = ((LogDispatchException)firstFailure).Report;
					AssertFailureReport(first, new[] { 2 }, new[] { LogFailureStage.Output }, 0);
					var correlation = first.CorrelationId;
					var view = first.Failures;
					var item = view[0];
					var generic = (IList<LogFailureDescriptor>)view;
					var nongeneric = (System.Collections.IList)view;
					Should.Throw<NotSupportedException>(() => generic.Add(item));
					Should.Throw<NotSupportedException>(() => generic[0] = item);
					Should.Throw<NotSupportedException>(() => generic.Clear());
					Should.Throw<NotSupportedException>(() => nongeneric.RemoveAt(0));
					Utilities.Logger.RemoveDestination(rejected);
					var secondFailure = Record.Exception(() => Utilities.Logger.Debug("second capture"));
					(secondFailure is LogDispatchException).ShouldBeTrue();
					var second = ((LogDispatchException)secondFailure).Report;
					AssertFailureReport(second, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
					second.CorrelationId.ShouldNotBe(correlation);
					Utilities.Logger.ClearDestinations();
					AssertFailureReport(first, new[] { 2 }, new[] { LogFailureStage.Output }, 0);
					first.CorrelationId.ShouldBe(correlation);
					view.Select(descriptor => descriptor.RegistrationId).ShouldBe(new[] { 2 });
					item.Stage.ShouldBe(LogFailureStage.Output);
					item.RegistrationId.ShouldBe(2);
					var readResults = new bool[2];
					var readFailures = new Exception[2];
					using (var start = new ManualResetEventSlim())
					{
						var readers = Enumerable.Range(0, 2).Select(index => new Thread(() =>
						{
							try
							{
								if (!start.Wait(TimeSpan.FromSeconds(5))) return;
								readResults[index] = Enumerable.Range(0, 128).All(iteration => first.CorrelationId == correlation && first.Failures.Count == 1 && first.Failures[0].RegistrationId == 2 && first.Failures[0].Stage == LogFailureStage.Output && first.OverflowCount == 0);
							}
							catch (Exception failure) { readFailures[index] = failure; }
						}) { IsBackground = true }).ToArray();
						foreach (var reader in readers) reader.Start();
						start.Set();
						var joined = true;
						foreach (var reader in readers) joined &= reader.Join(TimeSpan.FromSeconds(5));
						if (!joined) Environment.FailFast("Report-read specification worker did not terminate before shared teardown.");
						readFailures.ShouldAllBe(failure => failure == null);
						readResults.ShouldBe(new[] { true, true });
					}
					failing.Disposals.ShouldBe(0);
					rejected.Disposals.ShouldBe(0);
					stderr.ToString().Length.ShouldBeInRange(2, 1024);
				}
				finally
				{
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Fact]
		public void ShouldExposeOnlyLibraryCreatedImmutableFailureFacts()
		{
			foreach (var type in new[] { typeof(LogFailureDescriptor), typeof(LogFailureReport), typeof(LogDispatchException) })
			{
				type.IsSealed.ShouldBeTrue();
				type.Namespace.ShouldBe("ProphetsWay.Utilities");
				type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
				type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(method => method.ReturnType == type).ShouldBeEmpty();
				foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
					property.GetSetMethod().ShouldBeNull();
			}
			typeof(LogFailureDescriptor).GetProperties().Select(property => property.Name).OrderBy(name => name).ShouldBe(new[] { "RegistrationId", "Stage" });
			typeof(LogFailureReport).GetProperties().Select(property => property.Name).OrderBy(name => name).ShouldBe(new[] { "CorrelationId", "Failures", "OverflowCount" });
			typeof(LogFailureReport).GetProperty("Failures").PropertyType.ShouldBe(typeof(System.Collections.ObjectModel.ReadOnlyCollection<LogFailureDescriptor>));
			typeof(LogFailureStage).IsDefined(typeof(FlagsAttribute), false).ShouldBeFalse();
			Enum.GetValues(typeof(LogFailureStage)).Cast<LogFailureStage>().Select(stage => (int)stage).ShouldBe(new[] { 1, 2 });
			((int)LogFailureStage.Eligibility).ShouldBe(1);
			((int)LogFailureStage.Output).ShouldBe(2);
			typeof(Utilities.Logger).GetEvent("DispatchFailed").EventHandlerType.ShouldBe(typeof(Action<LogFailureReport>));
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldKeepSuccessfulOrRejectedOrdinaryCallsAndArgumentGuardsSilent(bool accepted)
		{
			var destination = new FailureDestination("recipient", new List<string>()) { OnEligibility = () => accepted };
			var notices = 0;
			Action<LogFailureReport> observer = report => notices++;
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(destination);
					Utilities.Logger.DispatchFailed += observer;
					Should.Throw<ArgumentNullException>(() => Utilities.Logger.Warn(null));
					destination.ValidationCalls.ShouldBe(0);
					Utilities.Logger.Warn(" \t ");
					destination.ValidationCalls.ShouldBe(1);
					destination.LogCalls.ShouldBe(accepted ? 1 : 0);
					destination.ReceivedMessage.ShouldBe(accepted ? " \t " : null);
					destination.ReceivedException.ShouldBeNull();
					notices.ShouldBe(0);
					stderr.ToString().ShouldBeEmpty();
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Fact]
		public void ShouldNeverInspectOrExportForeignDiagnosticsThroughSafeFailureViews()
		{
			var canary = "m2c-private-" + Guid.NewGuid().ToString("N");
			var cause = new OpaqueFailure(canary);
			var payload = new OpaqueFailure(canary + "-payload");
			var attempts = new List<string>();
			var failing = new FailureDestination("failing", attempts) { OnLog = () => { throw cause; } };
			var healthy = new FailureDestination("healthy", attempts);
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => reports.Add(report);
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(failing);
					Utilities.Logger.AddDestination(healthy);
					Utilities.Logger.DispatchFailed += observer;
					var observed = Record.Exception(() => Utilities.Logger.Critical(payload, canary));
					(observed is LogDispatchException).ShouldBeTrue("B19 must replace the raw callback exception without formatting it");
					var failure = (LogDispatchException)observed;
					failure.InnerException.ShouldBeNull();
					failure.StackTrace.ShouldBeNull();
					failure.HelpLink.ShouldBeNull();
					failure.Data.Count.ShouldBe(0);
					failure.Source.ShouldBe("ProphetsWay.Logger");
					failure.Message.ShouldNotBeNull();
					failure.ToString().ShouldNotBeNull();
					reports.Count.ShouldBe(1);
					AssertFailureReport(failure.Report, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
					AssertEquivalentFailureReports(reports[0], failure.Report);
					AssertNoFailureBacklinks(failure, reports[0], failing, healthy, cause, payload, observer, canary);
					healthy.ReceivedException.ShouldBeSameAs(payload);
					healthy.ReceivedMessage.ShouldBe(canary);
					foreach (var text in new[] { failure.Message, failure.ToString(), stderr.ToString() })
					{
						text.Contains(canary).ShouldBeFalse("B12/B20 exclude foreign content from safe views");
						text.Contains(typeof(OpaqueFailure).FullName).ShouldBeFalse();
						text.Contains(typeof(FailureDestination).FullName).ShouldBeFalse();
					}
					failure.Source = canary;
					failure.HelpLink = canary;
					failure.Data[canary] = canary;
					failure.ToString().Contains(canary).ShouldBeFalse("B20 excludes inherited mutable diagnostic content from ToString");
					AssertEquivalentFailureReports(reports[0], failure.Report);
					cause.DiagnosticReads.ShouldBe(0);
					payload.DiagnosticReads.ShouldBe(0);
					(failing.DiagnosticCalls + healthy.DiagnosticCalls).ShouldBe(0);
					stderr.ToString().Length.ShouldBeInRange(1, 512);
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldIsolateEverySubscriberAndPartialStderrFailureWithoutReplacingOriginalFacts(bool allSubscribersThrow)
		{
			var attempts = new List<string>();
			var cause = new OpaqueFailure("synthetic-private-recipient");
			var observerCause = new OpaqueFailure("synthetic-private-subscriber");
			var writerCause = new OpaqueFailure("synthetic-private-writer");
			var failing = new FailureDestination("failing", attempts) { OnLog = () => { throw cause; } };
			var healthy = new FailureDestination("healthy", attempts);
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> first = report => { attempts.Add("first"); reports.Add(report); throw observerCause; };
			Action<LogFailureReport> second = report => { attempts.Add("second"); reports.Add(report); if (allSubscribersThrow) throw observerCause; };
			Action<LogFailureReport> third = report => { attempts.Add("third"); reports.Add(report); if (allSubscribersThrow) throw observerCause; };
			var multicast = first + second;
			var originalError = Console.Error;
			using (var stderr = new ObservingErrorWriter { OnWrite = () => attempts.Add("stderr"), WriteFailure = writerCause })
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(failing);
					Utilities.Logger.AddDestination(healthy);
					Utilities.Logger.DispatchFailed += multicast;
					Utilities.Logger.DispatchFailed += third;
					var observed = Record.Exception(() => Utilities.Logger.Info("synthetic-private-message"));
					attempts.ShouldBe(new[] { "failing:eligibility", "failing:log", "healthy:eligibility", "healthy:log", "first", "second", "third", "stderr" });
					(observed is LogDispatchException).ShouldBeTrue();
					var failure = (LogDispatchException)observed;
					AssertFailureReport(failure.Report, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
					reports.Count.ShouldBe(3);
					foreach (var report in reports)
					{
						AssertEquivalentFailureReports(failure.Report, report);
						AssertNoFailureBacklinks(failure, report, failing, healthy, cause, observerCause, writerCause, first, second, third, multicast, stderr, "synthetic-private");
					}
					failure.InnerException.ShouldBeNull();
					stderr.WriteCalls.ShouldBe(1, "B16 forbids retry after the writer partially writes and throws");
					stderr.Text.Length.ShouldBe(1);
					stderr.FlushCalls.ShouldBe(0);
					stderr.Disposals.ShouldBe(0);
					cause.DiagnosticReads.ShouldBe(0);
					observerCause.DiagnosticReads.ShouldBe(0);
					writerCause.DiagnosticReads.ShouldBe(0);
					failure.Message.Contains("synthetic-private").ShouldBeFalse();
					failure.ToString().Contains("synthetic-private").ShouldBeFalse();
					stderr.WriteFailure = null;
					attempts.Clear();
					var recovered = Record.Exception(() => Utilities.Logger.Info("later configured failure"));
					attempts.Take(7).ShouldBe(new[] { "failing:eligibility", "failing:log", "healthy:eligibility", "healthy:log", "first", "second", "third" });
					attempts.Skip(7).ShouldNotBeEmpty();
					attempts.Skip(7).ShouldAllBe(attempt => attempt == "stderr");
					(recovered is LogDispatchException).ShouldBeTrue();
					reports.Count.ShouldBe(6, "B18 restores reporting even after every reporter fails");
					AssertEquivalentFailureReports(reports[3], ((LogDispatchException)recovered).Report);
					reports[3].CorrelationId.ShouldNotBe(failure.Report.CorrelationId);
					(stderr.Text.Length - 1).ShouldBeInRange(1, 512);
					stderr.FlushCalls.ShouldBe(0);
					stderr.Disposals.ShouldBe(0);
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= multicast;
					Utilities.Logger.DispatchFailed -= third;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Fact]
		public void ShouldCaptureSubscriptionsAfterAttemptsAndKeepThatListDuringNotification()
		{
			var calls = new List<string>();
			var recipient = new FailureDestination("recipient", new List<string>());
			Action<LogFailureReport> stale = report => calls.Add("stale");
			Action<LogFailureReport> tail = report => calls.Add("tail");
			Action<LogFailureReport> fromAttempt = report => calls.Add("attempt-added");
			Action<LogFailureReport> fromNotice = report => calls.Add("notice-added");
			var changedNotice = false;
			Action<LogFailureReport> mutator = report =>
			{
				calls.Add("mutator");
				if (changedNotice) return;
				changedNotice = true;
				Utilities.Logger.DispatchFailed -= tail;
				Utilities.Logger.DispatchFailed += fromNotice;
			};
			var changedAttempt = false;
			recipient.OnLog = () =>
			{
				if (!changedAttempt)
				{
					changedAttempt = true;
					Utilities.Logger.DispatchFailed -= stale;
					Utilities.Logger.DispatchFailed += fromAttempt;
				}
				throw new InvalidOperationException("synthetic output");
			};
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(recipient);
					Utilities.Logger.DispatchFailed += mutator;
					Utilities.Logger.DispatchFailed += stale;
					Utilities.Logger.DispatchFailed += tail;
					var first = Record.Exception(() => Utilities.Logger.Debug("first"));
					calls.ShouldBe(new[] { "mutator", "tail", "attempt-added" });
					(first is LogDispatchException).ShouldBeTrue();
					calls.Clear();
					var second = Record.Exception(() => Utilities.Logger.Debug("second"));
					calls.ShouldBe(new[] { "mutator", "attempt-added", "notice-added" });
					(second is LogDispatchException).ShouldBeTrue();
					recipient.LogCalls.ShouldBe(2);
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= mutator;
					Utilities.Logger.DispatchFailed -= stale;
					Utilities.Logger.DispatchFailed -= tail;
					Utilities.Logger.DispatchFailed -= fromAttempt;
					Utilities.Logger.DispatchFailed -= fromNotice;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Fact]
		public void ShouldPreserveDuplicateAndMulticastEventRemovalSemantics()
		{
			var calls = new List<string>();
			Action<LogFailureReport> first = report => calls.Add("first");
			Action<LogFailureReport> second = report => calls.Add("second");
			Action<LogFailureReport> separator = report => calls.Add("separator");
			var pair = first + second;
			var destination = new FailureDestination("failure", new List<string>()) { OnEligibility = () => { throw new InvalidOperationException("synthetic eligibility"); } };
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(destination);
					Utilities.Logger.DispatchFailed += null;
					Utilities.Logger.DispatchFailed -= null;
					Utilities.Logger.DispatchFailed += pair;
					Utilities.Logger.DispatchFailed += separator;
					Utilities.Logger.DispatchFailed += pair;
					Utilities.Logger.DispatchFailed += first;
					Utilities.Logger.DispatchFailed -= pair;
					var failure = Record.Exception(() => Utilities.Logger.Debug("multicast"));
					calls.ShouldBe(new[] { "first", "second", "separator", "first" });
					(failure is LogDispatchException).ShouldBeTrue();
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= pair;
					Utilities.Logger.DispatchFailed -= pair;
					Utilities.Logger.DispatchFailed -= separator;
					Utilities.Logger.DispatchFailed -= first;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Fact]
		public void ShouldFinishTheOriginalMembershipAfterEligibilityMutatesTheRouteAndThrows()
		{
			var attempts = new List<string>();
			var failing = new FailureDestination("failing", attempts);
			var removed = new FailureDestination("removed", attempts);
			var added = new FailureDestination("added", attempts);
			failing.OnEligibility = () =>
			{
				Utilities.Logger.ClearDestinations();
				Utilities.Logger.AddDestination(added);
				throw new InvalidOperationException("synthetic eligibility");
			};
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			{
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(failing);
					Utilities.Logger.AddDestination(removed);
					var failure = Record.Exception(() => Utilities.Logger.Debug("captured"));
					attempts.ShouldBe(new[] { "failing:eligibility", "removed:eligibility", "removed:log" });
					(failure is LogDispatchException).ShouldBeTrue();
					AssertFailureReport(((LogDispatchException)failure).Report, new[] { 1 }, new[] { LogFailureStage.Eligibility }, 0);
					removed.ReceivedMessage.ShouldBe("captured");
					attempts.Clear();
					Utilities.Logger.Debug("later");
					attempts.ShouldBe(new[] { "added:eligibility", "added:log" });
					added.ReceivedMessage.ShouldBe("later");
					(failing.Disposals + removed.Disposals + added.Disposals).ShouldBe(0);
				}
				finally
				{
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		[Fact]
		public void ShouldPublishAndReportOnAnotherThreadWhileANotificationIsBlocked()
		{
			var attempts = new List<string>();
			var failing = new FailureDestination("failing", attempts) { OnLog = () => { throw new InvalidOperationException("synthetic output"); } };
			var healthy = new FailureDestination("healthy", attempts);
			var added = new FailureDestination("added", attempts);
			var reports = new List<LogFailureReport>();
			var noticeThreads = new List<int>();
			var lateNotices = 0;
			var noticeCount = 0;
			Exception producerFailure = null;
			Exception workerFailure = null;
			var originalError = Console.Error;
			using (var stderr = new StringWriter())
			using (var entered = new ManualResetEventSlim())
			using (var release = new ManualResetEventSlim())
			using (var completed = new ManualResetEventSlim())
			{
				Action<LogFailureReport> late = report => Interlocked.Increment(ref lateNotices);
				Action<LogFailureReport> observer = report =>
				{
					lock (reports)
					{
						reports.Add(report);
						noticeThreads.Add(Thread.CurrentThread.ManagedThreadId);
					}
					if (Interlocked.Increment(ref noticeCount) != 1) return;
					entered.Set();
					if (!release.Wait(TimeSpan.FromSeconds(15))) throw new TimeoutException("Specification release was not signaled.");
				};
				var producer = new Thread(() => producerFailure = Record.Exception(() => Utilities.Logger.Debug("producer"))) { IsBackground = true };
				var worker = new Thread(() =>
				{
					try
					{
						Utilities.Logger.AddDestination(added);
						Utilities.Logger.DispatchFailed += late;
						workerFailure = Record.Exception(() => Utilities.Logger.Debug("worker"));
					}
					catch (Exception failure) { workerFailure = failure; }
					finally { completed.Set(); }
				}) { IsBackground = true };
				var workerStarted = false;
				var sawNotification = false;
				var independentCompletion = false;
				try
				{
					Console.SetError(stderr);
					Utilities.Logger.ClearDestinations();
					Utilities.Logger.AddDestination(failing);
					Utilities.Logger.AddDestination(healthy);
					Utilities.Logger.DispatchFailed += observer;
					producer.Start();
					try
					{
						sawNotification = entered.Wait(TimeSpan.FromSeconds(5));
						if (sawNotification)
						{
							worker.Start();
							workerStarted = true;
							independentCompletion = completed.Wait(TimeSpan.FromSeconds(5));
						}
					}
					finally
					{
						release.Set();
						var producerJoined = producer.Join(TimeSpan.FromSeconds(5));
						var workerJoined = !workerStarted || worker.Join(TimeSpan.FromSeconds(5));
						if (!producerJoined || !workerJoined)
							Environment.FailFast("Failure-report specification worker did not terminate after release; aborting before shared teardown.");
					}
					sawNotification.ShouldBeTrue();
					independentCompletion.ShouldBeTrue("B04/B14/B18 require publication and unrelated reporting without waiting for this callback");
					(producerFailure is LogDispatchException).ShouldBeTrue();
					(workerFailure is LogDispatchException).ShouldBeTrue();
					reports.Count.ShouldBe(2);
					noticeThreads.ShouldBe(new[] { producer.ManagedThreadId, worker.ManagedThreadId });
					lateNotices.ShouldBe(1);
					AssertEquivalentFailureReports(reports[0], ((LogDispatchException)producerFailure).Report);
					AssertEquivalentFailureReports(reports[1], ((LogDispatchException)workerFailure).Report);
					AssertFailureReport(reports[0], new[] { 1 }, new[] { LogFailureStage.Output }, 0);
					AssertFailureReport(reports[1], new[] { 1 }, new[] { LogFailureStage.Output }, 0);
					reports[0].CorrelationId.ShouldNotBe(reports[1].CorrelationId);
					added.LogCalls.ShouldBe(1);
					added.ReceivedMessage.ShouldBe("worker");
					healthy.LogCalls.ShouldBe(2);
					stderr.ToString().Length.ShouldBeInRange(2, 1024);
				}
				finally
				{
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.DispatchFailed -= late;
					Utilities.Logger.ClearDestinations();
					Console.SetError(originalError);
				}
			}
		}

		internal sealed class ObservingErrorWriter : TextWriter
		{
			private readonly System.Text.StringBuilder _text = new System.Text.StringBuilder();
			public Action OnWrite { get; set; }
			public Exception WriteFailure { get; set; }
			public int WriteCalls { get; private set; }
			public int FlushCalls { get; private set; }
			public int Disposals { get; private set; }
			public string Text { get { return _text.ToString(); } }
			public override System.Text.Encoding Encoding { get { return System.Text.Encoding.Unicode; } }
			private void Capture(string value)
			{
				WriteCalls++;
				OnWrite?.Invoke();
				if (WriteFailure != null)
				{
					if (!string.IsNullOrEmpty(value)) _text.Append(value[0]);
					throw WriteFailure;
				}
				_text.Append(value);
			}
			public override void Write(char value) { Capture(value.ToString()); }
			public override void Write(string value) { Capture(value); }
			public override void Write(char[] buffer, int index, int count) { Capture(new string(buffer, index, count)); }
			public override void WriteLine(string value) { Capture(value + NewLine); }
			public override void WriteLine() { Capture(NewLine); }
			public override void Flush() { FlushCalls++; }
			protected override void Dispose(bool disposing) { Disposals++; base.Dispose(disposing); }
		}

		internal sealed class OpaqueFailure : Exception
		{
			private readonly string _canary;
			public OpaqueFailure(string canary) : base(canary, new Exception(canary))
			{
				_canary = canary;
				base.Data[canary] = canary;
				base.Source = canary;
				base.HelpLink = canary;
			}
			public int DiagnosticReads { get; private set; }
			public override string Message { get { DiagnosticReads++; return _canary; } }
			public override string StackTrace { get { DiagnosticReads++; return _canary; } }
			public override System.Collections.IDictionary Data { get { DiagnosticReads++; return base.Data; } }
			public override string Source { get { DiagnosticReads++; return _canary; } set { base.Source = value; } }
			public override string HelpLink { get { DiagnosticReads++; return _canary; } set { base.HelpLink = value; } }
			public override string ToString() { DiagnosticReads++; return _canary; }
		}

		internal static void AssertNoFailureBacklinks(LogDispatchException failure, LogFailureReport notification, params object[] foreign)
		{
			var pending = new Stack<object>();
			var visited = new List<object>();
			pending.Push(failure);
			pending.Push(failure.Report);
			pending.Push(notification);
			while (pending.Count > 0)
			{
				var current = pending.Pop();
				if (current == null || visited.Any(value => ReferenceEquals(value, current))) continue;
				foreign.Any(value => ReferenceEquals(value, current)).ShouldBeFalse("B12/B19 forbid retained input, recipient and callback backlinks");
				visited.Add(current);
				if (current is string text)
				{
					foreach (var canary in foreign.OfType<string>()) text.Contains(canary).ShouldBeFalse("B12 forbids private copies of raw content too");
					continue;
				}
				(current is Exception && !(current is LogDispatchException)).ShouldBeFalse("B19 forbids a private raw-cause archive");
				var type = current.GetType();
				if (type.IsPrimitive || type.IsEnum || type == typeof(Guid) || type == typeof(IntPtr) || type == typeof(UIntPtr)) continue;
				(current is MemberInfo || current is Assembly || current is Module).ShouldBeFalse("B12 forbids reflective metadata retained in diagnostic storage; B21 inherited Exception fields are not traversed");
				if (current is Array array)
				{
					foreach (var value in array) pending.Push(value);
					continue;
				}
				for (var owner = type; owner != null && owner != typeof(object) && owner != typeof(Exception); owner = owner.BaseType)
					foreach (var field in owner.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
						pending.Push(field.GetValue(current));
			}
		}

		internal static void AssertFailureReport(LogFailureReport report, int[] registrations, LogFailureStage[] stages, int overflow)
		{
			report.ShouldNotBeNull();
			report.CorrelationId.ShouldNotBe(Guid.Empty);
			report.Failures.ShouldNotBeNull();
			report.Failures.ShouldAllBe(failure => failure != null);
			report.Failures.Select(failure => failure.RegistrationId).ShouldBe(registrations);
			report.Failures.Select(failure => failure.Stage).ShouldBe(stages);
			report.OverflowCount.ShouldBe(overflow);
		}

		internal static void AssertEquivalentFailureReports(LogFailureReport expected, LogFailureReport actual)
		{
			AssertFailureReport(actual, expected.Failures.Select(failure => failure.RegistrationId).ToArray(), expected.Failures.Select(failure => failure.Stage).ToArray(), expected.OverflowCount);
			actual.CorrelationId.ShouldBe(expected.CorrelationId);
		}

		private sealed class FailureDestination : ILoggingDestination, IDisposable
		{
			private readonly string _name;
			private readonly List<string> _attempts;
			public FailureDestination(string name, List<string> attempts) { _name = name; _attempts = attempts; }
			public Func<bool> OnEligibility { get; set; }
			public Action OnLog { get; set; }
			public int ValidationCalls { get; private set; }
			public int LogCalls { get; private set; }
			public int Disposals { get; private set; }
			public int DiagnosticCalls { get; private set; }
			public LogLevels ReceivedLevel { get; private set; }
			public string ReceivedMessage { get; private set; }
			public Exception ReceivedException { get; private set; }
			public bool ValidateMessageLevel(LogLevels level)
			{
				ValidationCalls++;
				_attempts.Add(_name + ":eligibility");
				return OnEligibility == null || OnEligibility();
			}
			public void Log(LogLevels level, string message = null, Exception ex = null)
			{
				LogCalls++;
				ReceivedLevel = level;
				ReceivedMessage = message;
				ReceivedException = ex;
				_attempts.Add(_name + ":log");
				OnLog?.Invoke();
			}
			public void Dispose() { Disposals++; }
			public override bool Equals(object other) { DiagnosticCalls++; return ReferenceEquals(this, other); }
			public override int GetHashCode() { DiagnosticCalls++; return 0; }
			public override string ToString() { DiagnosticCalls++; return "m2c-recipient-private"; }
		}

		private static void AddRegistration(bool typed, RegistrationRecipient recipient)
		{
			if (typed) Utilities.Logger.AddDestination<RegistrationMetadata>(recipient);
			else Utilities.Logger.AddDestination((ILoggingDestination)recipient);
		}

		private static void RemoveRegistration(bool typed, RegistrationRecipient recipient)
		{
			if (typed) Utilities.Logger.RemoveDestination<RegistrationMetadata>(recipient);
			else Utilities.Logger.RemoveDestination((ILoggingDestination)recipient);
		}

		private static void ClearRegistration(bool typed)
		{
			if (typed) Utilities.Logger.ClearDestinations<RegistrationMetadata>();
			else Utilities.Logger.ClearDestinations();
		}

		private static void LogRegistration(bool typed, string message)
		{
			if (typed) Utilities.Logger.Debug(message, new RegistrationMetadata());
			else Utilities.Logger.Debug(message);
		}

		private sealed class RegistrationMetadata { }

		private sealed class RegistrationRecipient : ILoggingDestination, Utilities.Generics.ILoggingDestination<RegistrationMetadata>, IDisposable
		{
			private readonly string _name;
			private readonly List<string> _received;
			public RegistrationRecipient(string name, List<string> received) { _name = name; _received = received; }
			public Action OnEligibility { get; set; }
			public Action OnLog { get; set; }
			public bool EqualToPeers { get; set; }
			public bool HostileEquality { get; set; }
			public int EqualityCalls { get; private set; }
			public int ValidationCalls { get; private set; }
			public int Disposals { get; private set; }
			public bool ValidateMessageLevel(LogLevels level) { ValidationCalls++; OnEligibility?.Invoke(); return true; }
			public void Log(LogLevels level, string message = null, Exception ex = null) { _received.Add(_name + ":" + message); OnLog?.Invoke(); }
			public void Log(LogLevels level, RegistrationMetadata metadata, string message = null, Exception ex = null) { Log(level, message, ex); }
			public void Dispose() { Disposals++; }
			public override bool Equals(object other)
			{
				EqualityCalls++;
				if (HostileEquality) throw new InvalidOperationException("User equality must not run.");
				return EqualToPeers ? other is RegistrationRecipient : ReferenceEquals(this, other);
			}
			public override int GetHashCode()
			{
				EqualityCalls++;
				if (HostileEquality) throw new InvalidOperationException("User hashing must not run.");
				return 1;
			}
		}

		private sealed class HandoffDestination : ILoggingDestination
		{
			private readonly bool _eligible;

			public HandoffDestination(bool eligible) { _eligible = eligible; }
			public int ValidationCalls { get; private set; }
			public LogLevels ValidatedLevel { get; private set; }
			public int LogCalls { get; private set; }
			public LogLevels ReceivedLevel { get; private set; }
			public string ReceivedMessage { get; private set; }
			public Exception ReceivedException { get; private set; }

			public bool ValidateMessageLevel(LogLevels messageLevel)
			{
				ValidationCalls++;
				ValidatedLevel = messageLevel;
				return _eligible;
			}

			public void Log(LogLevels level, string message = null, Exception ex = null)
			{
				LogCalls++;
				ReceivedLevel = level;
				ReceivedMessage = message;
				ReceivedException = ex;
			}
		}

		internal static void Emit(string helper, string message, Exception exception)
		{
			switch (helper)
			{
				case "Debug": Utilities.Logger.Debug(message); break;
				case "Info": Utilities.Logger.Info(message); break;
				case "Warn": Utilities.Logger.Warn(message, exception); break;
				case "Error": Utilities.Logger.Error(exception, message); break;
				case "Trace": InvokeMissing(typeof(Utilities.Logger), helper, null, message); break;
				case "Critical": InvokeMissing(typeof(Utilities.Logger), helper, null, exception, message); break;
				default: throw new InvalidOperationException("Unknown specification helper.");
			}
		}

		internal static void InvokeMissing(Type owner, string name, Type metadataType, params object[] arguments)
		{
			var method = owner.GetMethods(BindingFlags.Public | BindingFlags.Static).SingleOrDefault(candidate => candidate.Name == name && candidate.IsGenericMethodDefinition == (metadataType != null));
			method.ShouldNotBeNull("B11 requires the reviewed " + owner.FullName + "." + name + " export in the referenced library");
			if (metadataType != null) method = method.MakeGenericMethod(metadataType);
			try { method.Invoke(null, arguments); }
			catch (TargetInvocationException exception)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
				throw;
			}
		}

		private sealed class GuardDestination : ILoggingDestination
		{
			public int ValidationCalls { get; private set; }
			public int LogCalls { get; private set; }
			public bool ValidateMessageLevel(LogLevels level) { ValidationCalls++; return false; }
			public void Log(LogLevels level, string message = null, Exception ex = null) { LogCalls++; }
		}
	}
}
