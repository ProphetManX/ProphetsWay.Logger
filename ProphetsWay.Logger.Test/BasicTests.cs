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
        public void ShouldExposeM3NonvirtualGuardsAndProtectedContextHooks()
        {
            foreach (var baseType in new[] { typeof(BaseLoggingDestination), typeof(Utilities.Generics.BaseLoggingDestination<object>) })
            {
                var typed = baseType.IsGenericType;
                var capability = typed ? typeof(Utilities.Generics.IContextLoggingDestination<object>) : typeof(IContextLoggingDestination);
                capability.IsAssignableFrom(baseType).ShouldBeTrue();
                var raw = baseType.GetMethod("Log");
                raw.IsVirtual.ShouldBeFalse();
                var contextual = baseType.GetMethod("LogWithContext");
                contextual.ShouldNotBeNull();
                contextual.IsVirtual.ShouldBeFalse();
                var hook = baseType.GetMethod("LogCore", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                hook.ShouldNotBeNull();
                hook.IsFamily.ShouldBeTrue();
                hook.IsAbstract.ShouldBeTrue();
                hook.ReturnType.ShouldBe(typeof(void));
                hook.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe(typed
                    ? new[] { typeof(LogContext), typeof(LogLevels), typeof(object), typeof(string), typeof(Exception) }
                    : new[] { typeof(LogContext), typeof(LogLevels), typeof(string), typeof(Exception) });
            }
        }

        [Theory]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, true)]
        public void ShouldRequireBothM3RegistrationAndIntrinsicLabelPermission(bool registrationAllows, bool intrinsicAllows, bool expected)
        {
            var ordinary = new EventProbe(LogLevels.Trace);
            var typed = new GenericEventProbe(LogLevels.Trace);
            var sentinel = new EventDestination((LogLevels)0);
            var typedSentinel = new GenericEventDestination<object>((LogLevels)0);
            var configured = registrationAllows ? new[] { new SensitivityLabel("inherited") } : new SensitivityLabel[0];
            var settings = new DestinationRegistrationSettings(true, LogLevels.Trace, new DestinationLabelPolicy(LabelFilterMode.AllowOnly, configured));
            var intrinsic = new DestinationLabelPolicy(intrinsicAllows ? LabelFilterMode.NoFilter : LabelFilterMode.Exclude,
                new[] { new SensitivityLabel("inherited") });
            M3SetPolicy(ordinary, intrinsic);
            M3SetPolicy(typed, intrinsic);
            Utilities.Logger.AddDestination(sentinel);
            Utilities.Logger.AddDestination<object>(typedSentinel);
            try
            {
                var ordinaryAdd = typeof(Utilities.Logger).GetMethod("AddDestination", new[] { typeof(ILoggingDestination), typeof(DestinationRegistrationSettings) });
                M3InvokeMember(ordinaryAdd, null, ordinary, settings);
                var typedAdd = typeof(Utilities.Logger).GetMethods().Where(method => method.Name == "AddDestination" && method.IsGenericMethodDefinition)
                    .Select(method => method.MakeGenericMethod(typeof(object)))
                    .SingleOrDefault(method => method.GetParameters().Select(parameter => parameter.ParameterType)
                        .SequenceEqual(new[] { typeof(Utilities.Generics.ILoggingDestination<object>), typeof(DestinationRegistrationSettings) }));
                M3InvokeMember(typedAdd, null, typed, settings);
                using (M3Scope("inherited"))
                {
                    Utilities.Logger.Info("inherited only");
                    Utilities.Logger.Info<object>("inherited only", null);
                }
                ordinary.MassageCalls.ShouldBe(expected ? 1 : 0);
                typed.MassageCalls.ShouldBe(expected ? 1 : 0);
                Utilities.Logger.Info("unlabeled denied");
                Utilities.Logger.Info<object>("unlabeled denied", null);
                ordinary.MassageCalls.ShouldBe(expected ? 1 : 0);
                typed.MassageCalls.ShouldBe(expected ? 1 : 0);
            }
            finally
            {
                Utilities.Logger.RemoveDestination(ordinary);
                Utilities.Logger.RemoveDestination<object>(typed);
                Utilities.Logger.RemoveDestination(sentinel);
                Utilities.Logger.RemoveDestination<object>(typedSentinel);
            }
        }

        [Fact]
        public void ShouldPublishM3IntrinsicPolicyWithoutChangingSeverityOrAcceptingNull()
        {
            var destinations = new LoggingDestinationCore[]
            {
                new CoreProbe(LogLevels.Trace), new CoreProbe(63), new CoreProbe("Trace"),
                new EventDestination(LogLevels.Trace), new EventDestination(63), new EventDestination("Trace"),
                new GenericEventDestination<object>(LogLevels.Trace), new GenericEventDestination<object>(63),
                new GenericEventDestination<object>("Trace"), new TextProbe(LogLevels.Trace), new TextProbe(63), new TextProbe("Trace")
            };
            var property = M3PolicyProperty();
            property.GetGetMethod().IsVirtual.ShouldBeFalse();
            property.GetSetMethod().IsVirtual.ShouldBeFalse();
            foreach (var destination in destinations)
            {
                var initial = (DestinationLabelPolicy)property.GetValue(destination);
                initial.Mode.ShouldBe(LabelFilterMode.NoFilter);
                initial.Labels.ShouldBeEmpty();
                var policy = new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { new SensitivityLabel("private-policy") });
                M3SetPolicy(destination, policy);
                Should.Throw<ArgumentNullException>(() => M3SetPolicy(destination, null)).ParamName.ShouldBe("value");
                var retained = (DestinationLabelPolicy)property.GetValue(destination);
                retained.Mode.ShouldBe(LabelFilterMode.Exclude);
                retained.Labels.Select(label => label.Identifier).ShouldBe(new[] { "private-policy" });
                destination.ValidateMessageLevel(LogLevels.Trace).ShouldBeTrue();
            }
        }

        [Fact]
        public void ShouldDenyM3DirectPayloadsBeforeMassageEventsAndTextOutput()
        {
            var ordinary = new EventProbe(LogLevels.Trace);
            var typed = new GenericEventProbe(LogLevels.Trace);
            var text = new TextProbe(LogLevels.Trace);
            var ordinaryCalls = 0;
            var typedCalls = 0;
            ordinary.LoggingEvent += (sender, args) => ordinaryCalls++;
            typed.LoggingEvent += (sender, args) => typedCalls++;
            var policy = new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { new SensitivityLabel("private") });
            foreach (var destination in new LoggingDestinationCore[] { ordinary, typed, text }) M3SetPolicy(destination, policy);
            using (M3Scope("private"))
            {
                ordinary.Log((LogLevels)9, "denied raw", new Exception("denied cause"));
                typed.Log((LogLevels)9, new object(), "denied raw", new Exception("denied cause"));
                text.Log((LogLevels)9, "denied raw", new Exception("denied cause"));
                ordinary.MassageCalls.ShouldBe(0);
                typed.MassageCalls.ShouldBe(0);
                text.MassageCalls.ShouldBe(0);
                text.PrintCalls.ShouldBe(0);
                ordinaryCalls.ShouldBe(0);
                typedCalls.ShouldBe(0);
            }
            ordinary.Log(LogLevels.InformationOnly, "allowed outside");
            typed.Log(LogLevels.InformationOnly, null, "allowed outside");
            text.Log(LogLevels.InformationOnly, "allowed outside");
            ordinary.MassageCalls.ShouldBe(1);
            typed.MassageCalls.ShouldBe(1);
            text.MassageCalls.ShouldBe(1);
            text.PrintCalls.ShouldBe(1);
            ordinaryCalls.ShouldBe(1);
            typedCalls.ShouldBe(1);
            text.Printed.ShouldContain("massaged-entry");
        }

        [Fact]
        public void ShouldApplyM3CurrentContextMasksAndCompleteTypedPolicyBeforePayloadWork()
        {
            var capture = new EventDestination(LogLevels.Trace);
            EventDestination.LoggerEventArgs captured = null;
            capture.LoggingEvent += (sender, args) => captured = args;
            var ordinary = new EventProbe(LogLevels.InformationOnly);
            var typed = new GenericEventProbe(LogLevels.InformationOnly);
            var text = new TextProbe(LogLevels.InformationOnly);
            var ordinaryEvents = new System.Collections.Generic.List<EventDestination.LoggerEventArgs>();
            var typedEvents = new System.Collections.Generic.List<GenericEventDestination<object>.LoggerEventArgs>();
            ordinary.LoggingEvent += (sender, args) => ordinaryEvents.Add(args);
            typed.LoggingEvent += (sender, args) => typedEvents.Add(args);
            var metadata = new object();
            var reports = 0;
            Action<LogFailureReport> observer = report => reports++;
            Utilities.Logger.AddDestination(capture);
            Utilities.Logger.DispatchFailed += observer;
            try
            {
                using (M3Scope("inherited"))
                {
                    var annotated = typeof(Utilities.Logger).GetMethod("LogAnnotated",
                        new[] { typeof(LogAnnotations), typeof(LogLevels), typeof(string), typeof(Exception) });
                    M3InvokeMember(annotated, null, new LogAnnotations(new[] { new SensitivityLabel("entry") }),
                        LogLevels.InformationOnly, "capture", null);
                    var context = M3ReadContext(captured);
                    M3Direct(ordinary, context, (LogLevels)9, "mask denied");
                    M3TypedDirect(typed, context, (LogLevels)9, metadata, "mask denied");
                    M3Direct(text, context, (LogLevels)9, "mask denied");
                    ordinary.MassageCalls.ShouldBe(0);
                    typed.MassageCalls.ShouldBe(0);
                    text.MassageCalls.ShouldBe(0);
                    text.PrintCalls.ShouldBe(0);
                    text.Printed.ShouldBeNull();
                    ordinaryEvents.ShouldBeEmpty();
                    typedEvents.ShouldBeEmpty();
                    foreach (var incompleteMembership in new[] { "entry", "inherited" })
                    {
                        M3SetPolicy(typed, new DestinationLabelPolicy(LabelFilterMode.AllowOnly,
                            new[] { new SensitivityLabel(incompleteMembership) }));
                        M3TypedDirect(typed, context, LogLevels.InformationOnly, metadata, "incomplete policy denied");
                        typed.MassageCalls.ShouldBe(0);
                        typedEvents.ShouldBeEmpty();
                    }
                    var completePolicy = new DestinationLabelPolicy(LabelFilterMode.AllowOnly,
                        new[] { new SensitivityLabel("entry"), new SensitivityLabel("inherited") });
                    M3SetPolicy(typed, completePolicy);
                    M3Direct(ordinary, context, LogLevels.InformationOnly, "ordinary permitted");
                    M3TypedDirect(typed, context, LogLevels.InformationOnly, metadata, "typed permitted");
                    M3Direct(text, context, LogLevels.InformationOnly, "text permitted");
                    ordinary.MassageCalls.ShouldBe(1);
                    typed.MassageCalls.ShouldBe(1);
                    text.MassageCalls.ShouldBe(1);
                    text.PrintCalls.ShouldBe(1);
                    ordinaryEvents.ShouldHaveSingleItem().RawMessage.ShouldBe("ordinary permitted");
                    var delivered = typedEvents.ShouldHaveSingleItem();
                    delivered.Metadata.ShouldBeSameAs(metadata);
                    delivered.RawMessage.ShouldBe("typed permitted");
                    delivered.LogLevel.ShouldBe(LogLevels.InformationOnly);
                    M3ReadContext(delivered).Labels.EffectiveLabels.Select(label => label.Identifier).OrderBy(identifier => identifier)
                        .ShouldBe(new[] { "entry", "inherited" });
                    text.MassagedMessage.ShouldBe("text permitted");
                    text.MassagedLevel.ShouldBe(LogLevels.InformationOnly);
                    text.Printed.ShouldContain("massaged-entry");
                    M3SetPolicy(typed, new DestinationLabelPolicy(LabelFilterMode.Exclude,
                        new[] { new SensitivityLabel("inherited") }));
                    M3TypedDirect(typed, context, LogLevels.InformationOnly, metadata, "retained context now denied");
                    typed.MassageCalls.ShouldBe(1);
                    typedEvents.ShouldHaveSingleItem().RawMessage.ShouldBe("typed permitted");
                    M3SetPolicy(typed, completePolicy);
                    M3TypedDirect(typed, context, LogLevels.InformationOnly, metadata, "typed restored");
                    typed.MassageCalls.ShouldBe(2);
                    typedEvents.Select(arguments => arguments.RawMessage).ShouldBe(new[] { "typed permitted", "typed restored" });
                    typedEvents[1].Metadata.ShouldBeSameAs(metadata);
                    context.Labels.EntryAnnotations.LabelOccurrences.Select(label => label.Identifier).ShouldBe(new[] { "entry" });
                    context.Scopes.ShouldHaveSingleItem().Annotations.LabelOccurrences.Select(label => label.Identifier).ShouldBe(new[] { "inherited" });
                    reports.ShouldBe(0);
                }
            }
            finally
            {
                Utilities.Logger.RemoveDestination(capture);
                Utilities.Logger.DispatchFailed -= observer;
            }
        }

        [Fact]
        public void ShouldRequireM3CurrentScopeOpeningsAndRecheckDirectPolicy()
        {
            var destination = new EventProbe(LogLevels.Trace);
            EventDestination.LoggerEventArgs delivered = null;
            destination.LoggingEvent += (sender, args) => delivered = args;
            var noFilter = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);
            LogContext context;
            var reports = 0;
            Action<LogFailureReport> observer = report => reports++;
            Utilities.Logger.DispatchFailed += observer;
            try
            {
                using (M3Scope("same"))
                {
                    destination.Log(LogLevels.InformationOnly, "first");
                    context = M3ReadContext(delivered);
                    Should.Throw<ArgumentNullException>(() => M3Direct(destination, null, LogLevels.InformationOnly, "invalid"))
                        .ParamName.ShouldBe("context");
                    Should.Throw<ArgumentOutOfRangeException>(() => M3Direct(destination, context, (LogLevels)0, "invalid"))
                        .ParamName.ShouldBe("level");
                    M3Direct(destination, context, LogLevels.InformationOnly, "repeat");
                    destination.MassageCalls.ShouldBe(2);
                    M3SetPolicy(destination, new DestinationLabelPolicy(LabelFilterMode.AllowOnly, new SensitivityLabel[0]));
                    M3Direct(destination, context, LogLevels.InformationOnly, "now denied");
                    destination.MassageCalls.ShouldBe(2);
                    M3SetPolicy(destination, noFilter);
                    using (M3Scope(null))
                    {
                        var failure = Should.Throw<ArgumentException>(() => M3Direct(destination, context, LogLevels.InformationOnly, "noncurrent"));
                        failure.GetType().ShouldBe(typeof(ArgumentException));
                        failure.ParamName.ShouldBe("context");
                        destination.MassageCalls.ShouldBe(2);
                    }
                    M3Direct(destination, context, LogLevels.InformationOnly, "restored");
                    destination.MassageCalls.ShouldBe(3);
                }
                Should.Throw<ArgumentException>(() => M3Direct(destination, context, LogLevels.InformationOnly, "removed"))
                    .ParamName.ShouldBe("context");
                using (M3Scope("same"))
                    Should.Throw<ArgumentException>(() => M3Direct(destination, context, LogLevels.InformationOnly, "different opening"))
                        .ParamName.ShouldBe("context");
                context.Scopes.Count.ShouldBe(1);
                context.Labels.EffectiveLabels.Select(label => label.Identifier).ShouldBe(new[] { "same" });
                destination.MassageCalls.ShouldBe(3);
                reports.ShouldBe(0);
            }
            finally { Utilities.Logger.DispatchFailed -= observer; }
        }

        [Fact]
        public void ShouldValidateM3TypedDirectContextAndPreserveEmptyFrameCorrespondence()
        {
            var destination = new GenericEventProbe(LogLevels.Trace);
            GenericEventDestination<object>.LoggerEventArgs delivered = null;
            destination.LoggingEvent += (sender, args) => delivered = args;
            destination.Log(LogLevels.InformationOnly, null, null);
            var context = M3ReadContext(delivered);
            context.Scopes.ShouldBeEmpty();
            var metadata = new object();
            M3TypedDirect(destination, context, (LogLevels)9, metadata, "raw");
            delivered.Metadata.ShouldBeSameAs(metadata);
            delivered.RawMessage.ShouldBe("raw");
            delivered.LogLevel.ShouldBe((LogLevels)9);
            destination.MassageCalls.ShouldBe(2);
            Should.Throw<ArgumentNullException>(() => M3TypedDirect(destination, null, LogLevels.InformationOnly, metadata, "invalid"))
                .ParamName.ShouldBe("context");
            Should.Throw<ArgumentOutOfRangeException>(() => M3TypedDirect(destination, context, (LogLevels)64, metadata, "invalid"))
                .ParamName.ShouldBe("level");
            using (M3Scope(null))
                Should.Throw<ArgumentException>(() => M3TypedDirect(destination, context, LogLevels.InformationOnly, metadata, "added empty frame"))
                    .ParamName.ShouldBe("context");
            destination.MassageCalls.ShouldBe(2);
            M3TypedDirect(destination, context, LogLevels.InformationOnly, null, null);
            destination.MassageCalls.ShouldBe(3);
            delivered.Metadata.ShouldBeNull();
            delivered.RawMessage.ShouldBeNull();
        }

        [Fact]
        public void ShouldKeepM3CapturedIntrinsicPoliciesAndGuardPublicReentryAfresh()
        {
            var first = new EventDestination(LogLevels.Trace);
            var later = new EventProbe(LogLevels.Trace);
            EventDestination.LoggerEventArgs delivered = null;
            LogScopeHandle callbackScope = null;
            later.LoggingEvent += (sender, args) => delivered = args;
            M3SetPolicy(later, new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]));
            first.LoggingEvent += (sender, args) =>
            {
                if (args.RawMessage != "outer") return;
                M3SetPolicy(later, new DestinationLabelPolicy(LabelFilterMode.AllowOnly, new SensitivityLabel[0]));
                callbackScope = M3Scope("callback-only");
                later.Log(LogLevels.InformationOnly, "direct reentry denied");
            };
            Utilities.Logger.AddDestination(first);
            Utilities.Logger.AddDestination(later);
            try
            {
                Utilities.Logger.Info("outer");
                later.MassageCalls.ShouldBe(1);
                delivered.RawMessage.ShouldBe("outer");
                M3ReadContext(delivered).Scopes.ShouldBeEmpty();
                Utilities.Logger.Info("later denied");
                later.MassageCalls.ShouldBe(1);
                delivered.RawMessage.ShouldBe("outer");
            }
            finally
            {
                try { if (callbackScope != null) callbackScope.Dispose(); }
                finally
                {
                    Utilities.Logger.RemoveDestination(first);
                    Utilities.Logger.RemoveDestination(later);
                }
            }
        }

        [Theory]
        [InlineData("massage")]
        [InlineData("text")]
        [InlineData("event")]
        [InlineData("typed event")]
        public void ShouldReportM3DirectOutputOnceAndPreserveSafeMandatoryFailure(string boundary)
        {
            var reports = new System.Collections.Generic.List<LogFailureReport>();
            Action<LogFailureReport> observer = report => { reports.Add(report); throw new Exception("reporter-private"); };
            Action attempt;
            if (boundary == "event")
            {
                var destination = new EventDestination(LogLevels.Trace);
                destination.LoggingEvent += (sender, args) => { throw new Exception("output-private"); };
                attempt = () => destination.Log(LogLevels.InformationOnly, "message-private");
            }
            else if (boundary == "typed event")
            {
                var destination = new GenericEventDestination<object>(LogLevels.Trace);
                destination.LoggingEvent += (sender, args) => { throw new Exception("output-private"); };
                attempt = () => destination.Log(LogLevels.InformationOnly, new object(), "message-private");
            }
            else
            {
                var destination = new M3FailingText(boundary == "massage");
                attempt = () => destination.Log(LogLevels.InformationOnly, "message-private");
            }
            var oldError = Console.Error;
            using (var error = new System.IO.StringWriter())
            {
                Console.SetError(error);
                Utilities.Logger.DispatchFailed += observer;
                try
                {
                    var first = Should.Throw<LogDispatchException>(attempt);
                    var firstReport = reports.ShouldHaveSingleItem();
                    firstReport.CoreCaptureFailureCount.ShouldBe(0);
                    firstReport.OverflowCount.ShouldBe(0);
                    var descriptor = firstReport.Failures.ShouldHaveSingleItem();
                    descriptor.RegistrationId.ShouldBe(1);
                    descriptor.Stage.ShouldBe(LogFailureStage.Output);
                    first.Report.CorrelationId.ShouldBe(firstReport.CorrelationId);
                    first.InnerException.ShouldBeNull();
                    first.Data.Count.ShouldBe(0);
                    first.StackTrace.ShouldBeNull();
                    var safeText = error.ToString() + first.Message + first.ToString();
                    foreach (var canary in new[] { "message-private", "output-private", "reporter-private" }) safeText.ShouldNotContain(canary);
                    error.ToString().Length.ShouldBeInRange(1, 512);
                    var second = Should.Throw<LogDispatchException>(attempt);
                    reports.Count.ShouldBe(2);
                    second.Report.CorrelationId.ShouldNotBe(firstReport.CorrelationId);
                    second.Report.CoreCaptureFailureCount.ShouldBe(0);
                }
                finally
                {
                    Utilities.Logger.DispatchFailed -= observer;
                    Console.SetError(oldError);
                }
            }
        }

        private static PropertyInfo M3PolicyProperty()
        {
            var property = typeof(LoggingDestinationCore).GetProperty("LabelPolicy", BindingFlags.Public | BindingFlags.Instance);
            property.ShouldNotBeNull("M3 requires the reviewed intrinsic LabelPolicy property.");
            property.PropertyType.ShouldBe(typeof(DestinationLabelPolicy));
            return property;
        }

        private static void M3SetPolicy(LoggingDestinationCore destination, DestinationLabelPolicy policy)
        {
            M3InvokeMember(M3PolicyProperty().GetSetMethod(), destination, policy);
        }

        private static LogScopeHandle M3Scope(string label)
        {
            var method = typeof(Utilities.Logger).GetMethod("BeginScope", new[] { typeof(LogAnnotations) });
            return (LogScopeHandle)M3InvokeMember(method, null,
                new object[] { label == null ? null : new LogAnnotations(new[] { new SensitivityLabel(label) }) });
        }

        private static LogContext M3ReadContext(object arguments)
        {
            arguments.ShouldNotBeNull();
            var property = arguments.GetType().GetProperty("Context");
            property.ShouldNotBeNull("M3 requires the reviewed event Context getter.");
            property.PropertyType.ShouldBe(typeof(LogContext));
            var context = (LogContext)property.GetValue(arguments);
            context.ShouldNotBeNull("Library-delivered event context must be complete.");
            return context;
        }

        private static void M3Direct(BaseLoggingDestination destination, LogContext context, LogLevels level, string message)
        {
            var method = typeof(BaseLoggingDestination).GetMethod("LogWithContext", new[] { typeof(LogContext), typeof(LogLevels), typeof(string), typeof(Exception) });
            M3InvokeMember(method, destination, context, level, message, null);
        }

        private static void M3TypedDirect(GenericEventDestination<object> destination, LogContext context, LogLevels level, object metadata, string message)
        {
            var method = typeof(GenericEventDestination<object>).GetMethod("LogWithContext", new[] { typeof(LogContext), typeof(LogLevels), typeof(object), typeof(string), typeof(Exception) });
            M3InvokeMember(method, destination, context, level, metadata, message, null);
        }

        private static object M3InvokeMember(MethodInfo method, object receiver, params object[] arguments)
        {
            method.ShouldNotBeNull("M3 requires the exact reviewed public callable.");
            try { return method.Invoke(receiver, arguments); }
            catch (TargetInvocationException failure)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }

        private sealed class M3FailingText : TextBasedDestination
        {
            private readonly bool _failMassage;
            public M3FailingText(bool failMassage) : base(LogLevels.Trace) { _failMassage = failMassage; }
            protected override string MassageLogStatement(LogLevels level, string message = null, Exception ex = null)
            {
                if (_failMassage) throw new Exception("output-private");
                return message;
            }
            protected override void PrintLogEntry(string message) { throw new Exception("output-private"); }
        }

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