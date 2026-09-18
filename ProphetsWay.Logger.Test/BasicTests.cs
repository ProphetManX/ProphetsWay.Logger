using System;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
    [Collection("Logger is a Singleton")]
    public class BasicTests
    {
        [Fact]
        public void ShouldContainMessage()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Debug);
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);
            try
            {
                Utilities.Logger.Debug(msg);
                evtMessage.Should().Contain(msg);
            }
            finally { Utilities.Logger.RemoveDestination(d); }
        }

        [Fact]
        public void ShouldContainException()
        {
            //setup
            var e = new Exception("Hello World!");
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Error);
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);
            try
            {
                Utilities.Logger.Error(e, "Goodbye Everyone");
                evtMessage.Should().Contain(e.Message);
            }
            finally { Utilities.Logger.RemoveDestination(d); }
        }

        [Fact]
        public void ShouldContainMessageAndException()
        {
            //setup
            const string msg = "Goodbye Everyone";
            var e = new Exception("Hello World!");
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Error);
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);
            try
            {
                Utilities.Logger.Error(e, msg);
                evtMessage.Should().Contain(msg).And.Contain(e.Message);
            }
            finally { Utilities.Logger.RemoveDestination(d); }
        }

        [Fact]
        public void ShouldContainMessageLevelNamed()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination("Debug");
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);
            try
            {
                Utilities.Logger.Debug(msg);
                evtMessage.Should().Contain(msg);
            }
            finally { Utilities.Logger.RemoveDestination(d); }
        }

        [Fact]
        public void ShouldRejectMalformedReportingLevelName()
        {
            var exception = Should.Throw<ArgumentException>(() => new EventDestination("NOT_VALID"));
            exception.GetType().ShouldBe(typeof(ArgumentException));
            exception.ParamName.ShouldBe("strReportingLevel");
        }

        [Fact]
        public void ShouldContainMessageLevelInt()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Debug.GetHashCode());
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);
            try
            {
                Utilities.Logger.Debug(msg);
                evtMessage.Should().Contain(msg);
            }
            finally { Utilities.Logger.RemoveDestination(d); }
        }

        [Fact]
        public void ShouldRejectNegativeReportingLevelInt()
        {
            var exception = Should.Throw<ArgumentOutOfRangeException>(() => new EventDestination(-1));
            exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
            exception.ParamName.ShouldBe("intReportingLevel");
        }

        [Fact]
        public void ShouldExposeExactlyTheReviewedSeverityNamesAndBits()
        {
            var names = new[] { "Critical", "ErrorOnly", "Error", "WarningOnly", "Warning", "InformationOnly", "Information", "DebugOnly", "Debug", "TraceOnly", "Trace" };
            var values = new[] { 1, 2, 3, 4, 7, 8, 15, 16, 31, 32, 63 };
            typeof(LogLevels).Namespace.ShouldBe("ProphetsWay.Utilities");
            Enum.GetUnderlyingType(typeof(LogLevels)).ShouldBe(typeof(int));
            typeof(LogLevels).IsDefined(typeof(FlagsAttribute), false).ShouldBeTrue();
            Enum.GetNames(typeof(LogLevels)).ShouldBe(names);
            Enum.GetValues(typeof(LogLevels)).Cast<LogLevels>().Select(value => (int)value).ShouldBe(values);
        }

        [Fact]
        public void ShouldUseAllBitsForEveryValidRawMaskWithoutMutatingTheDestination()
        {
            for (var destinationMask = 0; destinationMask <= 63; destinationMask++)
            {
                var destination = new CoreProbe((LogLevels)destinationMask);
                for (var messageMask = 1; messageMask <= 63; messageMask++)
                {
                    var expected = (destinationMask & messageMask) == messageMask;
                    destination.ValidateMessageLevel((LogLevels)messageMask).ShouldBe(expected);
                    destination.ValidateMessageLevel((LogLevels)messageMask).ShouldBe(expected);
                }
            }
        }

        [Fact]
        public void ShouldPreserveEveryMaskAcrossIntegerAndDecimalRepresentations()
        {
            for (var mask = 0; mask <= 63; mask++)
            {
                var destinations = new LoggingDestinationCore[]
                {
                    new CoreProbe(mask), new CoreProbe(mask.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    new EventDestination(mask), new GenericEventDestination<object>(mask), new TextProbe(mask)
                };
                foreach (var destination in destinations)
                    for (var messageMask = 1; messageMask <= 63; messageMask++)
                        destination.ValidateMessageLevel((LogLevels)messageMask).ShouldBe((mask & messageMask) == messageMask);
            }
        }

        [Theory]
        [InlineData("0", 0)]
        [InlineData("9", 9)]
        [InlineData(" +009 ", 9)]
        [InlineData("-0", 0)]
        [InlineData("Critical, InformationOnly", 9)]
        [InlineData(" Critical , Critical ", 1)]
        [InlineData("ErrorOnly, WarningOnly", 6)]
        [InlineData("Error", 3)]
        [InlineData("Warning", 7)]
        [InlineData("Information", 15)]
        [InlineData("Debug", 31)]
        [InlineData("TraceOnly", 32)]
        [InlineData("Trace", 63)]
        public void ShouldRetainTheCaseSensitiveEnumParserGrammar(string representation, int mask)
        {
            var destinations = new LoggingDestinationCore[]
            {
                new CoreProbe(representation), new EventDestination(representation),
                new GenericEventDestination<object>(representation), new TextProbe(representation)
            };
            foreach (var destination in destinations)
                for (var messageMask = 1; messageMask <= 63; messageMask++)
                    destination.ValidateMessageLevel((LogLevels)messageMask).ShouldBe((mask & messageMask) == messageMask);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(64)]
        [InlineData(65)]
        [InlineData(int.MaxValue)]
        public void ShouldRejectNegativeAndUnknownReportingBits(int mask)
        {
            AssertArgumentError(Record.Exception(() => new CoreProbe((LogLevels)mask)), typeof(ArgumentOutOfRangeException), "reportingLevel");
            AssertArgumentError(Record.Exception(() => new CoreProbe(mask)), typeof(ArgumentOutOfRangeException), "intReportingLevel");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        [InlineData("critical")]
        [InlineData("Security")]
        [InlineData("SecurityOnly")]
        [InlineData("Unknown")]
        [InlineData("Critical,")]
        [InlineData(",Critical")]
        [InlineData("Critical,,InformationOnly")]
        [InlineData("Critical|InformationOnly")]
        [InlineData("Critical, 8")]
        [InlineData("1, 8")]
        [InlineData("0x9")]
        [InlineData("2147483648")]
        [InlineData("-2147483649")]
        [InlineData("-1")]
        [InlineData("64")]
        [InlineData("65")]
        public void ShouldRejectInvalidReportingTextWithoutFallback(string representation)
        {
            var expected = representation == null ? typeof(ArgumentNullException) : typeof(ArgumentException);
            AssertArgumentError(Record.Exception(() => new CoreProbe(representation)), expected, "strReportingLevel");
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(64)]
        [InlineData(65)]
        public void ShouldRejectInvalidRawMasksEvenForRejectAll(int mask)
        {
            foreach (var destinationMask in new[] { 0, 63 })
            {
                var destination = new CoreProbe((LogLevels)destinationMask);
                AssertArgumentError(Record.Exception(() => destination.ValidateMessageLevel((LogLevels)mask)), typeof(ArgumentOutOfRangeException), "messageLevel");
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(int.MinValue)]
        [InlineData(64)]
        [InlineData(65)]
        public void ShouldRejectInvalidDirectMasksBeforeMassageEvenWithoutSubscribers(int mask)
        {
            var ordinary = new EventProbe((LogLevels)63);
            var generic = new GenericEventProbe((LogLevels)63);
            var text = new TextProbe((LogLevels)63);
            var failures = new[]
            {
                Record.Exception(() => ordinary.Log((LogLevels)mask)),
                Record.Exception(() => generic.Log((LogLevels)mask, null)),
                Record.Exception(() => text.Log((LogLevels)mask))
            };
            ordinary.MassageCalls.ShouldBe(0);
            generic.MassageCalls.ShouldBe(0);
            text.MassageCalls.ShouldBe(0);
            text.PrintCalls.ShouldBe(0);
            foreach (var failure in failures)
                AssertArgumentError(failure, typeof(ArgumentOutOfRangeException), "level");
        }

        [Theory]
        [InlineData(8, 9)]
        [InlineData(0, 1)]
        [InlineData(0, 63)]
        [InlineData(31, 32)]
        public void ShouldWithholdRejectedDirectCallsBeforeAnyRecipientWork(int destinationMask, int messageMask)
        {
            var ordinary = new EventProbe((LogLevels)destinationMask);
            var generic = new GenericEventProbe((LogLevels)destinationMask);
            var text = new TextProbe((LogLevels)destinationMask);
            var ordinaryCallbacks = 0;
            var genericCallbacks = 0;
            ordinary.LoggingEvent += (sender, args) => ordinaryCallbacks++;
            generic.LoggingEvent += (sender, args) => genericCallbacks++;
            var exception = new Exception("synthetic rejected detail");
            ordinary.Log((LogLevels)messageMask, "synthetic rejected context", exception);
            generic.Log((LogLevels)messageMask, new object(), "synthetic rejected context", exception);
            text.Log((LogLevels)messageMask, "synthetic rejected context", exception);
            ordinary.MassageCalls.ShouldBe(0);
            generic.MassageCalls.ShouldBe(0);
            text.MassageCalls.ShouldBe(0);
            ordinaryCallbacks.ShouldBe(0);
            genericCallbacks.ShouldBe(0);
            text.PrintCalls.ShouldBe(0);
        }

        [Theory]
        [InlineData(15, 9)]
        [InlineData(63, 63)]
        [InlineData(63, 32)]
        [InlineData(3, 2)]
        public void ShouldDeliverEligibleDirectCallsWithRawAndMassagedValuesDistinct(int destinationMask, int messageMask)
        {
            var ordinary = new EventProbe((LogLevels)destinationMask);
            var generic = new GenericEventProbe((LogLevels)destinationMask);
            var text = new TextProbe((LogLevels)destinationMask);
            var exception = new Exception("synthetic detail");
            var metadata = new object();
            EventDestination.LoggerEventArgs ordinaryResult = null;
            GenericEventDestination<object>.LoggerEventArgs genericResult = null;
            object ordinarySender = null;
            object genericSender = null;
            ordinary.LoggingEvent += (sender, args) => { ordinarySender = sender; ordinaryResult = args; };
            generic.LoggingEvent += (sender, args) => { genericSender = sender; genericResult = args; };
            ordinary.Log((LogLevels)messageMask, "raw context", exception);
            generic.Log((LogLevels)messageMask, metadata, "raw context", exception);
            text.Log((LogLevels)messageMask, "raw context", exception);
            ordinaryResult.ShouldNotBeNull();
            genericResult.ShouldNotBeNull();
            ordinarySender.ShouldBeSameAs(ordinary);
            genericSender.ShouldBeSameAs(generic);
            ordinaryResult.LogLevel.ShouldBe((LogLevels)messageMask);
            genericResult.LogLevel.ShouldBe((LogLevels)messageMask);
            ordinaryResult.RawMessage.ShouldBe("raw context");
            genericResult.RawMessage.ShouldBe("raw context");
            ordinaryResult.Exception.ShouldBeSameAs(exception);
            genericResult.Exception.ShouldBeSameAs(exception);
            genericResult.Metadata.ShouldBeSameAs(metadata);
            ordinaryResult.Message.ShouldBe("massaged-entry");
            genericResult.Message.ShouldBe("massaged-entry");
            ordinary.MassageCalls.ShouldBe(1);
            generic.MassageCalls.ShouldBe(1);
            text.MassageCalls.ShouldBe(1);
            text.PrintCalls.ShouldBe(1);
            text.Printed.ShouldContain("massaged-entry", Case.Sensitive);
            text.Printed.ShouldContain(((LogLevels)messageMask).ToString(), Case.Sensitive);
            ordinary.MassagedLevel.ShouldBe((LogLevels)messageMask);
            generic.MassagedLevel.ShouldBe((LogLevels)messageMask);
            text.MassagedLevel.ShouldBe((LogLevels)messageMask);
            ordinary.MassagedMessage.ShouldBe("raw context");
            generic.MassagedMessage.ShouldBe("raw context");
            text.MassagedMessage.ShouldBe("raw context");
            ordinary.MassagedException.ShouldBeSameAs(exception);
            generic.MassagedException.ShouldBeSameAs(exception);
            text.MassagedException.ShouldBeSameAs(exception);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" \t ")]
        [InlineData("Unchanged CONTEXT")]
        public void ShouldPreserveRawAbsencesAndTextAtEveryValidSeverity(string message)
        {
            var core = new CoreProbe((LogLevels)63);
            var ordinary = new EventDestination((LogLevels)63);
            var generic = new GenericEventDestination<object>((LogLevels)63);
            EventDestination.LoggerEventArgs ordinaryResult = null;
            GenericEventDestination<object>.LoggerEventArgs genericResult = null;
            ordinary.LoggingEvent += (sender, args) => ordinaryResult = args;
            generic.LoggingEvent += (sender, args) => genericResult = args;
            for (var mask = 1; mask <= 63; mask++)
            {
                core.Massage((LogLevels)mask, message, null).ShouldBe(message);
                ordinaryResult = null;
                genericResult = null;
                ordinary.Log((LogLevels)mask, message);
                generic.Log((LogLevels)mask, null, message);
                ordinaryResult.ShouldNotBeNull();
                genericResult.ShouldNotBeNull();
                ordinaryResult.RawMessage.ShouldBe(message);
                ordinaryResult.Message.ShouldBe(message);
                ordinaryResult.Exception.ShouldBeNull();
                ordinaryResult.LogLevel.ShouldBe((LogLevels)mask);
                genericResult.RawMessage.ShouldBe(message);
                genericResult.Message.ShouldBe(message);
                genericResult.Exception.ShouldBeNull();
                genericResult.Metadata.ShouldBeNull();
                genericResult.LogLevel.ShouldBe((LogLevels)mask);
            }
        }

        [Fact]
        public void ShouldPreserveContextAndNestedExceptionDetailsAtEveryValidMask()
        {
            var exception = CreateNestedException();
            exception.StackTrace.ShouldNotBeNullOrEmpty();
            exception.InnerException.StackTrace.ShouldNotBeNullOrEmpty();
            var core = new CoreProbe((LogLevels)63);
            for (var mask = 1; mask <= 63; mask++)
            {
                var result = core.Massage((LogLevels)mask, "  Preserved CONTEXT  ", exception);
                result.ShouldContain("  Preserved CONTEXT  ", Case.Sensitive);
                result.ShouldContain(exception.Message, Case.Sensitive);
                result.ShouldContain(exception.StackTrace, Case.Sensitive);
                result.ShouldContain(exception.InnerException.Message, Case.Sensitive);
                result.ShouldContain(exception.InnerException.StackTrace, Case.Sensitive);
            }
        }

        [Fact]
        public void ShouldRetainRawSignaturesForwardingConstructorsAndEventCarriers()
        {
            var owners = new[] { typeof(ILoggingDestination), typeof(Utilities.Generics.ILoggingDestination<object>), typeof(BaseLoggingDestination), typeof(Utilities.Generics.BaseLoggingDestination<object>), typeof(EventDestination), typeof(GenericEventDestination<object>), typeof(TextBasedDestination) };
            foreach (var owner in owners)
            {
                var method = owner.GetMethod("Log");
                method.ShouldNotBeNull();
                method.ReturnType.ShouldBe(typeof(void));
                var parameters = method.GetParameters();
                var typed = owner.IsGenericType;
                parameters.Select(parameter => parameter.Name).ShouldBe(typed ? new[] { "level", "metadata", "message", "ex" } : new[] { "level", "message", "ex" });
                parameters.Select(parameter => parameter.ParameterType).ShouldBe(typed ? new[] { typeof(LogLevels), typeof(object), typeof(string), typeof(Exception) } : new[] { typeof(LogLevels), typeof(string), typeof(Exception) });
                parameters[0].IsOptional.ShouldBeFalse();
                if (typed) parameters[1].IsOptional.ShouldBeFalse();
                foreach (var parameter in parameters.Skip(typed ? 2 : 1))
                {
                    parameter.IsOptional.ShouldBeTrue();
                    parameter.DefaultValue.ShouldBeNull();
                }
            }
            foreach (var owner in owners.Where(owner => !owner.IsInterface).Concat(new[] { typeof(LoggingDestinationCore) }))
            {
                var constructors = owner.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                constructors.Length.ShouldBe(3);
                var parameterTypes = new[] { typeof(LogLevels), typeof(string), typeof(int) };
                var parameterNames = new[] { "reportingLevel", "strReportingLevel", "intReportingLevel" };
                for (var index = 0; index < parameterTypes.Length; index++)
                {
                    var constructor = constructors.Single(candidate => candidate.GetParameters().Length == 1 && candidate.GetParameters()[0].ParameterType == parameterTypes[index]);
                    constructor.GetParameters()[0].Name.ShouldBe(parameterNames[index]);
                    constructor.GetParameters()[0].IsOptional.ShouldBeFalse();
                    constructor.IsPublic.ShouldBe(owner == typeof(EventDestination) || owner == typeof(GenericEventDestination<object>) || owner == typeof(LoggingDestinationCore));
                    if (!constructor.IsPublic) constructor.IsFamily.ShouldBeTrue();
                }
            }
            typeof(EventDestination).GetField("LoggingEvent").FieldType.ShouldBe(typeof(EventHandler<EventDestination.LoggerEventArgs>));
            typeof(GenericEventDestination<object>).GetField("LoggingEvent").FieldType.ShouldBe(typeof(EventHandler<GenericEventDestination<object>.LoggerEventArgs>));
            var before = DateTime.Now;
            var ordinary = new EventDestination.LoggerEventArgs("rendered", (LogLevels)0, null, null);
            var generic = new GenericEventDestination<object>.LoggerEventArgs(null, (LogLevels)0, null, null, "rendered");
            var after = DateTime.Now;
            ordinary.Message.ShouldBe("rendered");
            generic.Message.ShouldBe("rendered");
            ordinary.RawMessage.ShouldBeNull();
            generic.RawMessage.ShouldBeNull();
            ordinary.Exception.ShouldBeNull();
            generic.Exception.ShouldBeNull();
            generic.Metadata.ShouldBeNull();
            ordinary.LogLevel.ShouldBe((LogLevels)0);
            generic.LogLevel.ShouldBe((LogLevels)0);
            foreach (var timestamp in new[] { ordinary.Timestamp, generic.Timestamp })
            {
                timestamp.Kind.ShouldBe(DateTimeKind.Local);
                timestamp.ShouldBeInRange(before, after);
            }
            foreach (var carrier in new[] { ordinary.GetType(), generic.GetType() })
            {
                carrier.BaseType.ShouldBe(typeof(EventArgs));
                foreach (var property in carrier.GetProperties()) property.GetSetMethod(true).ShouldBeNull();
                foreach (var parameter in carrier.GetConstructors().Single().GetParameters()) parameter.IsOptional.ShouldBeFalse();
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(4)]
        [InlineData(7)]
        [InlineData(9)]
        [InlineData(63)]
        public void ShouldPrintPermittedRawAbsenceAndPreservedExceptionContent(int mask)
        {
            var destination = new CapturingTextDestination();
            destination.Log((LogLevels)mask);
            destination.PrintCalls.ShouldBe(1);
            destination.Printed.ShouldContain(((LogLevels)mask).ToString(), Case.Sensitive);
            var exception = CreateNestedException();
            destination.Log((LogLevels)mask, "  Text CONTEXT  ", exception);
            destination.PrintCalls.ShouldBe(2);
            destination.Printed.ShouldContain("  Text CONTEXT  ", Case.Sensitive);
            destination.Printed.ShouldContain(exception.Message, Case.Sensitive);
            destination.Printed.ShouldContain(exception.StackTrace, Case.Sensitive);
            destination.Printed.ShouldContain(exception.InnerException.Message, Case.Sensitive);
            destination.Printed.ShouldContain(exception.InnerException.StackTrace, Case.Sensitive);
        }

        [Fact]
        public void ShouldRetainTheCoreQueryAndProtectedHookSignatures()
        {
            foreach (var owner in new[] { typeof(IDestination), typeof(LoggingDestinationCore) })
            {
                var query = owner.GetMethod("ValidateMessageLevel");
                query.ReturnType.ShouldBe(typeof(bool));
                var parameter = query.GetParameters().Single();
                parameter.ParameterType.ShouldBe(typeof(LogLevels));
                parameter.Name.ShouldBe("messageLevel");
                parameter.IsOptional.ShouldBeFalse();
            }
            var massage = typeof(LoggingDestinationCore).GetMethod("MassageLogStatement", BindingFlags.Instance | BindingFlags.NonPublic);
            massage.IsFamily.ShouldBeTrue();
            massage.IsVirtual.ShouldBeTrue();
            massage.ReturnType.ShouldBe(typeof(string));
            massage.GetParameters().Select(parameter => parameter.Name).ShouldBe(new[] { "level", "message", "ex" });
            massage.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe(new[] { typeof(LogLevels), typeof(string), typeof(Exception) });
            massage.GetParameters()[0].IsOptional.ShouldBeFalse();
            foreach (var parameter in massage.GetParameters().Skip(1))
            {
                parameter.IsOptional.ShouldBeTrue();
                parameter.DefaultValue.ShouldBeNull();
            }
            var print = typeof(TextBasedDestination).GetMethod("PrintLogEntry", BindingFlags.Instance | BindingFlags.NonPublic);
            print.IsFamily.ShouldBeTrue();
            print.IsAbstract.ShouldBeTrue();
            print.ReturnType.ShouldBe(typeof(void));
            print.GetParameters().Single().ParameterType.ShouldBe(typeof(string));
            print.GetParameters().Single().Name.ShouldBe("message");
            print.GetParameters().Single().IsOptional.ShouldBeFalse();
        }

        private static void AssertArgumentError(Exception exception, Type expected, string parameterName)
        {
            exception.ShouldNotBeNull();
            exception.GetType().ShouldBe(expected);
            ((ArgumentException)exception).ParamName.ShouldBe(parameterName);
        }

        private static Exception CreateNestedException()
        {
            try
            {
                try { throw new InvalidOperationException("Synthetic Inner DETAIL"); }
                catch (Exception inner) { throw new Exception("Synthetic Outer DETAIL", inner); }
            }
            catch (Exception exception) { return exception; }
        }

        private sealed class CoreProbe : LoggingDestinationCore
        {
            public CoreProbe(LogLevels mask) : base(mask) { }
            public CoreProbe(int mask) : base(mask) { }
            public CoreProbe(string mask) : base(mask) { }
            public string Massage(LogLevels level, string message, Exception exception) => MassageLogStatement(level, message, exception);
        }

        private sealed class EventProbe : EventDestination
        {
            public EventProbe(LogLevels mask) : base(mask) { }
            public int MassageCalls { get; private set; }
            public LogLevels MassagedLevel { get; private set; }
            public string MassagedMessage { get; private set; }
            public Exception MassagedException { get; private set; }
            protected override string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
            {
                MassageCalls++;
                MassagedLevel = level;
                MassagedMessage = message;
                MassagedException = ex;
                return "massaged-entry";
            }
        }

        private sealed class GenericEventProbe : GenericEventDestination<object>
        {
            public GenericEventProbe(LogLevels mask) : base(mask) { }
            public int MassageCalls { get; private set; }
            public LogLevels MassagedLevel { get; private set; }
            public string MassagedMessage { get; private set; }
            public Exception MassagedException { get; private set; }
            protected override string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
            {
                MassageCalls++;
                MassagedLevel = level;
                MassagedMessage = message;
                MassagedException = ex;
                return "massaged-entry";
            }
        }

        private sealed class TextProbe : TextBasedDestination
        {
            public TextProbe(LogLevels mask) : base(mask) { }
            public TextProbe(int mask) : base(mask) { }
            public TextProbe(string mask) : base(mask) { }
            public int MassageCalls { get; private set; }
            public LogLevels MassagedLevel { get; private set; }
            public string MassagedMessage { get; private set; }
            public Exception MassagedException { get; private set; }
            public int PrintCalls { get; private set; }
            public string Printed { get; private set; }
            protected override string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
            {
                MassageCalls++;
                MassagedLevel = level;
                MassagedMessage = message;
                MassagedException = ex;
                return "massaged-entry";
            }
            protected override void PrintLogEntry(string message)
            {
                PrintCalls++;
                Printed = message;
            }
        }

        private sealed class CapturingTextDestination : TextBasedDestination
        {
            public CapturingTextDestination() : base((LogLevels)63) { }
            public int PrintCalls { get; private set; }
            public string Printed { get; private set; }
            protected override void PrintLogEntry(string message) { PrintCalls++; Printed = message; }
        }
    }
}