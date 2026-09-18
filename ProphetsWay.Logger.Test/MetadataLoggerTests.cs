using System;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.Generics;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
    [Collection("Logger is a Singleton")]
    public class MetadataLoggerTests
    {
        public class myMetadata : ILoggerMetadata
        {
            public bool State = true;
        }

        [Fact]
        public void ShouldTriggerDebugOnDebug()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Debug);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            try
            {
                ctx.Debug("Hello World!");
                triggered.Should().BeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerInfoOnInfo()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Information);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            try
            {
                ctx.Info("Hello World!");
                triggered.Should().BeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerCriticalOnCritical()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>((LogLevels)1);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();
            try
            {
                Emit("Critical", ctx, "Hello World!", new Exception("synthetic detail"));
                triggered.ShouldBeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerWarnOnWarn()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Warning);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            try
            {
                ctx.Warn("Hello World!");
                triggered.Should().BeTrue();
            }
            finally { Utilities.Logger.RemoveDestination(dest); }
        }

        [Fact]
        public void ShouldTriggerErrorOnError()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Error);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            try
            {
                ctx.Error(new Exception("Hello World!"));
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
        public void ShouldEmitExactSeverityAndPreserveReferenceOrNullReceiver(string helper, int bit)
        {
            var destination = new GenericEventDestination<myMetadata>((LogLevels)63);
            GenericEventDestination<myMetadata>.LoggerEventArgs result = null;
            var calls = 0;
            destination.LoggingEvent += (sender, args) => { calls++; result = args; };
            Utilities.Logger.AddDestination(destination);
            try
            {
                foreach (var metadata in new[] { new myMetadata(), null })
                    foreach (var message in new[] { "Synthetic CONTEXT", "", " \t " })
                    {
                        calls = 0;
                        result = null;
                        var exception = helper == "Error" || helper == "Critical" || (helper == "Warn" && message.Length > 0) ? new Exception("Synthetic DETAIL") : null;
                        Emit(helper, metadata, message, exception);
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
        public void ShouldPermitDefaultValueTypeMarkerMetadata(string helper)
        {
            var destination = new GenericEventDestination<ValueMetadata>((LogLevels)63);
            GenericEventDestination<ValueMetadata>.LoggerEventArgs result = null;
            destination.LoggingEvent += (sender, args) => result = args;
            Utilities.Logger.AddDestination(destination);
            try
            {
                foreach (var metadata in new[] { default(ValueMetadata), new ValueMetadata(42) })
                {
                    result = null;
                    Emit(helper, metadata, "value receiver", new Exception("synthetic detail"));
                    result.ShouldNotBeNull();
                    result.Metadata.Value.ShouldBe(metadata.Value);
                    result.RawMessage.ShouldBe("value receiver");
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
        public void ShouldValidateRequiredExtensionArgumentsBeforeRecipientEligibility(string helper, string parameter)
        {
            var destination = new GuardDestination();
            Utilities.Logger.AddDestination(destination);
            try
            {
                var failure = Record.Exception(() => Emit(helper, new myMetadata(), parameter == "message" ? null : "synthetic context", parameter == "ex" ? null : new Exception("synthetic detail")));
                destination.ValidationCalls.ShouldBe(0);
                destination.LogCalls.ShouldBe(0);
                failure.ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe(parameter);
            }
            finally { Utilities.Logger.RemoveDestination(destination); }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShouldKeepAbsentErrorContextNullThroughTheExtension(bool explicitlyNull)
        {
            var destination = new GenericEventDestination<myMetadata>((LogLevels)63);
            GenericEventDestination<myMetadata>.LoggerEventArgs result = null;
            destination.LoggingEvent += (sender, args) => result = args;
            Utilities.Logger.AddDestination(destination);
            try
            {
                var exception = new Exception("synthetic error detail");
                var metadata = new myMetadata();
                if (explicitlyNull) metadata.Error(exception, null);
                else metadata.Error(exception);
                result.ShouldNotBeNull();
                result.RawMessage.ShouldBeNull();
                result.Exception.ShouldBeSameAs(exception);
                result.Metadata.ShouldBeSameAs(metadata);
                result.Message.ShouldContain(exception.Message, Case.Sensitive);
                result.LogLevel.ShouldBe((LogLevels)2);
            }
            finally { Utilities.Logger.RemoveDestination(destination); }
        }

        private static void Emit<T>(string helper, T metadata, string message, Exception exception) where T : ILoggerMetadata
        {
            switch (helper)
            {
                case "Debug": metadata.Debug(message); break;
                case "Info": metadata.Info(message); break;
                case "Warn": metadata.Warn(message, exception); break;
                case "Error": metadata.Error(exception, message); break;
                case "Trace": LoggerTests.InvokeMissing(typeof(MetadataExtensions), helper, typeof(T), metadata, message); break;
                case "Critical": LoggerTests.InvokeMissing(typeof(MetadataExtensions), helper, typeof(T), metadata, exception, message); break;
                default: throw new InvalidOperationException("Unknown specification helper.");
            }
        }

        private struct ValueMetadata : ILoggerMetadata
        {
            public ValueMetadata(int value) { Value = value; }
            public int Value { get; }
        }

        private sealed class GuardDestination : ILoggingDestination<myMetadata>
        {
            public int ValidationCalls { get; private set; }
            public int LogCalls { get; private set; }
            public bool ValidateMessageLevel(LogLevels level) { ValidationCalls++; return false; }
            public void Log(LogLevels level, myMetadata metadata, string message = null, Exception ex = null) { LogCalls++; }
        }
    }
}
