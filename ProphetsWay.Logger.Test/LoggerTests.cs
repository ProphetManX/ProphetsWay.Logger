using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
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
