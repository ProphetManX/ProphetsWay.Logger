using System;
using System.Collections.Generic;
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
