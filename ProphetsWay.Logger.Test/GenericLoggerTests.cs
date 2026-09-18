using System;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
    [Collection("Logger is a Singleton")]
    public class GenericLoggerTests
    {
        [Fact]
        public void ShouldTriggerDebugOnDebug()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Debug);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            try
            {
                Utilities.Logger.Debug("Hello World!", true);
                triggered.Should().BeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerInfoOnInfo()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Information);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            try
            {
                Utilities.Logger.Info("Hello World!", true);
                triggered.Should().BeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerCriticalOnCritical()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>((LogLevels)1);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);
            try
            {
                Emit("Critical", "Hello World!", new Exception("synthetic detail"), true);
                triggered.ShouldBeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerWarnOnWarn()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Warning);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            try
            {
                Utilities.Logger.Warn("Hello World!", true);
                triggered.Should().BeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerErrorOnError()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Error);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            try
            {
                Utilities.Logger.Error(new Exception("Hello World!"), true);
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
        public void ShouldEmitExactSeverityAndPreserveContentAndMetadata(string helper, int bit)
        {
            var destination = new GenericEventDestination<object>((LogLevels)63);
            GenericEventDestination<object>.LoggerEventArgs result = null;
            var calls = 0;
            destination.LoggingEvent += (sender, args) => { calls++; result = args; };
            Utilities.Logger.AddDestination(destination);
            try
            {
                foreach (var metadata in new[] { new object(), null })
                    foreach (var message in new[] { "Synthetic CONTEXT", "", " \t " })
                    {
                        calls = 0;
                        result = null;
                        var exception = helper == "Error" || helper == "Critical" || (helper == "Warn" && message.Length > 0) ? new Exception("Synthetic DETAIL") : null;
                        Emit(helper, message, exception, metadata);
                        calls.ShouldBe(1);
                        result.ShouldNotBeNull();
                        result.LogLevel.ShouldBe((LogLevels)bit);
                        result.RawMessage.ShouldBe(message);
                        result.Exception.ShouldBeSameAs(exception);
                        result.Metadata.ShouldBeSameAs(metadata);
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
        [InlineData("Trace")]
        [InlineData("Debug")]
        [InlineData("Info")]
        [InlineData("Warn")]
        [InlineData("Error")]
        [InlineData("Critical")]
        public void ShouldPermitDefaultValueMetadataWithoutAMarkerConstraint(string helper)
        {
            var destination = new GenericEventDestination<int>((LogLevels)63);
            GenericEventDestination<int>.LoggerEventArgs result = null;
            destination.LoggingEvent += (sender, args) => result = args;
            Utilities.Logger.AddDestination(destination);
            try
            {
                foreach (var metadata in new[] { default(int), 42 })
                {
                    result = null;
                    Emit(helper, "value metadata", new Exception("synthetic detail"), metadata);
                    result.ShouldNotBeNull();
                    result.Metadata.ShouldBe(metadata);
                    result.RawMessage.ShouldBe("value metadata");
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
        public void ShouldValidateRequiredArgumentsBeforeTypedRecipientEligibility(string helper, string parameter)
        {
            var destination = new GuardDestination();
            Utilities.Logger.AddDestination(destination);
            try
            {
                var failure = Record.Exception(() => Emit(helper, parameter == "message" ? null : "synthetic context", parameter == "ex" ? null : new Exception("synthetic detail"), new object()));
                destination.ValidationCalls.ShouldBe(0);
                destination.LogCalls.ShouldBe(0);
                failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe(parameter);
            }
            finally { Utilities.Logger.RemoveDestination(destination); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShouldKeepAbsentErrorContextNullWithMetadata(bool explicitlyNull)
        {
            var destination = new GenericEventDestination<object>((LogLevels)63);
            GenericEventDestination<object>.LoggerEventArgs result = null;
            destination.LoggingEvent += (sender, args) => result = args;
            Utilities.Logger.AddDestination(destination);
            try
            {
                var exception = new Exception("synthetic error detail");
                var metadata = new object();
                if (explicitlyNull) Utilities.Logger.Error(exception, metadata, null);
                else Utilities.Logger.Error(exception, metadata);
                result.ShouldNotBeNull();
                result.RawMessage.ShouldBeNull();
                result.Exception.ShouldBeSameAs(exception);
                result.Metadata.ShouldBeSameAs(metadata);
                result.Message.ShouldContain(exception.Message, Case.Sensitive);
                result.LogLevel.ShouldBe((LogLevels)2);
            }
            finally { Utilities.Logger.RemoveDestination(destination); }
        }

        [Fact]
        public void ShouldRejectNullTypedRegistrationWithoutDisturbingTheExplicitRoute()
        {
            var destination = new GenericEventDestination<object>((LogLevels)63);
            string received = null;
            destination.LoggingEvent += (sender, args) => received = args.RawMessage;
            Utilities.Logger.AddDestination(destination);
            try
            {
                var failure = Record.Exception(() => Utilities.Logger.AddDestination<object>(null));
                failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("newDest");
                Utilities.Logger.Debug("route remains usable", new object());
                received.ShouldBe("route remains usable");
            }
            finally
            {
                Utilities.Logger.RemoveDestination<object>(null);
                Utilities.Logger.RemoveDestination(destination);
            }
        }

        [Fact]
        public void ShouldNotFallBackWhenTheExplicitTypedDestinationRejectsAll()
        {
            var destination = new GenericEventDestination<object>((LogLevels)0);
            var fallbackObserver = new EventDestination((LogLevels)63);
            var typedCallbacks = 0;
            var ordinaryCallbacks = 0;
            destination.LoggingEvent += (sender, args) => typedCallbacks++;
            fallbackObserver.LoggingEvent += (sender, args) => ordinaryCallbacks++;
            Utilities.Logger.AddDestination(fallbackObserver);
            Utilities.Logger.AddDestination(destination);
            try
            {
                Utilities.Logger.Debug("synthetic denied entry", new object());
                Utilities.Logger.Error(new Exception("synthetic denied detail"), new object());
                typedCallbacks.ShouldBe(0);
                ordinaryCallbacks.ShouldBe(0);
            }
            finally
            {
                Utilities.Logger.RemoveDestination(destination);
                Utilities.Logger.RemoveDestination(fallbackObserver);
            }
        }

        [Fact]
        public void ShouldRespectCustomTypedRecipientEligibilityBeforeRawHandoff()
        {
            var rejected = new HandoffDestination(false);
            var accepted = new HandoffDestination(true);
            const string message = "Synthetic RAW typed context";
            var exception = new Exception("Synthetic RAW typed detail");
            var metadata = new object();
            try
            {
                Utilities.Logger.AddDestination(rejected);
                Utilities.Logger.AddDestination(accepted);
                Utilities.Logger.Warn(message, metadata, exception);

                rejected.ValidationCalls.ShouldBeGreaterThan(0);
                rejected.ValidatedLevel.ShouldBe(LogLevels.WarningOnly);
                rejected.LogCalls.ShouldBe(0);
                rejected.ReceivedMessage.ShouldBeNull();
                rejected.ReceivedException.ShouldBeNull();
                rejected.ReceivedMetadata.ShouldBeNull();
                accepted.ValidationCalls.ShouldBeGreaterThan(0);
                accepted.ValidatedLevel.ShouldBe(LogLevels.WarningOnly);
                accepted.LogCalls.ShouldBe(1);
                accepted.ReceivedLevel.ShouldBe(LogLevels.WarningOnly);
                accepted.ReceivedMessage.ShouldBe(message);
                accepted.ReceivedException.ShouldBeSameAs(exception);
                accepted.ReceivedMetadata.ShouldBeSameAs(metadata);
            }
            finally
            {
                Utilities.Logger.RemoveDestination(rejected);
                Utilities.Logger.RemoveDestination(accepted);
            }
        }

        private sealed class HandoffDestination : Utilities.Generics.ILoggingDestination<object>
        {
            private readonly bool _eligible;

            public HandoffDestination(bool eligible) { _eligible = eligible; }
            public int ValidationCalls { get; private set; }
            public LogLevels ValidatedLevel { get; private set; }
            public int LogCalls { get; private set; }
            public LogLevels ReceivedLevel { get; private set; }
            public string ReceivedMessage { get; private set; }
            public Exception ReceivedException { get; private set; }
            public object ReceivedMetadata { get; private set; }

            public bool ValidateMessageLevel(LogLevels messageLevel)
            {
                ValidationCalls++;
                ValidatedLevel = messageLevel;
                return _eligible;
            }

            public void Log(LogLevels level, object metadata, string message = null, Exception ex = null)
            {
                LogCalls++;
                ReceivedLevel = level;
                ReceivedMessage = message;
                ReceivedException = ex;
                ReceivedMetadata = metadata;
            }
        }

        internal static void Emit<T>(string helper, string message, Exception exception, T metadata)
        {
            switch (helper)
            {
                case "Debug": Utilities.Logger.Debug(message, metadata); break;
                case "Info": Utilities.Logger.Info(message, metadata); break;
                case "Warn": Utilities.Logger.Warn(message, metadata, exception); break;
                case "Error": Utilities.Logger.Error(exception, metadata, message); break;
                case "Trace": LoggerTests.InvokeMissing(typeof(Utilities.Logger), helper, typeof(T), message, metadata); break;
                case "Critical": LoggerTests.InvokeMissing(typeof(Utilities.Logger), helper, typeof(T), exception, metadata, message); break;
                default: throw new InvalidOperationException("Unknown specification helper.");
            }
        }

        private sealed class GuardDestination : Utilities.Generics.ILoggingDestination<object>
        {
            public int ValidationCalls { get; private set; }
            public int LogCalls { get; private set; }
            public bool ValidateMessageLevel(LogLevels level) { ValidationCalls++; return false; }
            public void Log(LogLevels level, object metadata, string message = null, Exception ex = null) { LogCalls++; }
        }
    }
}
