using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public void ShouldDeliverM3ExactTypeContextOrLegacyOnceWithoutClassifyingMetadata()
        {
            var receiver = new M3TypedReceiver<object>();
            var otherType = new M3TypedReceiver<LogAnnotations>();
            var legacy = new M3TypedLegacy<object>();
            Utilities.Logger.AddDestination<object>(receiver);
            Utilities.Logger.AddDestination<LogAnnotations>(otherType);
            Utilities.Logger.AddDestination<object>(legacy);
            Utilities.Logger.AddDestination((ILoggingDestination)receiver);
            try
            {
                var metadata = new LogAnnotations(new[] { new SensitivityLabel("metadata-only") });
                var exception = new Exception("typed original");
                M3TypedLog<object>(null, (LogLevels)9, metadata, null, exception);
                var entry = receiver.Entries.ShouldHaveSingleItem();
                entry.Metadata.ShouldBeSameAs(metadata);
                entry.Message.ShouldBeNull();
                entry.Exception.ShouldBeSameAs(exception);
                entry.Level.ShouldBe((LogLevels)9);
                entry.Context.Labels.EffectiveLabels.ShouldBeEmpty();
                entry.Context.Labels.EntryAnnotations.ShouldBeNull();
                entry.Context.Scopes.ShouldBeEmpty();
                receiver.LegacyCalls.ShouldBe(0);
                receiver.OrdinaryCalls.ShouldBe(0);
                otherType.Entries.ShouldBeEmpty();
                otherType.LegacyCalls.ShouldBe(0);
                legacy.Metadata.ShouldBeSameAs(metadata);
                legacy.Exception.ShouldBeSameAs(exception);
                legacy.Message.ShouldBeNull();
                legacy.Level.ShouldBe((LogLevels)9);
                legacy.Calls.ShouldBe(1);
                Utilities.Logger.Info("ordinary remains separate");
                receiver.OrdinaryCalls.ShouldBe(1);
                receiver.Entries.ShouldHaveSingleItem();
            }
            finally
            {
                Utilities.Logger.RemoveDestination<object>(receiver);
                Utilities.Logger.RemoveDestination<LogAnnotations>(otherType);
                Utilities.Logger.RemoveDestination<object>(legacy);
                Utilities.Logger.RemoveDestination((ILoggingDestination)receiver);
            }
        }

        [Theory]
        [InlineData("disabled")]
        [InlineData("mask")]
        [InlineData("labels")]
        [InlineData("severity")]
        public void ShouldWithholdM3TypedAnnotatedPayloadUnlessEveryRecipientGatePermits(string rejection)
        {
            var contextual = new M3TypedReceiver<object> { Permitted = rejection != "severity" };
            var legacy = new M3TypedLegacy<object> { Permitted = rejection != "severity" };
            var permitted = new M3TypedReceiver<object>();
            var settings = new DestinationRegistrationSettings(rejection != "disabled",
                rejection == "mask" ? (LogLevels)8 : LogLevels.Trace,
                new DestinationLabelPolicy(rejection == "labels" ? LabelFilterMode.AllowOnly : LabelFilterMode.NoFilter,
                    new[] { new SensitivityLabel("entry") }));
            var metadata = new M3OpaqueMetadata();
            var exception = new Exception("annotated original");
            var reports = 0;
            Action<LogFailureReport> observer = report => reports++;
            Utilities.Logger.DispatchFailed += observer;
            try
            {
                M3TypedSettings("AddDestination", contextual, settings);
                M3TypedSettings("AddDestination", legacy, settings);
                Utilities.Logger.AddDestination<object>(permitted);
                using (M3TypedScope("inherited"))
                    M3TypedLog<object>(new LogAnnotations(new[] { new SensitivityLabel("entry") }),
                        (LogLevels)9, metadata, "annotated payload", exception);
                contextual.Entries.ShouldBeEmpty();
                contextual.LegacyCalls.ShouldBe(0);
                contextual.OrdinaryCalls.ShouldBe(0);
                legacy.Calls.ShouldBe(0);
                contextual.ValidationCalls.ShouldBeInRange(0, 1);
                legacy.ValidationCalls.ShouldBeInRange(0, 1);
                if (rejection == "disabled")
                {
                    contextual.ValidationCalls.ShouldBe(0);
                    legacy.ValidationCalls.ShouldBe(0);
                }
                if (rejection == "severity")
                {
                    contextual.ValidationCalls.ShouldBe(1);
                    legacy.ValidationCalls.ShouldBe(1);
                }
                if (contextual.ValidationCalls != 0) contextual.LastValidationLevel.ShouldBe((LogLevels)9);
                if (legacy.ValidationCalls != 0) legacy.LastValidationLevel.ShouldBe((LogLevels)9);
                var delivered = permitted.Entries.ShouldHaveSingleItem();
                delivered.Metadata.ShouldBeSameAs(metadata);
                delivered.Exception.ShouldBeSameAs(exception);
                delivered.Message.ShouldBe("annotated payload");
                delivered.Level.ShouldBe((LogLevels)9);
                delivered.Context.Labels.EffectiveLabels.Select(label => label.Identifier).OrderBy(identifier => identifier)
                    .ShouldBe(new[] { "entry", "inherited" });
                delivered.Context.Scopes.ShouldHaveSingleItem().Annotations.LabelOccurrences
                    .Select(label => label.Identifier).ShouldBe(new[] { "inherited" });
                delivered.Context.Labels.EntryAnnotations.LabelOccurrences.Select(label => label.Identifier).ShouldBe(new[] { "entry" });
                permitted.ValidationCalls.ShouldBe(1);
                permitted.LastValidationLevel.ShouldBe((LogLevels)9);
                permitted.LegacyCalls.ShouldBe(0);
                permitted.OrdinaryCalls.ShouldBe(0);
                metadata.InspectionCalls.ShouldBe(0);
                reports.ShouldBe(0);
            }
            finally
            {
                Utilities.Logger.RemoveDestination<object>(contextual);
                Utilities.Logger.RemoveDestination<object>(legacy);
                Utilities.Logger.RemoveDestination<object>(permitted);
                Utilities.Logger.DispatchFailed -= observer;
            }
        }

        [Fact]
        public void ShouldPreserveM3NullAndDefaultTypedRawValues()
        {
            var reference = new M3TypedReceiver<object>();
            var value = new M3TypedReceiver<int>();
            Utilities.Logger.AddDestination<object>(reference);
            Utilities.Logger.AddDestination<int>(value);
            try
            {
                M3TypedLog<object>(null, LogLevels.TraceOnly, null, "", null);
                M3TypedLog<int>(null, LogLevels.InformationOnly, 0, " \t ", null);
                reference.Entries.ShouldHaveSingleItem().Metadata.ShouldBeNull();
                reference.Entries[0].Message.ShouldBe("");
                reference.Entries[0].Exception.ShouldBeNull();
                value.Entries.ShouldHaveSingleItem().Metadata.ShouldBe(0);
                value.Entries[0].Message.ShouldBe(" \t ");
                value.Entries[0].Exception.ShouldBeNull();
                value.Entries[0].Context.Labels.EffectiveLabels.ShouldBeEmpty();
                reference.LegacyCalls.ShouldBe(0);
                value.LegacyCalls.ShouldBe(0);
                var opaque = new M3OpaqueMetadata();
                M3TypedLog<object>(null, LogLevels.InformationOnly, opaque, "opaque", null);
                reference.Entries.Count.ShouldBe(2);
                reference.Entries[1].Metadata.ShouldBeSameAs(opaque);
                opaque.InspectionCalls.ShouldBe(0);
            }
            finally
            {
                Utilities.Logger.RemoveDestination<object>(reference);
                Utilities.Logger.RemoveDestination<int>(value);
            }
        }

        [Fact]
        public void ShouldKeepM3SettingsIndependentForARecipientOnTwoRoutes()
        {
            var receiver = new M3TypedReceiver<object>();
            var sentinel = new M3TypedReceiver<object> { Permitted = false };
            var policy = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);
            Utilities.Logger.AddDestination((ILoggingDestination)receiver);
            Utilities.Logger.AddDestination<object>(receiver);
            Utilities.Logger.AddDestination<object>(sentinel);
            try
            {
                M3TypedSettings("SetDestinationSettings", receiver, new DestinationRegistrationSettings(false, (LogLevels)0, policy));
                receiver.ValidationCalls.ShouldBe(0);
                Utilities.Logger.Info("ordinary");
                Utilities.Logger.Info<object>("disabled typed", null);
                receiver.OrdinaryCalls.ShouldBe(1);
                receiver.Entries.ShouldBeEmpty();
                receiver.LegacyCalls.ShouldBe(0);
                M3TypedSettings("SetDestinationSettings", receiver, new DestinationRegistrationSettings(true, LogLevels.Trace, policy));
                Utilities.Logger.Info<object>("enabled typed", null);
                receiver.Entries.ShouldHaveSingleItem().Message.ShouldBe("enabled typed");
                receiver.OrdinaryCalls.ShouldBe(1);
                foreach (var restriction in new[] { "mask", "policy" })
                {
                    var ordinaryBefore = receiver.OrdinaryCalls;
                    var typedBefore = receiver.Entries.Count;
                    M3TypedSettings("SetDestinationSettings", receiver, new DestinationRegistrationSettings(true,
                        restriction == "mask" ? LogLevels.Critical : LogLevels.Trace,
                        restriction == "policy" ? new DestinationLabelPolicy(LabelFilterMode.AllowOnly, new SensitivityLabel[0]) : policy));
                    Utilities.Logger.Info<object>(restriction + " denied typed", null);
                    Utilities.Logger.Info(restriction + " ordinary unaffected");
                    receiver.Entries.Count.ShouldBe(typedBefore);
                    receiver.OrdinaryCalls.ShouldBe(ordinaryBefore + 1);
                    M3TypedSettings("SetDestinationSettings", receiver, new DestinationRegistrationSettings(true, LogLevels.Trace, policy));
                    Utilities.Logger.Info<object>(restriction + " typed restored", null);
                    receiver.Entries.Count.ShouldBe(typedBefore + 1);
                    receiver.Entries.Last().Message.ShouldBe(restriction + " typed restored");
                    receiver.OrdinaryCalls.ShouldBe(ordinaryBefore + 1);
                }
                receiver.LegacyCalls.ShouldBe(0);
            }
            finally
            {
                Utilities.Logger.RemoveDestination((ILoggingDestination)receiver);
                Utilities.Logger.RemoveDestination<object>(receiver);
                Utilities.Logger.RemoveDestination<object>(sentinel);
            }
        }

        [Fact]
        public void ShouldRetainM3TypedCapturedSettingsThroughReentrantReplacement()
        {
            var first = new M3TypedReceiver<object>();
            var second = new M3TypedReceiver<object>();
            var third = new M3TypedReceiver<object>();
            var order = new List<string>();
            var policy = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);
            var enabled = new DestinationRegistrationSettings(true, LogLevels.Trace, policy);
            first.OnContext = message =>
            {
                order.Add("first:" + message);
                if (message == "outer")
                {
                    M3TypedSettings("SetDestinationSettings", second, new DestinationRegistrationSettings(false, (LogLevels)0, policy));
                    Utilities.Logger.Info<object>("nested", null);
                }
            };
            second.OnContext = message => order.Add("second:" + message);
            third.OnContext = message => order.Add("third:" + message);
            Utilities.Logger.AddDestination<object>(first);
            try
            {
                M3TypedSettings("AddDestination", second, enabled);
                Utilities.Logger.AddDestination<object>(third);
                Utilities.Logger.Info<object>("outer", null);
                order.ShouldBe(new[] { "first:outer", "first:nested", "third:nested", "second:outer", "third:outer" });
                M3TypedSettings("SetDestinationSettings", second, enabled);
                Utilities.Logger.Info<object>("later", null);
                order.Skip(5).ShouldBe(new[] { "first:later", "second:later", "third:later" });
                first.LegacyCalls.ShouldBe(0);
                second.LegacyCalls.ShouldBe(0);
                foreach (var restriction in new[] { "mask", "policy" })
                {
                    var annotation = new LogAnnotations(new[] { new SensitivityLabel("restricted") });
                    var replacement = new DestinationRegistrationSettings(true,
                        restriction == "mask" ? (LogLevels)8 : LogLevels.Trace,
                        restriction == "policy" ? new DestinationLabelPolicy(LabelFilterMode.Exclude,
                            new[] { new SensitivityLabel("restricted") }) : policy);
                    var outerMessage = restriction + " outer";
                    var nestedMessage = restriction + " nested";
                    var subsequentMessage = restriction + " subsequent";
                    var restoredMessage = restriction + " restored";
                    order.Clear();
                    first.OnContext = message =>
                    {
                        order.Add("first:" + message);
                        if (message == outerMessage)
                        {
                            M3TypedSettings("SetDestinationSettings", second, replacement);
                            M3TypedLog<object>(annotation, (LogLevels)9, null, nestedMessage, null);
                        }
                    };
                    M3TypedLog<object>(annotation, (LogLevels)9, null, outerMessage, null);
                    M3TypedLog<object>(annotation, (LogLevels)9, null, subsequentMessage, null);
                    order.ShouldBe(new[] { "first:" + outerMessage, "first:" + nestedMessage,
                        "third:" + nestedMessage, "second:" + outerMessage, "third:" + outerMessage,
                        "first:" + subsequentMessage, "third:" + subsequentMessage });
                    var captured = second.Entries.Single(entry => entry.Message == outerMessage);
                    captured.Level.ShouldBe((LogLevels)9);
                    captured.Context.Labels.EffectiveLabels.Select(label => label.Identifier).ShouldBe(new[] { "restricted" });
                    M3TypedSettings("SetDestinationSettings", second, enabled);
                    M3TypedLog<object>(annotation, (LogLevels)9, null, restoredMessage, null);
                    order.Skip(7).ShouldBe(new[] { "first:" + restoredMessage, "second:" + restoredMessage, "third:" + restoredMessage });
                    second.Entries.Single(entry => entry.Message == restoredMessage).Level.ShouldBe((LogLevels)9);
                }
                first.LegacyCalls.ShouldBe(0);
                second.LegacyCalls.ShouldBe(0);
                third.LegacyCalls.ShouldBe(0);
            }
            finally
            {
                Utilities.Logger.RemoveDestination<object>(first);
                Utilities.Logger.RemoveDestination<object>(second);
                Utilities.Logger.RemoveDestination<object>(third);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(64)]
        [InlineData(65)]
        public void ShouldRejectM3TypedRawMasksBeforeEligibility(int mask)
        {
            var receiver = new M3TypedReceiver<object>();
            Utilities.Logger.AddDestination<object>(receiver);
            try
            {
                Should.Throw<ArgumentOutOfRangeException>(() => M3TypedLog<object>(null, (LogLevels)mask, null, null, null))
                    .ParamName.ShouldBe("level");
                receiver.ValidationCalls.ShouldBe(0);
                receiver.LegacyCalls.ShouldBe(0);
                receiver.Entries.ShouldBeEmpty();
            }
            finally { Utilities.Logger.RemoveDestination<object>(receiver); }
        }

        private static object M3TypedInvoke<T>(string name, Type[] parameters, params object[] arguments)
        {
            var method = typeof(Utilities.Logger).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(candidate => candidate.Name == name && candidate.IsGenericMethodDefinition && candidate.GetGenericArguments().Length == 1)
                .Select(candidate => candidate.MakeGenericMethod(typeof(T)))
                .SingleOrDefault(candidate => candidate.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameters));
            method.ShouldNotBeNull("M3 requires the reviewed public exact-T Logger." + name + " signature.");
            try { return method.Invoke(null, arguments); }
            catch (System.Reflection.TargetInvocationException failure)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }

        private static void M3TypedLog<T>(LogAnnotations annotations, LogLevels level, T metadata, string message, Exception exception)
        {
            M3TypedInvoke<T>("LogAnnotated", new[] { typeof(LogAnnotations), typeof(LogLevels), typeof(T), typeof(string), typeof(Exception) },
                annotations, level, metadata, message, exception);
        }

        private static void M3TypedSettings<T>(string name, Utilities.Generics.ILoggingDestination<T> receiver, DestinationRegistrationSettings settings)
        {
            M3TypedInvoke<T>(name, new[] { typeof(Utilities.Generics.ILoggingDestination<T>), typeof(DestinationRegistrationSettings) }, receiver, settings);
        }

        private static LogScopeHandle M3TypedScope(string label)
        {
            var method = typeof(Utilities.Logger).GetMethod("BeginScope", new[] { typeof(LogAnnotations) });
            method.ShouldNotBeNull("M3 requires the reviewed public Logger.BeginScope signature.");
            try { return (LogScopeHandle)method.Invoke(null, new object[] { new LogAnnotations(new[] { new SensitivityLabel(label) }) }); }
            catch (System.Reflection.TargetInvocationException failure)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
                throw;
            }
        }

        private sealed class M3OpaqueMetadata : System.Collections.IEnumerable
        {
            public int InspectionCalls;
            public object Dangerous { get { InspectionCalls++; throw new Exception("metadata getter must not run"); } }
            public override string ToString() { InspectionCalls++; throw new Exception("metadata ToString must not run"); }
            public System.Collections.IEnumerator GetEnumerator() { InspectionCalls++; throw new Exception("metadata enumeration must not run"); }
        }

        private sealed class M3TypedEntry<T>
        {
            public LogContext Context;
            public T Metadata;
            public LogLevels Level;
            public string Message;
            public Exception Exception;
        }

        private sealed class M3TypedReceiver<T> : Utilities.Generics.IContextLoggingDestination<T>, IContextLoggingDestination
        {
            public readonly List<M3TypedEntry<T>> Entries = new List<M3TypedEntry<T>>();
            public int LegacyCalls;
            public int OrdinaryCalls;
            public int ValidationCalls;
            public LogLevels LastValidationLevel;
            public bool Permitted = true;
            public Action<string> OnContext;
            public bool ValidateMessageLevel(LogLevels level) { ValidationCalls++; LastValidationLevel = level; return Permitted; }
            public void Log(LogLevels level, T metadata, string message = null, Exception ex = null) { LegacyCalls++; }
            public void Log(LogLevels level, string message = null, Exception ex = null) { LegacyCalls++; }
            public void LogWithContext(LogContext context, LogLevels level, T metadata, string message = null, Exception ex = null)
            {
                Entries.Add(new M3TypedEntry<T> { Context = context, Metadata = metadata, Level = level, Message = message, Exception = ex });
                if (OnContext != null) OnContext(message);
            }
            public void LogWithContext(LogContext context, LogLevels level, string message = null, Exception ex = null) { OrdinaryCalls++; }
        }

        private sealed class M3TypedLegacy<T> : Utilities.Generics.ILoggingDestination<T>
        {
            public int Calls;
            public int ValidationCalls;
            public LogLevels LastValidationLevel;
            public bool Permitted = true;
            public T Metadata;
            public string Message;
            public LogLevels Level;
            public Exception Exception;
            public bool ValidateMessageLevel(LogLevels level) { ValidationCalls++; LastValidationLevel = level; return Permitted; }
            public void Log(LogLevels level, T metadata, string message = null, Exception ex = null)
            {
                Calls++;
                Metadata = metadata;
                Message = message;
                Level = level;
                Exception = ex;
            }
        }

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

        [Fact]
        public void ShouldKeepDestinationContractRouteIndependentAndBorrowCrossRouteRecipients()
        {
            var shared = new RouteRecipient<ILoggingDestination>();
            var ordinarySentinel = new RouteRecipient<ILoggingDestination>();
            var typedSentinel = new RouteRecipient<ILoggingDestination>();
            try
            {
                Utilities.Logger.ClearDestinations();
                Utilities.Logger.ClearDestinations<ILoggingDestination>();
                Utilities.Logger.AddDestination((ILoggingDestination)shared);
                Utilities.Logger.AddDestination<ILoggingDestination>(shared);
                shared.ValidationCalls.ShouldBe(0);
                Utilities.Logger.Debug("ordinary");
                Utilities.Logger.Debug<ILoggingDestination>("typed", shared);
                shared.Ordinary.ShouldBe(new[] { "ordinary" });
                shared.Typed.ShouldBe(new[] { "typed" });
                shared.Metadata.ShouldBeSameAs(shared);
                Utilities.Logger.RemoveDestination((ILoggingDestination)shared);
                Utilities.Logger.AddDestination((ILoggingDestination)ordinarySentinel);
                Utilities.Logger.Debug("removed ordinary");
                Utilities.Logger.Debug<ILoggingDestination>("retained typed", shared);
                shared.Ordinary.ShouldBe(new[] { "ordinary" });
                shared.Typed.ShouldBe(new[] { "typed", "retained typed" });
                Utilities.Logger.AddDestination((ILoggingDestination)shared);
                Utilities.Logger.ClearDestinations<ILoggingDestination>();
                Utilities.Logger.AddDestination<ILoggingDestination>(typedSentinel);
                Utilities.Logger.Debug("ordinary survives typed clear");
                Utilities.Logger.Debug<ILoggingDestination>("cleared typed", shared);
                shared.Ordinary.ShouldBe(new[] { "ordinary", "ordinary survives typed clear" });
                shared.Typed.ShouldBe(new[] { "typed", "retained typed" });
                Utilities.Logger.AddDestination<ILoggingDestination>(shared);
                Utilities.Logger.ClearDestinations();
                Utilities.Logger.AddDestination((ILoggingDestination)ordinarySentinel);
                Utilities.Logger.Debug("cleared ordinary");
                Utilities.Logger.Debug<ILoggingDestination>("typed survives ordinary clear", shared);
                shared.Ordinary.ShouldBe(new[] { "ordinary", "ordinary survives typed clear" });
                shared.Typed.ShouldBe(new[] { "typed", "retained typed", "typed survives ordinary clear" });
                Utilities.Logger.AddDestination((ILoggingDestination)shared);
                Utilities.Logger.RemoveDestination<ILoggingDestination>(shared);
                Utilities.Logger.Debug("ordinary survives typed remove");
                Utilities.Logger.Debug<ILoggingDestination>("removed typed", shared);
                shared.Ordinary.ShouldBe(new[] { "ordinary", "ordinary survives typed clear", "ordinary survives typed remove" });
                shared.Typed.ShouldBe(new[] { "typed", "retained typed", "typed survives ordinary clear" });
                ordinarySentinel.Ordinary.ShouldBe(new[] { "removed ordinary", "ordinary survives typed clear", "cleared ordinary", "ordinary survives typed remove" });
                typedSentinel.Typed.ShouldBe(new[] { "cleared typed", "typed survives ordinary clear", "removed typed" });
                shared.Disposals.ShouldBe(0);
                ordinarySentinel.Disposals.ShouldBe(0);
                typedSentinel.Disposals.ShouldBe(0);
            }
            finally
            {
                Utilities.Logger.ClearDestinations<ILoggingDestination>();
                Utilities.Logger.ClearDestinations();
            }
        }

        [Fact]
        public void ShouldRouteByDeclaredTypeInsteadOfRuntimeSubtypeOrAssignability()
        {
            var ordinary = new RouteRecipient<RouteBase>();
            var baseRecipient = new RouteRecipient<RouteBase>();
            var derivedRecipient = new RouteRecipient<RouteDerived>();
            var baseSentinel = new RouteRecipient<RouteBase>();
            var derivedSentinel = new RouteRecipient<RouteDerived>();
            var metadata = new RouteDerived();
            try
            {
                Utilities.Logger.ClearDestinations();
                Utilities.Logger.ClearDestinations<RouteBase>();
                Utilities.Logger.ClearDestinations<RouteDerived>();
                Utilities.Logger.AddDestination((ILoggingDestination)ordinary);
                Utilities.Logger.AddDestination<RouteBase>(baseRecipient);
                Utilities.Logger.AddDestination<RouteDerived>(derivedRecipient);
                Utilities.Logger.Debug<RouteBase>("declared base", metadata);
                Utilities.Logger.Debug<RouteDerived>("declared derived", metadata);
                baseRecipient.Typed.ShouldBe(new[] { "declared base" });
                derivedRecipient.Typed.ShouldBe(new[] { "declared derived" });
                baseRecipient.Metadata.ShouldBeSameAs(metadata);
                derivedRecipient.Metadata.ShouldBeSameAs(metadata);
                Utilities.Logger.ClearDestinations<RouteBase>();
                Utilities.Logger.AddDestination<RouteBase>(baseSentinel);
                Utilities.Logger.Debug<RouteDerived>("derived survives", metadata);
                Utilities.Logger.Debug<RouteBase>("base cleared", metadata);
                derivedRecipient.Typed.ShouldBe(new[] { "declared derived", "derived survives" });
                baseRecipient.Typed.ShouldBe(new[] { "declared base" });
                baseSentinel.Typed.ShouldBe(new[] { "base cleared" });
                Utilities.Logger.AddDestination<RouteBase>(baseRecipient);
                Utilities.Logger.ClearDestinations<RouteDerived>();
                Utilities.Logger.AddDestination<RouteDerived>(derivedSentinel);
                Utilities.Logger.Debug<RouteBase>("base survives", metadata);
                Utilities.Logger.Debug<RouteDerived>("derived cleared", metadata);
                baseRecipient.Typed.ShouldBe(new[] { "declared base", "base survives" });
                derivedRecipient.Typed.ShouldBe(new[] { "declared derived", "derived survives" });
                derivedSentinel.Typed.ShouldBe(new[] { "derived cleared" });
                ordinary.Ordinary.ShouldBeEmpty();
                baseRecipient.Disposals.ShouldBe(0);
                derivedRecipient.Disposals.ShouldBe(0);
            }
            finally
            {
                Utilities.Logger.ClearDestinations<RouteBase>();
                Utilities.Logger.ClearDestinations<RouteDerived>();
                Utilities.Logger.ClearDestinations();
            }
        }

        [Theory]
        [InlineData(true, true)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public void ShouldContinueOnlyTheDeclaredTypedRouteBeforeReportingEitherCallbackFailure(bool eligibilityFailure, bool failingFirst)
        {
            var attempts = new List<string>();
            var failing = new FailureDestination<RouteBase>("failing", attempts);
            var healthy = new FailureDestination<RouteBase>("healthy", attempts);
            var ordinary = new RouteRecipient<RouteBase>();
            var runtimeRoute = new RouteRecipient<RouteDerived>();
            var cause = new InvalidOperationException("synthetic typed callback failure");
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
                    Utilities.Logger.ClearDestinations<RouteBase>();
                    Utilities.Logger.ClearDestinations<RouteDerived>();
                    Utilities.Logger.AddDestination((ILoggingDestination)ordinary);
                    Utilities.Logger.AddDestination<RouteDerived>(runtimeRoute);
                    Utilities.Logger.AddDestination<RouteBase>(failingFirst ? failing : healthy);
                    Utilities.Logger.AddDestination<RouteBase>(failingFirst ? healthy : failing);
                    Utilities.Logger.DispatchFailed += observer;
                    var metadata = new RouteDerived();
                    var payload = new Exception("synthetic typed payload");
                    var failure = Record.Exception(() => Utilities.Logger.Warn<RouteBase>("unchanged typed context", metadata, payload));
                    var failedAttempts = eligibilityFailure ? new[] { "failing:eligibility" } : new[] { "failing:eligibility", "failing:log" };
                    var healthyAttempts = new[] { "healthy:eligibility", "healthy:log" };
                    attempts.ShouldBe((failingFirst ? failedAttempts.Concat(healthyAttempts) : healthyAttempts.Concat(failedAttempts)).Concat(new[] { "notice" }));
                    failing.ValidationCalls.ShouldBe(1);
                    failing.LogCalls.ShouldBe(eligibilityFailure ? 0 : 1);
                    if (eligibilityFailure)
                    {
                        failing.ReceivedMessage.ShouldBeNull();
                        failing.ReceivedException.ShouldBeNull();
                        failing.ReceivedMetadata.ShouldBeNull();
                    }
                    healthy.ValidationCalls.ShouldBe(1);
                    healthy.LogCalls.ShouldBe(1);
                    healthy.ReceivedLevel.ShouldBe(LogLevels.WarningOnly);
                    healthy.ReceivedMessage.ShouldBe("unchanged typed context");
                    healthy.ReceivedException.ShouldBeSameAs(payload);
                    healthy.ReceivedMetadata.ShouldBeSameAs(metadata);
                    ordinary.ValidationCalls.ShouldBe(0);
                    ordinary.Ordinary.ShouldBeEmpty();
                    runtimeRoute.ValidationCalls.ShouldBe(0);
                    runtimeRoute.Typed.ShouldBeEmpty();
                    (failure is LogDispatchException).ShouldBeTrue("B07 applies to the exact declared-T route without a strict opt-in");
                    reports.Count.ShouldBe(1);
                    LoggerTests.AssertFailureReport(reports[0], new[] { failingFirst ? 1 : 2 }, new[] { eligibilityFailure ? LogFailureStage.Eligibility : LogFailureStage.Output }, 0);
                    LoggerTests.AssertEquivalentFailureReports(reports[0], ((LogDispatchException)failure).Report);
                    stderr.ToString().Length.ShouldBeInRange(1, 512);
                }
                finally
                {
                    Utilities.Logger.DispatchFailed -= observer;
                    Utilities.Logger.ClearDestinations<RouteBase>();
                    Utilities.Logger.ClearDestinations<RouteDerived>();
                    Utilities.Logger.ClearDestinations();
                    Console.SetError(originalError);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShouldKeepSuccessfulOrRejectedTypedCallsAndArgumentGuardsSilent(bool accepted)
        {
            var destination = new FailureDestination<object>("recipient", new List<string>()) { OnEligibility = () => accepted };
            var ordinary = new RouteRecipient<object>();
            var notices = 0;
            Action<LogFailureReport> observer = report => notices++;
            var originalError = Console.Error;
            using (var stderr = new StringWriter())
            {
                try
                {
                    Console.SetError(stderr);
                    Utilities.Logger.ClearDestinations();
                    Utilities.Logger.ClearDestinations<object>();
                    Utilities.Logger.AddDestination((ILoggingDestination)ordinary);
                    Utilities.Logger.AddDestination<object>(destination);
                    Utilities.Logger.DispatchFailed += observer;
                    Should.Throw<ArgumentNullException>(() => Utilities.Logger.Warn<object>(null, null));
                    destination.ValidationCalls.ShouldBe(0);
                    Utilities.Logger.Warn<object>("", null);
                    destination.ValidationCalls.ShouldBe(1);
                    destination.LogCalls.ShouldBe(accepted ? 1 : 0);
                    destination.ReceivedMessage.ShouldBe(accepted ? "" : null);
                    destination.ReceivedMetadata.ShouldBeNull();
                    destination.ReceivedException.ShouldBeNull();
                    ordinary.ValidationCalls.ShouldBe(0);
                    notices.ShouldBe(0);
                    stderr.ToString().ShouldBeEmpty();
                }
                finally
                {
                    Utilities.Logger.DispatchFailed -= observer;
                    Utilities.Logger.ClearDestinations<object>();
                    Utilities.Logger.ClearDestinations();
                    Console.SetError(originalError);
                }
            }
        }

        [Fact]
        public void ShouldClassifyAnInternalEligibilityRecheckAsOneOutputFailureAndPreserveDefaultMetadata()
        {
            var attempts = new List<string>();
            var failing = new FailureDestination<int>("failing", attempts);
            var healthy = new FailureDestination<int>("healthy", attempts);
            failing.OnEligibility = () =>
            {
                if (failing.ValidationCalls > 1) throw new InvalidOperationException("synthetic internal recheck");
                return true;
            };
            failing.OnLog = () => failing.ValidateMessageLevel(LogLevels.DebugOnly);
            var originalError = Console.Error;
            using (var stderr = new StringWriter())
            {
                try
                {
                    Console.SetError(stderr);
                    Utilities.Logger.ClearDestinations<int>();
                    Utilities.Logger.AddDestination<int>(failing);
                    Utilities.Logger.AddDestination<int>(healthy);
                    var failure = Record.Exception(() => Utilities.Logger.Debug("default metadata", default(int)));
                    attempts.ShouldBe(new[] { "failing:eligibility", "failing:log", "failing:eligibility", "healthy:eligibility", "healthy:log" });
                    healthy.ReceivedMetadata.ShouldBe(0);
                    healthy.ReceivedMessage.ShouldBe("default metadata");
                    failing.LogCalls.ShouldBe(1);
                    (failure is LogDispatchException).ShouldBeTrue();
                    LoggerTests.AssertFailureReport(((LogDispatchException)failure).Report, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
                }
                finally
                {
                    Utilities.Logger.ClearDestinations<int>();
                    Console.SetError(originalError);
                }
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ShouldSuppressOnlySynchronousRecursiveReportingAcrossRoutesAndRestoreIt(bool typedOuter, bool fromWriter)
        {
            var ordinary = new EventDestination(LogLevels.Trace);
            var ordinaryHealthy = new EventDestination(LogLevels.Trace);
            var typed = new FailureDestination<ReentryMetadata>("typed", new List<string>());
            var typedHealthy = new FailureDestination<ReentryMetadata>("typed-healthy", new List<string>());
            var ordinaryMessages = new List<string>();
            var typedMessages = new List<string>();
            ordinary.LoggingEvent += (sender, args) => { if (args.RawMessage.EndsWith("failure", StringComparison.Ordinal)) throw new InvalidOperationException("synthetic ordinary failure"); };
            ordinaryHealthy.LoggingEvent += (sender, args) => ordinaryMessages.Add(args.RawMessage);
            typed.OnLog = () => { if (typed.ReceivedMessage.EndsWith("failure", StringComparison.Ordinal)) throw new InvalidOperationException("synthetic typed failure"); };
            typedHealthy.OnLog = () => typedMessages.Add(typedHealthy.ReceivedMessage);
            var metadata = new ReentryMetadata();
            var reports = new List<LogFailureReport>();
            var reentered = false;
            var insideNestedDispatch = false;
            var nestedWrites = 0;
            Exception nestedFailure = null;
            Action reenter = () =>
            {
                reentered = true;
                insideNestedDispatch = true;
                try
                {
                    Utilities.Logger.Debug("nested ordinary success");
                    Utilities.Logger.Debug("nested typed success", metadata);
                    nestedFailure = Record.Exception(() =>
                    {
                        if (typedOuter) Utilities.Logger.Debug("nested failure");
                        else Utilities.Logger.Debug("nested failure", metadata);
                    });
                }
                finally { insideNestedDispatch = false; }
            };
            Action<LogFailureReport> observer = report =>
            {
                reports.Add(report);
                if (!fromWriter && !reentered) reenter();
            };
            var originalError = Console.Error;
            using (var stderr = new LoggerTests.ObservingErrorWriter())
            {
                stderr.OnWrite = () =>
                {
                    if (insideNestedDispatch) nestedWrites++;
                    else if (fromWriter && !reentered) reenter();
                };
                try
                {
                    Console.SetError(stderr);
                    Utilities.Logger.ClearDestinations();
                    Utilities.Logger.ClearDestinations<ReentryMetadata>();
                    Utilities.Logger.AddDestination(ordinary);
                    Utilities.Logger.AddDestination(ordinaryHealthy);
                    Utilities.Logger.AddDestination<ReentryMetadata>(typed);
                    Utilities.Logger.AddDestination<ReentryMetadata>(typedHealthy);
                    Utilities.Logger.DispatchFailed += observer;
                    var outerFailure = Record.Exception(() =>
                    {
                        if (typedOuter) Utilities.Logger.Debug("outer failure", metadata);
                        else Utilities.Logger.Debug("outer failure");
                    });
                    reentered.ShouldBeTrue("B17 reports the original call before returning its mandatory failure");
                    ordinaryMessages.ShouldBe(typedOuter ? new[] { "nested ordinary success", "nested failure" } : new[] { "outer failure", "nested ordinary success" });
                    typedMessages.ShouldBe(typedOuter ? new[] { "outer failure", "nested typed success" } : new[] { "nested typed success", "nested failure" });
                    typedHealthy.ReceivedMetadata.ShouldBeSameAs(metadata);
                    (outerFailure is LogDispatchException).ShouldBeTrue();
                    (nestedFailure is LogDispatchException).ShouldBeTrue("B17 suppresses reporting, never the nested failure");
                    reports.Count.ShouldBe(1);
                    nestedWrites.ShouldBe(0);
                    var outerReport = ((LogDispatchException)outerFailure).Report;
                    var nestedReport = ((LogDispatchException)nestedFailure).Report;
                    LoggerTests.AssertFailureReport(outerReport, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
                    LoggerTests.AssertFailureReport(nestedReport, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
                    LoggerTests.AssertEquivalentFailureReports(reports[0], outerReport);
                    nestedReport.CorrelationId.ShouldNotBe(outerReport.CorrelationId);
                    var firstLength = stderr.Text.Length;
                    firstLength.ShouldBeInRange(1, 512);
                    var laterFailure = Record.Exception(() =>
                    {
                        if (typedOuter) Utilities.Logger.Debug("later failure", metadata);
                        else Utilities.Logger.Debug("later failure");
                    });
                    (laterFailure is LogDispatchException).ShouldBeTrue();
                    reports.Count.ShouldBe(2);
                    LoggerTests.AssertEquivalentFailureReports(reports[1], ((LogDispatchException)laterFailure).Report);
                    reports[1].CorrelationId.ShouldNotBe(outerReport.CorrelationId);
                    (stderr.Text.Length - firstLength).ShouldBeInRange(1, 512);
                    stderr.FlushCalls.ShouldBe(0);
                    stderr.Disposals.ShouldBe(0);
                }
                finally
                {
                    Utilities.Logger.DispatchFailed -= observer;
                    Utilities.Logger.ClearDestinations<ReentryMetadata>();
                    Utilities.Logger.ClearDestinations();
                    Console.SetError(originalError);
                }
            }
        }

        [Fact]
        public void ShouldKeepTheOuterOriginalFailureWhenASubscriberLetsANestedTypedFailureEscape()
        {
            var ordinary = new EventDestination(LogLevels.Trace);
            ordinary.LoggingEvent += (sender, args) => { throw new InvalidOperationException("synthetic outer output"); };
            var healthy = new FailureDestination<ReentryMetadata>("healthy", new List<string>());
            var failing = new FailureDestination<ReentryMetadata>("failing", new List<string>()) { OnEligibility = () => { throw new InvalidOperationException("synthetic inner eligibility"); } };
            var reports = new List<LogFailureReport>();
            var nestedCalls = 0;
            Action<LogFailureReport> nested = report =>
            {
                nestedCalls++;
                if (nestedCalls == 1) Utilities.Logger.Debug("nested", new ReentryMetadata());
            };
            Action<LogFailureReport> later = report => reports.Add(report);
            var originalError = Console.Error;
            using (var stderr = new StringWriter())
            {
                try
                {
                    Console.SetError(stderr);
                    Utilities.Logger.ClearDestinations();
                    Utilities.Logger.ClearDestinations<ReentryMetadata>();
                    Utilities.Logger.AddDestination(ordinary);
                    Utilities.Logger.AddDestination<ReentryMetadata>(healthy);
                    Utilities.Logger.AddDestination<ReentryMetadata>(failing);
                    Utilities.Logger.DispatchFailed += nested;
                    Utilities.Logger.DispatchFailed += later;
                    var observed = Record.Exception(() => Utilities.Logger.Debug("outer"));
                    nestedCalls.ShouldBe(1);
                    healthy.LogCalls.ShouldBe(1);
                    healthy.ReceivedMessage.ShouldBe("nested");
                    failing.ValidationCalls.ShouldBe(1);
                    failing.LogCalls.ShouldBe(0);
                    (observed is LogDispatchException).ShouldBeTrue();
                    reports.Count.ShouldBe(1);
                    LoggerTests.AssertFailureReport(((LogDispatchException)observed).Report, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
                    LoggerTests.AssertEquivalentFailureReports(reports[0], ((LogDispatchException)observed).Report);
                    stderr.ToString().Length.ShouldBeInRange(1, 512);
                }
                finally
                {
                    Utilities.Logger.DispatchFailed -= nested;
                    Utilities.Logger.DispatchFailed -= later;
                    Utilities.Logger.ClearDestinations<ReentryMetadata>();
                    Utilities.Logger.ClearDestinations();
                    Console.SetError(originalError);
                }
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ShouldPreserveOpaqueOrNullTypedPayloadsAfterFailureWithoutDiagnosticInspection(bool absent)
        {
            var canary = "m2c-typed-private-" + Guid.NewGuid().ToString("N");
            var metadata = absent ? null : new OpaqueMetadata(canary);
            var payload = new LoggerTests.OpaqueFailure(canary + "-payload");
            var cause = new LoggerTests.OpaqueFailure(canary + "-cause");
            var failing = new FailureDestination<OpaqueMetadata>("failing", new List<string>());
            var healthy = new FailureDestination<OpaqueMetadata>("healthy", new List<string>());
            int? receivedValue = null;
            failing.OnLog = () =>
            {
                if (failing.ReceivedMetadata != null) failing.ReceivedMetadata.Value = 7;
                throw cause;
            };
            healthy.OnLog = () => receivedValue = healthy.ReceivedMetadata == null ? (int?)null : healthy.ReceivedMetadata.Value;
            var reports = new List<LogFailureReport>();
            Action<LogFailureReport> observer = report => reports.Add(report);
            var originalError = Console.Error;
            using (var stderr = new StringWriter())
            {
                try
                {
                    Console.SetError(stderr);
                    Utilities.Logger.ClearDestinations<OpaqueMetadata>();
                    Utilities.Logger.AddDestination<OpaqueMetadata>(failing);
                    Utilities.Logger.AddDestination<OpaqueMetadata>(healthy);
                    Utilities.Logger.DispatchFailed += observer;
                    var observed = Record.Exception(() => Utilities.Logger.Critical(payload, metadata, canary));
                    healthy.LogCalls.ShouldBe(1);
                    healthy.ReceivedMetadata.ShouldBeSameAs(metadata);
                    healthy.ReceivedException.ShouldBeSameAs(payload);
                    healthy.ReceivedMessage.ShouldBe(canary);
                    receivedValue.ShouldBe(absent ? (int?)null : 7);
                    (observed is LogDispatchException).ShouldBeTrue();
                    var failure = (LogDispatchException)observed;
                    reports.Count.ShouldBe(1);
                    LoggerTests.AssertFailureReport(failure.Report, new[] { 1 }, new[] { LogFailureStage.Output }, 0);
                    LoggerTests.AssertEquivalentFailureReports(reports[0], failure.Report);
                    LoggerTests.AssertNoFailureBacklinks(failure, reports[0], failing, healthy, metadata, cause, payload, observer, canary);
                    foreach (var text in new[] { failure.Message, failure.ToString(), stderr.ToString() })
                    {
                        text.Contains(canary).ShouldBeFalse();
                        text.Contains(typeof(OpaqueMetadata).FullName).ShouldBeFalse();
                    }
                    if (metadata != null) metadata.DiagnosticReads.ShouldBe(0);
                    payload.DiagnosticReads.ShouldBe(0);
                    cause.DiagnosticReads.ShouldBe(0);
                }
                finally
                {
                    Utilities.Logger.DispatchFailed -= observer;
                    Utilities.Logger.ClearDestinations<OpaqueMetadata>();
                    Console.SetError(originalError);
                }
            }
        }

        private sealed class ReentryMetadata { }

        private sealed class OpaqueMetadata
        {
            private readonly string _canary;
            public OpaqueMetadata(string canary) { _canary = canary; }
            public int Value;
            public int DiagnosticReads { get; private set; }
            public string PrivateValue { get { DiagnosticReads++; return _canary; } }
            public override string ToString() { DiagnosticReads++; return _canary; }
            public override bool Equals(object other) { DiagnosticReads++; return ReferenceEquals(this, other); }
            public override int GetHashCode() { DiagnosticReads++; return 0; }
        }

        private sealed class FailureDestination<T> : Utilities.Generics.ILoggingDestination<T>
        {
            private readonly string _name;
            private readonly List<string> _attempts;
            public FailureDestination(string name, List<string> attempts) { _name = name; _attempts = attempts; }
            public Func<bool> OnEligibility { get; set; }
            public Action OnLog { get; set; }
            public int ValidationCalls { get; private set; }
            public int LogCalls { get; private set; }
            public LogLevels ReceivedLevel { get; private set; }
            public string ReceivedMessage { get; private set; }
            public Exception ReceivedException { get; private set; }
            public T ReceivedMetadata { get; private set; }
            public bool ValidateMessageLevel(LogLevels level)
            {
                ValidationCalls++;
                _attempts.Add(_name + ":eligibility");
                return OnEligibility == null || OnEligibility();
            }
            public void Log(LogLevels level, T metadata, string message = null, Exception ex = null)
            {
                LogCalls++;
                ReceivedLevel = level;
                ReceivedMessage = message;
                ReceivedException = ex;
                ReceivedMetadata = metadata;
                _attempts.Add(_name + ":log");
                OnLog?.Invoke();
            }
        }

        private class RouteBase { }
        private sealed class RouteDerived : RouteBase { }

        private sealed class RouteRecipient<T> : ILoggingDestination, Utilities.Generics.ILoggingDestination<T>, IDisposable
        {
            public List<string> Ordinary { get; } = new List<string>();
            public List<string> Typed { get; } = new List<string>();
            public T Metadata { get; private set; }
            public int ValidationCalls { get; private set; }
            public int Disposals { get; private set; }
            public bool ValidateMessageLevel(LogLevels level) { ValidationCalls++; return true; }
            public void Log(LogLevels level, string message = null, Exception ex = null) { Ordinary.Add(message); }
            public void Log(LogLevels level, T metadata, string message = null, Exception ex = null) { Metadata = metadata; Typed.Add(message); }
            public void Dispose() { Disposals++; }
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
