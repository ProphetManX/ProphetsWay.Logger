using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;
using NativeLogger = ProphetsWay.Utilities.Logger;

namespace ProphetsWay.Logger.Test
{
	[Collection(MicrosoftInboundTestCollection.Name)]
	public sealed class MicrosoftLoggerDestinationTests
	{
		private readonly InboundTestEnvironment _environment;
		public MicrosoftLoggerDestinationTests(InboundTestEnvironment environment) { _environment = environment; }

		[Fact]
		public void ConstructorsValidateApprovedArgumentsAndMakeNoMicrosoftCalls()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var ordinary = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
			var fixedOrdinary = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
			var generic = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, factory);
			var fixedGeneric = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, logger);
			factory.Categories.ShouldBeEmpty();
			factory.CreateCalls.ShouldBe(0);
			logger.IsEnabledCalls.ShouldBe(0);
			logger.BeginScopeStates.ShouldBeEmpty();
			logger.Records.ShouldBeEmpty();
			ordinary.LabelPolicy.Mode.ShouldBe(LabelFilterMode.NoFilter);
			fixedOrdinary.LabelPolicy.Mode.ShouldBe(LabelFilterMode.NoFilter);
			generic.LabelPolicy.Mode.ShouldBe(LabelFilterMode.NoFilter);
			fixedGeneric.LabelPolicy.Mode.ShouldBe(LabelFilterMode.NoFilter);
			Should.Throw<ArgumentNullException>(() => new MicrosoftLoggerDestination(LogLevels.Trace, (ILoggerFactory)null)).ParamName.ShouldBe("factory");
			Should.Throw<ArgumentNullException>(() => new MicrosoftLoggerDestination(LogLevels.Trace, factory, null)).ParamName.ShouldBe("defaultCategoryName");
			Should.Throw<ArgumentNullException>(() => new MicrosoftLoggerDestination(LogLevels.Trace, (ILogger)null)).ParamName.ShouldBe("logger");
			Should.Throw<ArgumentNullException>(() => new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, (ILoggerFactory)null)).ParamName.ShouldBe("factory");
			Should.Throw<ArgumentNullException>(() => new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, factory, null)).ParamName.ShouldBe("defaultCategoryName");
			Should.Throw<ArgumentNullException>(() => new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, (ILogger)null)).ParamName.ShouldBe("logger");
			Should.Throw<ArgumentOutOfRangeException>(() => new MicrosoftLoggerDestination((LogLevels)64, factory)).ParamName.ShouldBe("reportingLevel");
			Should.Throw<ArgumentOutOfRangeException>(() => new GenericMicrosoftLoggerDestination<UnfamiliarState>((LogLevels)(-1), logger)).ParamName.ShouldBe("reportingLevel");
			var zeroMask = new MicrosoftLoggerDestination(0, factory);
			zeroMask.ValidateMessageLevel(LogLevels.Critical).ShouldBeFalse();
			factory.CreateCalls.ShouldBe(0);
			logger.IsEnabledCalls.ShouldBe(0);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void OrdinaryRouteExportsStringStateAndTheExactThreeGroupEnvelopeAfterEligibility()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
			var order = new List<string>();
			factory.OnCreate = category => { order.Add("factory:" + category); return logger; };
			logger.OnIsEnabled = level => { order.Add("enabled:" + level); return true; };
			logger.OnLog = loggedRecord => order.Add("log");
			var exception = new InvalidOperationException("ordinary synthetic exception");
			using (RegisterOrdinary(destination, Settings()))
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "native string", exception);
			factory.Categories.ShouldBe(new[] { "ProphetsWay.Logger" });
			order.ShouldBe(new[] { "factory:ProphetsWay.Logger", "enabled:Information", "log" });
			logger.Records.ShouldHaveSingleItem();
			factory.DisposeCalls.ShouldBe(0);
			logger.DisposeCalls.ShouldBe(0);
			var record = logger.Records.Single();
			record.Level.ShouldBe(LogLevel.Information);
			record.EventId.ShouldBe(default(EventId));
			record.Exception.ShouldBeSameAs(exception);
			record.FormattedMessage.ShouldBe("native string");
			record.Formatter.ShouldNotBeNull();
			logger.IsEnabledCalls.ShouldBe(1);
			logger.BeginScopeStates.ShouldBeEmpty();
			var envelope = Envelope(record.State);
			envelope.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.Entry", "ProphetsWay.NativeContexts", "ProphetsWay.ExternalScopes" });
			var entry = envelope[0].Value;
			Read<string>(entry, "State").ShouldBe("native string");
			Read<object>(entry, "OriginalState").ShouldBe("native string");
			Read<Type>(entry, "StateType").ShouldBe(typeof(string));
			Read<string>(entry, "RawMessage").ShouldBe("native string");
			Read<string>(entry, "FormattedMessage").ShouldBe("native string");
			Read<string>(entry, "CategoryName").ShouldBeNull();
			Read<EventId?>(entry, "EventId").ShouldBeNull();
			Read<Delegate>(entry, "ProducerFormatter").ShouldBeNull();
			Read<LogLevels>(entry, "OriginalLevel").ShouldBe(LogLevels.InformationOnly);
			Read<Exception>(entry, "Exception").ShouldBeSameAs(exception);
			Read<ReadOnlyCollection<KeyValuePair<string, object>>>(entry, "Properties").ShouldBeEmpty();
			Read<ReadOnlyCollection<LogBridgeScope>>(entry, "Scopes").ShouldBeEmpty();
			Read<LogAnnotations>(entry, "Annotations").ShouldBeNull();
			var bridgeMetadata = Read<ReadOnlyCollection<KeyValuePair<string, object>>>(entry, "BridgeMetadata");
			bridgeMetadata.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.CategoryName", "ProphetsWay.EventId", "ProphetsWay.OriginalLevel", "ProphetsWay.NativeContext", "ProphetsWay.Annotations", "ProphetsWay.Scopes" });
			bridgeMetadata[0].Value.ShouldBeNull();
			bridgeMetadata[1].Value.ShouldBeNull();
			bridgeMetadata[2].Value.ShouldBe(LogLevels.InformationOnly);
			bridgeMetadata[4].Value.ShouldBeNull();
			ReadOnlyCollection<LogContext> contexts = (ReadOnlyCollection<LogContext>)envelope[1].Value;
			contexts.ShouldHaveSingleItem();
			Read<LogContext>(entry, "NativeContext").ShouldBeSameAs(contexts[0]);
			bridgeMetadata[3].Value.ShouldBeSameAs(contexts[0]);
			bridgeMetadata[5].Value.ShouldBeSameAs(Read<ReadOnlyCollection<LogBridgeScope>>(entry, "Scopes"));
			contexts[0].EventTimestampUtc.Offset.ShouldBe(TimeSpan.Zero);
			((ReadOnlyCollection<LogBridgeScope>)envelope[2].Value).ShouldBeEmpty();
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void DestinationConstructionDoesNotChangeNativeRouteMembership()
		{
			var ordinaryMarker = new CountingOrdinaryDestination();
			var exactMarker = new CountingExactDestination<UnfamiliarState>();
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			using (RegisterOrdinary(ordinaryMarker, Settings()))
			using (RegisterExact<UnfamiliarState>(exactMarker, Settings()))
			{
				var ordinary = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
				var fixedOrdinary = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
				var typed = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, factory);
				var fixedTyped = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, logger);
				ordinary.ShouldNotBeNull();
				fixedOrdinary.ShouldNotBeNull();
				typed.ShouldNotBeNull();
				fixedTyped.ShouldNotBeNull();
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "ordinary membership unchanged", (Exception)null);
				NativeLogger.LogAnnotated<UnfamiliarState>(null, LogLevels.InformationOnly, new UnfamiliarState(), "exact membership unchanged");
				ordinaryMarker.Calls.ShouldBe(1);
				exactMarker.Calls.ShouldBe(1);
				factory.CreateCalls.ShouldBe(0);
				logger.IsEnabledCalls.ShouldBe(0);
				logger.BeginScopeStates.ShouldBeEmpty();
				logger.Records.ShouldBeEmpty();
			}
			_environment.AssertNoAutomaticOutput();
		}

		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData(" \t")]
		public void OrdinaryRoutePreservesPermittedNullEmptyAndWhitespaceMessages(string message)
		{
			var logger = new RecordingMicrosoftLogger();
			using (RegisterOrdinary(new MicrosoftLoggerDestination(LogLevels.Trace, logger), Settings()))
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, message, (Exception)null);
			logger.Records.ShouldHaveSingleItem();
			var entry = Envelope(logger.Records.Single().State)[0].Value;
			Read<string>(entry, "State").ShouldBe(message);
			Read<string>(entry, "RawMessage").ShouldBe(message);
			Read<string>(entry, "FormattedMessage").ShouldBe(message);
			logger.Records.Single().FormattedMessage.ShouldBe(message);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ExactDeclaredRoutePreservesNullTypedMetadataAsOriginalState()
		{
			var logger = new RecordingMicrosoftLogger();
			var destination = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, logger);
			using (RegisterExact<UnfamiliarState>(destination, Settings()))
				NativeLogger.LogAnnotated<UnfamiliarState>(null, LogLevels.InformationOnly, null, null);
			logger.Records.ShouldHaveSingleItem();
			var entry = Envelope(logger.Records.Single().State)[0].Value;
			Read<object>(entry, "OriginalState").ShouldBeNull();
			Read<Type>(entry, "StateType").ShouldBe(typeof(UnfamiliarState));
			Read<string>(entry, "RawMessage").ShouldBeNull();
			Read<LogLevels>(entry, "OriginalLevel").ShouldBe(LogLevels.InformationOnly);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ExactValueTypeRoutePreservesDefaultOriginalStateAndDeclaredType()
		{
			var logger = new RecordingMicrosoftLogger();
			var destination = new GenericMicrosoftLoggerDestination<int>(LogLevels.Trace, logger);
			using (RegisterExact<int>(destination, Settings()))
				NativeLogger.LogAnnotated<int>(null, LogLevels.InformationOnly, default(int), "default typed value");
			logger.Records.ShouldHaveSingleItem();
			var entry = Envelope(logger.Records.Single().State)[0].Value;
			Read<Type>(entry, "StateType").ShouldBe(typeof(int));
			Read<object>(entry, "State").ShouldBe(0);
			Read<object>(entry, "OriginalState").ShouldBe(0);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ExactDeclaredRoutePreservesStructuredAndUnfamiliarStateWithoutRuntimeRerouting()
		{
			var baseLogger = new RecordingMicrosoftLogger();
			var derivedLogger = new RecordingMicrosoftLogger();
			var ordinaryLogger = new RecordingMicrosoftLogger();
			var baseFactory = new RecordingMicrosoftLoggerFactory(baseLogger);
			var derivedFactory = new RecordingMicrosoftLoggerFactory(derivedLogger);
			var ordinaryFactory = new RecordingMicrosoftLoggerFactory(ordinaryLogger);
			var baseDestination = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, baseFactory);
			var derivedDestination = new GenericMicrosoftLoggerDestination<DerivedState>(LogLevels.Trace, derivedFactory);
			var ordinaryDestination = new MicrosoftLoggerDestination(LogLevels.Trace, ordinaryFactory);
			var state = new DerivedState();
			using (RegisterExact<UnfamiliarState>(baseDestination, Settings()))
			using (RegisterExact<DerivedState>(derivedDestination, Settings()))
			using (RegisterOrdinary(ordinaryDestination, Settings()))
				NativeLogger.LogAnnotated<DerivedState>(null, LogLevels.WarningOnly, state, "typed raw");
			baseFactory.CreateCalls.ShouldBe(0);
			ordinaryFactory.CreateCalls.ShouldBe(0);
			derivedFactory.Categories.ShouldBe(new[] { "ProphetsWay.Logger" });
			derivedLogger.Records.ShouldHaveSingleItem();
			var entry = Envelope(derivedLogger.Records.Single().State)[0].Value;
			Read<object>(entry, "OriginalState").ShouldBeSameAs(state);
			Read<Type>(entry, "StateType").ShouldBe(typeof(DerivedState));
			state.ToStringCalls.ShouldBe(0);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void StructuredStateMembershipIsCopiedOnceAndApplicationKeysDoNotBecomeLabels()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var pairs = new List<KeyValuePair<string, object>>
			{
				new KeyValuePair<string, object>("sensitivity", "not a mapped label"),
				new KeyValuePair<string, object>("duplicate", 1),
				new KeyValuePair<string, object>("duplicate", null),
				new KeyValuePair<string, object>("", "empty key"),
				new KeyValuePair<string, object>("ProphetsWay.CategoryName", "application value"),
				default(KeyValuePair<string, object>)
			};
			var destination = new GenericMicrosoftLoggerDestination<List<KeyValuePair<string, object>>>(LogLevels.Trace, factory);
			factory.OnCreate = category => { pairs.Add(new KeyValuePair<string, object>("later", "callback mutation")); return logger; };
			using (RegisterExact<List<KeyValuePair<string, object>>>(destination, Settings()))
				NativeLogger.LogAnnotated<List<KeyValuePair<string, object>>>(null, LogLevels.InformationOnly, pairs, "structured raw");
			logger.Records.ShouldHaveSingleItem();
			var entry = Envelope(logger.Records.Single().State)[0].Value;
			Read<object>(entry, "OriginalState").ShouldBeSameAs(pairs);
			var captured = Read<ReadOnlyCollection<KeyValuePair<string, object>>>(entry, "Properties");
			captured.Count.ShouldBe(6);
			captured.Select(pair => pair.Key).ShouldBe(new[] { "sensitivity", "duplicate", "duplicate", "", "ProphetsWay.CategoryName", null });
			captured[1].Value.ShouldBe(1);
			captured[2].Value.ShouldBeNull();
			captured[4].Value.ShouldBe("application value");
			captured[5].ShouldBe(default(KeyValuePair<string, object>));
			var bridgeMetadata = Read<ReadOnlyCollection<KeyValuePair<string, object>>>(entry, "BridgeMetadata");
			bridgeMetadata.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.CategoryName", "ProphetsWay.EventId", "ProphetsWay.OriginalLevel", "ProphetsWay.NativeContext", "ProphetsWay.Annotations", "ProphetsWay.Scopes" });
			bridgeMetadata[0].Value.ShouldBeNull("the application collision remains only in Properties");
			Read<LogContext>(entry, "NativeContext").Labels.EffectiveLabels.ShouldBeEmpty();
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ActiveIneligibleRegistrationSuppressesFallbackAndWithholdsMicrosoftCallbacks()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
			var policy = new DestinationLabelPolicy(LabelFilterMode.AllowOnly, new[] { new SensitivityLabel("explicit-only") });
			using (RegisterOrdinary(destination, Settings(LogLevels.Trace, true, policy)))
				NativeLogger.LogAnnotated(new LogAnnotations(new[] { new SensitivityLabel("different") }), LogLevels.InformationOnly, "withheld", (Exception)null);
			factory.CreateCalls.ShouldBe(0);
			logger.IsEnabledCalls.ShouldBe(0);
			logger.BeginScopeStates.ShouldBeEmpty();
			logger.Records.ShouldBeEmpty();
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void MicrosoftIsEnabledFalseRejectsWithoutScopesLogOrFallback()
		{
			var logger = new RecordingMicrosoftLogger { IsEnabledResult = false };
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			using (RegisterOrdinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory), Settings()))
				NativeLogger.LogAnnotated(null, LogLevels.WarningOnly, "rejected", (Exception)null);
			factory.CreateCalls.ShouldBe(1);
			logger.IsEnabledCalls.ShouldBe(1);
			logger.BeginScopeStates.ShouldBeEmpty();
			logger.Records.ShouldBeEmpty();
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void IntrinsicZeroMaskRejectsActualDispatchBeforeMicrosoftCallbacks()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var destination = new MicrosoftLoggerDestination(0, factory);
			using (RegisterOrdinary(destination, Settings()))
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "zero intrinsic mask", (Exception)null);
			factory.CreateCalls.ShouldBe(0);
			logger.IsEnabledCalls.ShouldBe(0);
			logger.BeginScopeStates.ShouldBeEmpty();
			logger.Records.ShouldBeEmpty();
			_environment.AssertNoAutomaticOutput();
		}

		[Theory]
		[InlineData("")]
		[InlineData(" \t")]
		[InlineData("custom fallback")]
		public void ExactRouteUsesConfiguredFallbackOnlyForAbsentOriginCategory(string fallback)
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var destination = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, factory, fallback);
			using (RegisterExact<UnfamiliarState>(destination, Settings()))
				NativeLogger.LogAnnotated<UnfamiliarState>(null, LogLevels.InformationOnly, new UnfamiliarState(), "fallback");
			factory.Categories.ShouldBe(new[] { fallback });
			logger.Records.ShouldHaveSingleItem();
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void FixedLoggerRetainsIdentityAndBorrowedLifetime()
		{
			var fixedLogger = new RecordingMicrosoftLogger();
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, fixedLogger);
			using (RegisterOrdinary(destination, Settings()))
				NativeLogger.LogAnnotated(null, LogLevels.ErrorOnly, "fixed route", (Exception)null);
			fixedLogger.Records.ShouldHaveSingleItem();
			fixedLogger.Records.Single().Level.ShouldBe(LogLevel.Error);
			fixedLogger.DisposeCalls.ShouldBe(0);
			destination.LabelPolicy.Mode.ShouldBe(LabelFilterMode.NoFilter);
			_environment.AssertNoAutomaticOutput();
		}

		[Theory]
		[InlineData(LogLevels.Critical, LogLevel.Critical)]
		[InlineData(LogLevels.ErrorOnly, LogLevel.Error)]
		[InlineData(LogLevels.WarningOnly, LogLevel.Warning)]
		[InlineData(LogLevels.InformationOnly, LogLevel.Information)]
		[InlineData(LogLevels.DebugOnly, LogLevel.Debug)]
		[InlineData(LogLevels.TraceOnly, LogLevel.Trace)]
		public void MapsEachSeverityExplicitlyAndRetainsTheNativeBit(LogLevels nativeLevel, LogLevel microsoftLevel)
		{
			var logger = new RecordingMicrosoftLogger();
			using (RegisterOrdinary(new MicrosoftLoggerDestination(LogLevels.Trace, logger), Settings()))
				NativeLogger.LogAnnotated(null, nativeLevel, "mapped", (Exception)null);
			logger.Records.Select(record => record.Level).ShouldBe(new[] { microsoftLevel });
			Read<LogLevels>(Envelope(logger.Records.Single().State)[0].Value, "OriginalLevel").ShouldBe(nativeLevel);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void CompositeMaskProducesOneLowestSeverityAndRetainsFullMask()
		{
			var logger = new RecordingMicrosoftLogger();
			using (RegisterOrdinary(new MicrosoftLoggerDestination(LogLevels.Trace, logger), Settings()))
				NativeLogger.LogAnnotated(null, LogLevels.Critical | LogLevels.ErrorOnly, "composite", (Exception)null);
			logger.Records.ShouldHaveSingleItem();
			logger.Records[0].Level.ShouldBe(LogLevel.Critical);
			Read<LogLevels>(Envelope(logger.Records[0].State)[0].Value, "OriginalLevel").ShouldBe(LogLevels.Critical | LogLevels.ErrorOnly);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ReplaysNativeFramesOuterToInnerAndRetainsTheirOriginPositions()
		{
			var logger = new RecordingMicrosoftLogger();
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
			var outerLabel = new SensitivityLabel("outer-native");
			var innerLabel = new SensitivityLabel("inner-native");
			var outerProperties = new[] { new KeyValuePair<string, object>("frame", "outer") };
			var innerProperties = new[] { new KeyValuePair<string, object>("frame", "inner") };
			using (RegisterOrdinary(destination, Settings()))
			using (NativeLogger.BeginScope(new LogAnnotations(new[] { outerLabel }), outerProperties))
			using (NativeLogger.BeginScope(new LogAnnotations(new[] { innerLabel }), innerProperties))
				NativeLogger.LogAnnotated(new LogAnnotations(new[] { new SensitivityLabel("entry-label") }), LogLevels.InformationOnly, "scoped", (Exception)null);
			logger.BeginScopeStates.Count.ShouldBe(2);
			var first = Envelope(logger.BeginScopeStates[0]);
			var second = Envelope(logger.BeginScopeStates[1]);
			first.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.NativeScope" });
			second.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.NativeScope" });
			((LogScopeFrame)first[0].Value).Properties.ShouldBe(outerProperties);
			((LogScopeFrame)second[0].Value).Properties.ShouldBe(innerProperties);
			var eventEnvelope = Envelope(logger.Records.Single().State);
			ReadOnlyCollection<LogContext> contexts = (ReadOnlyCollection<LogContext>)eventEnvelope[1].Value;
			contexts.ShouldHaveSingleItem();
			contexts[0].Scopes.Count.ShouldBe(2);
			var origins = contexts[0].Labels.Origins;
			origins.Count.ShouldBe(3);
			origins.Any(origin => origin.Label.Equals(outerLabel) && origin.ScopeIndex == 0 && origin.OccurrenceIndex == 0).ShouldBeTrue();
			origins.Any(origin => origin.Label.Equals(innerLabel) && origin.ScopeIndex == 1 && origin.OccurrenceIndex == 0).ShouldBeTrue();
			origins.Any(origin => origin.Label.Identifier == "entry-label" && origin.ScopeIndex == null && origin.OccurrenceIndex == 0).ShouldBeTrue();
			((ReadOnlyCollection<LogBridgeScope>)eventEnvelope[2].Value).ShouldBeEmpty();
			logger.DisposeOrder.ShouldBe(new[] { "scope-2", "scope-1" });
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void NullScopeHandleDoesNotWithholdLogOrPreventCleanupOfOtherFrames()
		{
			var logger = new RecordingMicrosoftLogger();
			var firstHandle = new TrackingScopeHandle("first", logger.DisposeOrder);
			var thirdHandle = new TrackingScopeHandle("third", logger.DisposeOrder);
			logger.BeginScopeHandler = (state, index) => index == 2 ? null : index == 1 ? firstHandle : thirdHandle;
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
			using (RegisterOrdinary(destination, Settings()))
			using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
			using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
			using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "null handle", (Exception)null);
			logger.Records.ShouldHaveSingleItem();
			logger.BeginScopeStates.Count.ShouldBe(3);
			logger.DisposeOrder.ShouldBe(new[] { "third", "first" });
			thirdHandle.DisposeCalls.ShouldBe(1);
			firstHandle.DisposeCalls.ShouldBe(1);
			((ReadOnlyCollection<LogContext>)Envelope(logger.Records.Single().State)[1].Value).Single().Scopes.Count.ShouldBe(3);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void RepeatedScopeHandleIsDisposedOncePerSuccessfulOpening()
		{
			var logger = new RecordingMicrosoftLogger();
			var repeatedHandle = new TrackingScopeHandle("shared", logger.DisposeOrder);
			logger.BeginScopeHandler = (state, index) => repeatedHandle;
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
			using (RegisterOrdinary(destination, Settings()))
			using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
			using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "repeated handle", (Exception)null);
			logger.Records.ShouldHaveSingleItem();
			logger.BeginScopeStates.Count.ShouldBe(2);
			logger.DisposeOrder.ShouldBe(new[] { "shared", "shared" });
			repeatedHandle.DisposeCalls.ShouldBe(2, "each successful opening occurrence owns one cleanup attempt");
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ScopeOpeningFailureStopsLaterFramesAndCleanupFailureIsOneOutput()
		{
			const string pathSentinel = @"C:\synthetic\scope\private.log";
			const string payloadSentinel = "synthetic-scope-payload-should-not-leak";
			const string dataKeySentinel = "synthetic-scope-key-should-not-leak";
			var scopeInner = new InvalidOperationException("synthetic scope inner cause " + pathSentinel);
			scopeInner.Data[dataKeySentinel] = payloadSentinel;
			var scopeFailure = new InvalidOperationException("foreign scope-open cause " + pathSentinel + " " + payloadSentinel, scopeInner);
			scopeFailure.Data[dataKeySentinel] = payloadSentinel;
			var cleanupInner = new InvalidOperationException("synthetic cleanup inner cause " + pathSentinel);
			cleanupInner.Data[dataKeySentinel] = payloadSentinel;
			var cleanupFailure = new InvalidOperationException("foreign cleanup cause " + pathSentinel + " " + payloadSentinel, cleanupInner);
			cleanupFailure.Data[dataKeySentinel] = payloadSentinel;
			var logger = new RecordingMicrosoftLogger();
			logger.BeginScopeHandler = (state, index) =>
			{
				if (index == 3) throw scopeFailure;
				return new TrackingScopeHandle("scope-" + index, logger.DisposeOrder,
					index == 2 ? cleanupFailure : null);
			};
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => reports.Add(report);
			var priorError = Console.Error;
			LogDispatchException failure = null;
			string diagnostic;
			using (RegisterOrdinary(destination, Settings()))
			using (var error = new StringWriter())
			{
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(error);
					using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
					using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
					using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
					{
						failure = Should.Throw<LogDispatchException>(() => NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, payloadSentinel, (Exception)null));
						failure.Report.Failures.ShouldHaveSingleItem();
						failure.Report.Failures[0].RegistrationId.ShouldBe(1);
						failure.Report.Failures[0].Stage.ShouldBe(LogFailureStage.Output);
						failure.InnerException.ShouldBeNull();
					}
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
				}
				diagnostic = error.ToString();
			}
			logger.BeginScopeStates.Count.ShouldBe(3);
			logger.Records.ShouldBeEmpty();
			logger.DisposeOrder.ShouldBe(new[] { "scope-2", "scope-1" });
			reports.ShouldHaveSingleItem();
			reports.Single().Failures.ShouldHaveSingleItem();
			AssertSafeOutputReport(failure, diagnostic, pathSentinel, payloadSentinel, dataKeySentinel,
				scopeInner.Message, scopeFailure.Message, scopeInner.ToString(), scopeFailure.ToString(),
				cleanupInner.Message, cleanupFailure.Message, cleanupInner.ToString(), cleanupFailure.ToString());
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void LogAndMultipleCleanupFaultsAggregateOnceAfterReverseUnwind()
		{
			const string pathSentinel = @"C:\synthetic\log-cleanup\private.log";
			const string payloadSentinel = "synthetic-log-cleanup-payload-should-not-leak";
			const string dataKeySentinel = "synthetic-log-cleanup-key-should-not-leak";
			var logInner = new InvalidOperationException("synthetic log inner cause " + pathSentinel);
			logInner.Data[dataKeySentinel] = payloadSentinel;
			var logFailure = new InvalidOperationException("foreign log cause " + pathSentinel + " " + payloadSentinel, logInner);
			logFailure.Data[dataKeySentinel] = payloadSentinel;
			var cleanupInner = new InvalidOperationException("synthetic cleanup inner cause " + pathSentinel);
			cleanupInner.Data[dataKeySentinel] = payloadSentinel;
			var cleanupFailure = new InvalidOperationException("foreign cleanup cause " + pathSentinel + " " + payloadSentinel, cleanupInner);
			cleanupFailure.Data[dataKeySentinel] = payloadSentinel;
			var logger = new RecordingMicrosoftLogger();
			logger.BeginScopeHandler = (state, index) => new TrackingScopeHandle("scope-" + index, logger.DisposeOrder, cleanupFailure);
			logger.OnLog = record => { throw logFailure; };
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
			var priorError = Console.Error;
			LogDispatchException failure = null;
			string diagnostic;
			using (RegisterOrdinary(destination, Settings()))
			using (var error = new StringWriter())
			{
				try
				{
					Console.SetError(error);
					using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
					using (NativeLogger.BeginScope(null, new KeyValuePair<string, object>[0]))
					{
						failure = Should.Throw<LogDispatchException>(() => NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, payloadSentinel, (Exception)null));
						failure.Report.Failures.ShouldHaveSingleItem();
						failure.Report.Failures[0].Stage.ShouldBe(LogFailureStage.Output);
						failure.Report.CoreCaptureFailureCount.ShouldBe(0);
						failure.Report.OverflowCount.ShouldBe(0);
						failure.InnerException.ShouldBeNull();
					}
				}
				finally { Console.SetError(priorError); }
				diagnostic = error.ToString();
			}
			logger.DisposeOrder.ShouldBe(new[] { "scope-2", "scope-1" });
			logger.Records.ShouldHaveSingleItem();
			AssertSafeOutputReport(failure, diagnostic, pathSentinel, payloadSentinel, dataKeySentinel,
				logInner.Message, logFailure.Message, logInner.ToString(), logFailure.ToString(),
				cleanupInner.Message, cleanupFailure.Message, cleanupInner.ToString(), cleanupFailure.ToString());
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void FreshIdenticalTextCallDuringIsEnabledRemainsIndependentlyEligible()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
			var nested = false;
			logger.OnIsEnabled = level =>
			{
				if (!nested)
				{
					nested = true;
					NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "same text", (Exception)null);
				}
				return true;
			};
			using (RegisterOrdinary(destination, Settings()))
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "same text", (Exception)null);
			factory.CreateCalls.ShouldBe(2);
			logger.IsEnabledCalls.ShouldBe(2);
			logger.Records.Count.ShouldBe(2);
			logger.Records.Select(record => record.FormattedMessage).ShouldBe(new[] { "same text", "same text" });
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ControlledProviderRoundTripPreservesOriginalTypedEntryAndSuppressesVisitedReturn()
		{
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			try
			{
				var inboundLogger = InboundTestSupport.CreateLogger(provider, "controlled-category");
				var forwardingLogger = new ForwardingRecordingLogger(inboundLogger, "controlled-category");
				var destination = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, forwardingLogger);
				var observer = new InboundCaptureDestination<LogBridgeEntry>(LogLevels.Trace);
				var sourceState = new List<KeyValuePair<string, object>>
				{
					new KeyValuePair<string, object>("duplicate", 1), new KeyValuePair<string, object>("duplicate", 2)
				};
				var sourceException = new InvalidOperationException("controlled source exception");
				var producerCalls = 0;
				Func<List<KeyValuePair<string, object>>, Exception, string> producer = (state, exception) =>
				{
					producerCalls++;
					state.ShouldBeSameAs(sourceState);
					exception.ShouldBeSameAs(sourceException);
					return "captured original text";
				};
				using (RegisterExact<LogBridgeEntry>(destination, Settings()))
				using (RegisterExact<LogBridgeEntry>(observer, Settings()))
				{
					var source = InboundTestSupport.CreateLogger(provider, "source category \t");
					source.Log(LogLevel.Warning, new EventId(-17, "source-event"), sourceState, sourceException, producer);
				}
				producerCalls.ShouldBe(1);
				observer.Records.ShouldHaveSingleItem();
				forwardingLogger.FixedCategory.ShouldBe("controlled-category");
				forwardingLogger.IsEnabledCalls.ShouldBe(1);
				forwardingLogger.Records.ShouldHaveSingleItem();
				var outboundCall = forwardingLogger.Records.Single();
				outboundCall.Level.ShouldBe(LogLevel.Warning);
				outboundCall.EventId.ShouldBe(new EventId(-17, "source-event"));
				outboundCall.Exception.ShouldBeSameAs(sourceException);
				outboundCall.FormattedMessage.ShouldBe("captured original text");
				outboundCall.RepeatedFormattedMessage.ShouldBe("captured original text");
				outboundCall.Formatter.ShouldNotBeSameAs(producer);
				outboundCall.FormatterInvoker(outboundCall.State, outboundCall.Exception).ShouldBe("captured original text");
				producerCalls.ShouldBe(1, "transport formatter calls reuse captured text rather than invoking the producer");
				var outboundEnvelope = Envelope(outboundCall.State);
				outboundEnvelope.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.Entry", "ProphetsWay.NativeContexts", "ProphetsWay.ExternalScopes" });
				var outboundEntry = outboundEnvelope[0].Value;
				var captured = observer.Records.Single().Metadata;
				outboundEntry.ShouldBeSameAs(captured);
				Read<object>(outboundEntry, "State").ShouldBeSameAs(sourceState);
				Read<object>(outboundEntry, "OriginalState").ShouldBeSameAs(sourceState);
				Read<Type>(outboundEntry, "StateType").ShouldBe(sourceState.GetType());
				Read<Delegate>(outboundEntry, "OriginalProducerFormatter").ShouldBeSameAs(producer);
				Read<string>(outboundEntry, "FormattedMessage").ShouldBe("captured original text");
				Read<string>(outboundEntry, "CategoryName").ShouldBe("source category \t");
				Read<Exception>(outboundEntry, "Exception").ShouldBeSameAs(sourceException);
				Read<LogLevels>(outboundEntry, "OriginalLevel").ShouldBe(LogLevels.WarningOnly);
				var eventId = Read<EventId?>(outboundEntry, "EventId");
				eventId.HasValue.ShouldBeTrue();
				eventId.Value.ShouldBe(new EventId(-17, "source-event"));
				Read<ReadOnlyCollection<KeyValuePair<string, object>>>(outboundEntry, "Properties").ToArray().ShouldBe(sourceState.ToArray());
				((ReadOnlyCollection<LogContext>)outboundEnvelope[1].Value).ShouldHaveSingleItem();
				((ReadOnlyCollection<LogBridgeScope>)outboundEnvelope[2].Value).ShouldBeEmpty();
			}
			finally { InboundTestSupport.DisposeProvider(provider); }
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ControlledProviderSharedCaptureFailureKeepsOneDefaultTreatedCompletion()
		{
			MapperProbe.Reset();
			MapperProbe.Entry = (category, level, eventId, state, exception, properties) => null;
			MapperProbe.Scope = (state, properties) => throw new InvalidOperationException("synthetic controlled scope capture failure");
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"), InboundTestSupport.CreateMapper());
			try
			{
				var providerLogger = InboundTestSupport.CreateLogger(provider, "controlled-capture-failure");
				var forwarding = new ForwardingRecordingLogger(providerLogger, "controlled-capture-failure");
				var destination = new MicrosoftLoggerDestination(LogLevels.Trace, forwarding);
				var capture = new InboundCaptureDestination<LogBridgeEntry>(LogLevels.Trace);
				var scopeState = new List<KeyValuePair<string, object>>
				{
					new KeyValuePair<string, object>("controlled", "synthetic scope")
				};
				var reports = new List<LogFailureReport>();
				Action<LogFailureReport> observer = observedReport => reports.Add(observedReport);
				var priorError = Console.Error;
				try
				{
					using (RegisterOrdinary(destination, Settings()))
					using (RegisterExact<LogBridgeEntry>(capture, Settings()))
					using (providerLogger.BeginScope(scopeState))
					using (var error = new StringWriter())
					{
						NativeLogger.DispatchFailed += observer;
						try
						{
							Console.SetError(error);
							NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "controlled capture failure", (Exception)null);
						}
						finally
						{
							NativeLogger.DispatchFailed -= observer;
							Console.SetError(priorError);
						}
					}
				}
				finally { Console.SetError(priorError); }

				forwarding.Records.ShouldNotBeEmpty("the outbound exporter must reach the controlled provider");
				MapperProbe.ScopeCalls.ShouldBeGreaterThan(0, "the deterministic mapper failure must be reached");
				capture.Records.ShouldBeEmpty("shared capture failure withholds the incomplete entry");
				reports.ShouldHaveSingleItem("the controlled failure has one originating completion/notification");
				var report = reports.Single();
				report.CoreCaptureFailureCount.ShouldBe(1);
				report.Failures.ShouldBeEmpty("a default-treated capture result is not exporter Output");
				report.OverflowCount.ShouldBe(0);
				((long)report.CoreCaptureFailureCount + report.Failures.Count + report.OverflowCount).ShouldBe(1L);
				_environment.AssertNoAutomaticOutput();
			}
			finally
			{
				InboundTestSupport.DisposeProvider(provider);
				MapperProbe.Reset();
			}
		}

		[Fact]
		public void ControlledPropagationStateIsRestoredAfterFailedCallbackBeforeFreshIdenticalCall()
		{
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			var providerLogger = InboundTestSupport.CreateLogger(provider, "restore-controlled-state");
			var forwarding = new ForwardingRecordingLogger(providerLogger, "restore-controlled-state");
			var root = new MicrosoftLoggerDestination(LogLevels.Trace, forwarding);
			var successfulLogger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(successfulLogger);
			var factoryAttempts = 0;
			factory.OnCreate = category =>
			{
				if (Interlocked.Increment(ref factoryAttempts) == 1)
					throw new InvalidOperationException("synthetic controlled callback failure");
				return successfulLogger;
			};
			var nested = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, factory);
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => reports.Add(report);
			var priorError = Console.Error;
			try
			{
				using (RegisterOrdinary(root, Settings()))
				using (RegisterExact<LogBridgeEntry>(nested, Settings()))
				using (var error = new StringWriter())
				{
					NativeLogger.DispatchFailed += observer;
					try
					{
						Console.SetError(error);
						var first = Should.Throw<LogDispatchException>(() => NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "same fresh text", (Exception)null));
						first.Report.Failures.ShouldHaveSingleItem();
						first.Report.Failures[0].Stage.ShouldBe(LogFailureStage.Output);
						first.InnerException.ShouldBeNull();
						NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "same fresh text", (Exception)null);
					}
					finally
					{
						NativeLogger.DispatchFailed -= observer;
						Console.SetError(priorError);
					}
				}
				factory.CreateCalls.ShouldBe(2);
				forwarding.Records.Count.ShouldBe(2);
				successfulLogger.Records.ShouldHaveSingleItem();
				reports.ShouldHaveSingleItem("the failed controlled callback is reported once; the fresh call succeeds independently");
				reports.Single().Failures.ShouldHaveSingleItem();
				reports.Single().Failures[0].Stage.ShouldBe(LogFailureStage.Output);
			}
			finally { InboundTestSupport.DisposeProvider(provider); }
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void DirectCallsPreserveCurrentFramesUseFreshContextsAndRejectNoncurrentInputsBeforeCallbacks()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
			LogContext retained;
			using (RegisterOrdinary(destination, Settings()))
			using (NativeLogger.BeginScope(new LogAnnotations(new[] { new SensitivityLabel("direct-frame") })))
			{
				NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "capture current context", (Exception)null);
				retained = ((ReadOnlyCollection<LogContext>)Envelope(logger.Records[0].State)[1].Value).Single();
				var directCallStartedUtc = DateTimeOffset.UtcNow;
				destination.LogWithContext(retained, LogLevels.InformationOnly, "accepted current context");
				var directCallReturnedUtc = DateTimeOffset.UtcNow;
				var directContexts = (ReadOnlyCollection<LogContext>)Envelope(logger.Records[1].State)[1].Value;
				directContexts.ShouldHaveSingleItem();
				directContexts[0].ShouldNotBeSameAs(retained);
				var freshEventTime = directContexts[0].EventTimestampUtc;
				freshEventTime.Offset.ShouldBe(TimeSpan.Zero);
				(freshEventTime >= directCallStartedUtc && freshEventTime <= directCallReturnedUtc).ShouldBeTrue();
				directContexts[0].Labels.EffectiveLabels.ShouldContain(new SensitivityLabel("direct-frame"));
				directContexts[0].Scopes[0].ShouldBeSameAs(retained.Scopes[0]);
			}
			factory.CreateCalls.ShouldBe(2);
			destination.Log(LogLevels.WarningOnly, "unregistered direct call");
			factory.CreateCalls.ShouldBe(3, "a direct call does not require or discover a registry route");
			Should.Throw<ArgumentOutOfRangeException>(() => destination.Log((LogLevels)64, "invalid direct level")).ParamName.ShouldBe("level");
			Should.Throw<ArgumentNullException>(() => destination.LogWithContext(null, LogLevels.InformationOnly, "null context")).ParamName.ShouldBe("context");
			Should.Throw<ArgumentException>(() => destination.LogWithContext(retained, LogLevels.InformationOnly, "noncurrent context")).ParamName.ShouldBe("context");
			factory.CreateCalls.ShouldBe(3);
			logger.Records.Count.ShouldBe(3);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void DirectCarrierShapedMetadataIsFreshTypedInputNotArchivedContextAuthority()
		{
			MapperProbe.Reset();
			var archived = new LogAnnotations(new[] { new SensitivityLabel("archived-label") });
			MapperProbe.Entry = (category, level, eventId, state, exception, properties) => archived;
			MapperProbe.Scope = (state, properties) => null;
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"), InboundTestSupport.CreateMapper());
			LogBridgeEntry original;
			try
			{
				var captured = new InboundCaptureDestination<LogBridgeEntry>(LogLevels.Trace);
				using (RegisterExact<LogBridgeEntry>(captured, Settings()))
				{
					var source = InboundTestSupport.CreateLogger(provider, "archived category");
					source.Log(LogLevel.Information, default(EventId), "source state", null, (state, exception) => "source text");
				}
				original = (LogBridgeEntry)captured.Records.Single().Metadata;
				Read<LogAnnotations>(original, "Annotations").ShouldBeSameAs(archived);
			}
			finally
			{
				InboundTestSupport.DisposeProvider(provider);
				MapperProbe.Reset();
			}
			var logger = new RecordingMicrosoftLogger();
			var destination = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, logger);
			destination.Log(LogLevels.InformationOnly, original, "fresh direct metadata");
			var exported = Envelope(logger.Records.Single().State)[0].Value;
			Read<object>(exported, "OriginalState").ShouldBeSameAs(original);
			Read<Type>(exported, "StateType").ShouldBe(typeof(LogBridgeEntry));
			Read<LogAnnotations>(exported, "Annotations").ShouldBeNull();
			Read<LogContext>(exported, "NativeContext").Labels.EffectiveLabels.ShouldBeEmpty();
			Read<ReadOnlyCollection<KeyValuePair<string, object>>>(exported, "Properties").ShouldBeEmpty();
			_environment.AssertNoAutomaticOutput();
		}

		[Theory]
		[InlineData("")]
		[InlineData(" \t")]
		public void ExplicitInboundCategoryAndMappedExternalFramesRemainSeparateFromNativeOrigin(string category)
		{
			MapperProbe.Reset();
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var mappedEntry = new LogAnnotations(new[] { new SensitivityLabel("mapped-entry") });
			var mappedScope = new LogAnnotations(new[] { new SensitivityLabel("mapped-external") });
			MapperProbe.Entry = (mappedCategory, level, eventId, state, exception, properties) => mappedEntry;
			MapperProbe.Scope = (state, properties) => mappedScope;
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"), InboundTestSupport.CreateMapper());
			var externalState = new List<KeyValuePair<string, object>>
			{
				new KeyValuePair<string, object>("duplicate", 1), new KeyValuePair<string, object>("duplicate", 2)
			};
			var external = new CountingExternalScopeProvider(new object[] { externalState, externalState });
			var destination = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, factory);
			try
			{
				InboundTestSupport.SetScopeProvider(provider, external);
				var policy = new DestinationLabelPolicy(LabelFilterMode.AllowOnly, new[] { new SensitivityLabel("mapped-entry"), new SensitivityLabel("mapped-external") });
				using (RegisterExact<LogBridgeEntry>(destination, Settings(LogLevels.Trace, true, policy)))
				{
					var source = InboundTestSupport.CreateLogger(provider, category);
					source.Log(LogLevel.Information, default(EventId), "state", null, (state, exception) => "external text");
				}
				factory.Categories.ShouldBe(new[] { category });
				logger.Records.ShouldHaveSingleItem();
				var eventEnvelope = Envelope(logger.Records.Single().State);
				eventEnvelope.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.Entry", "ProphetsWay.NativeContexts", "ProphetsWay.ExternalScopes" });
				var entry = eventEnvelope[0].Value;
				Read<string>(entry, "CategoryName").ShouldBe(category);
				Read<LogAnnotations>(entry, "Annotations").ShouldBeSameAs(mappedEntry);
				var externalFrames = (ReadOnlyCollection<LogBridgeScope>)eventEnvelope[2].Value;
				externalFrames.Count.ShouldBe(2);
				externalFrames[0].ShouldNotBeSameAs(externalFrames[1]);
				externalFrames[0].State.ShouldBeSameAs(externalState);
				externalFrames[1].State.ShouldBeSameAs(externalState);
				externalFrames[0].Properties.ToArray().ShouldBe(externalState.ToArray());
				externalFrames[0].Annotations.ShouldBeSameAs(mappedScope);
				Read<ReadOnlyCollection<LogBridgeScope>>(entry, "Scopes").Count.ShouldBe(2);
				Read<ReadOnlyCollection<LogBridgeScope>>(entry, "Scopes")[0].ShouldBeSameAs(externalFrames[0]);
				Read<ReadOnlyCollection<LogBridgeScope>>(entry, "Scopes")[1].ShouldBeSameAs(externalFrames[1]);
				logger.BeginScopeStates.Count.ShouldBe(2);
				Envelope(logger.BeginScopeStates[0]).Single().Value.ShouldBeSameAs(externalFrames[0]);
				Envelope(logger.BeginScopeStates[1]).Single().Value.ShouldBeSameAs(externalFrames[1]);
				var contexts = (ReadOnlyCollection<LogContext>)eventEnvelope[1].Value;
				contexts.ShouldHaveSingleItem();
				contexts[0].Labels.EntryAnnotations.ShouldBeNull();
				contexts[0].Labels.Origins.Any(origin => origin.Label.Equals(mappedEntry.LabelOccurrences[0])).ShouldBeFalse();
				contexts[0].Labels.EffectiveLabels.ShouldNotContain(mappedEntry.LabelOccurrences[0]);
				Read<LogContext>(entry, "NativeContext").Labels.ScopeAnnotations.ShouldBeEmpty();
				external.ForEachCalls.ShouldBe(1);
			}
			finally
			{
				InboundTestSupport.DisposeProvider(provider);
				MapperProbe.Reset();
			}
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ControlledTransferCombinesSourceHostAndExternalLabelsBeforeWithholdingAndPreservesScopeDomains()
		{
			MapperProbe.Reset();
			var sourceScopeLabel = new SensitivityLabel("controlled-source-scope");
			var sourceEntryLabel = new SensitivityLabel("controlled-source-entry");
			var hostOnlyLabel = new SensitivityLabel("controlled-host-only");
			var externalLabel = new SensitivityLabel("controlled-mapped-external");
			var hostExternalState = new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("host", "existing") };
			MapperProbe.Entry = (category, level, eventId, state, exception, properties) => null;
			MapperProbe.Scope = (state, properties) => ReferenceEquals(state, hostExternalState)
				? new LogAnnotations(new[] { externalLabel }) : null;
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"), InboundTestSupport.CreateMapper());
			var externalScopes = new DynamicTrackingExternalScopeProvider();
			try
			{
				InboundTestSupport.SetScopeProvider(provider, externalScopes);
				var providerLogger = InboundTestSupport.CreateLogger(provider, "controlled-host-category");
				var forwarding = new ForwardingRecordingLogger(providerLogger, "controlled-host-category");
				LogScopeHandle hostNativeHandle = null;
				forwarding.IsEnabledCallback = level =>
				{
					hostNativeHandle = NativeLogger.BeginScope(new LogAnnotations(new[] { hostOnlyLabel }),
						new[] { new KeyValuePair<string, object>("host-native", "newly observed") });
					return providerLogger.IsEnabled(level);
				};
				forwarding.AfterLog = () =>
				{
					if (hostNativeHandle != null) { hostNativeHandle.Dispose(); hostNativeHandle = null; }
				};
				var root = new MicrosoftLoggerDestination(LogLevels.Trace, forwarding);
				var positiveLogger = new RecordingMicrosoftLogger();
				var positive = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, positiveLogger);
				var deniedLogger = new RecordingMicrosoftLogger();
				var denied = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, deniedLogger);
				var entryDeniedLogger = new RecordingMicrosoftLogger();
				var entryDeniedFactory = new RecordingMicrosoftLoggerFactory(entryDeniedLogger);
				var entryDenied = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, entryDeniedFactory);
				var externalDeniedLogger = new RecordingMicrosoftLogger();
				var externalDenied = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, externalDeniedLogger);
				var observer = new InboundCaptureDestination<LogBridgeEntry>(LogLevels.Trace);
				var deniedPolicy = new DestinationLabelPolicy(LabelFilterMode.AllowOnly,
					new[] { sourceScopeLabel, sourceEntryLabel, externalLabel });
				var entryDeniedPolicy = new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { sourceEntryLabel });
				var externalDeniedPolicy = new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { externalLabel });
				using (RegisterOrdinary(root, Settings()))
				using (RegisterExact<LogBridgeEntry>(positive, Settings()))
				using (RegisterExact<LogBridgeEntry>(denied, Settings(LogLevels.Trace, true, deniedPolicy)))
				using (RegisterExact<LogBridgeEntry>(entryDenied, Settings(LogLevels.Trace, true, entryDeniedPolicy)))
				using (RegisterExact<LogBridgeEntry>(externalDenied, Settings(LogLevels.Trace, true, externalDeniedPolicy)))
				using (RegisterExact<LogBridgeEntry>(observer, Settings()))
				using (providerLogger.BeginScope(hostExternalState))
				using (NativeLogger.BeginScope(new LogAnnotations(new[] { sourceScopeLabel }),
					new[] { new KeyValuePair<string, object>("source-native", "original") }))
				{
					NativeLogger.LogAnnotated(new LogAnnotations(new[] { sourceEntryLabel }), LogLevels.InformationOnly, "controlled union payload", (Exception)null);
					externalScopes.Snapshot().ShouldBe(new object[] { hostExternalState },
						"outbound scope cleanup must leave the pre-existing Microsoft host scope active");
				}
				forwarding.IsEnabledCalls.ShouldBe(1);
				forwarding.Records.ShouldHaveSingleItem();
				positiveLogger.IsEnabledCalls.ShouldBe(1);
				positiveLogger.Records.ShouldHaveSingleItem();
				deniedLogger.IsEnabledCalls.ShouldBe(0);
				deniedLogger.BeginScopeStates.ShouldBeEmpty();
				deniedLogger.Records.ShouldBeEmpty();
				entryDeniedFactory.CreateCalls.ShouldBe(0);
				entryDeniedLogger.IsEnabledCalls.ShouldBe(0);
				entryDeniedLogger.BeginScopeStates.ShouldBeEmpty();
				entryDeniedLogger.Records.ShouldBeEmpty();
				externalDeniedLogger.IsEnabledCalls.ShouldBe(0);
				externalDeniedLogger.BeginScopeStates.ShouldBeEmpty();
				externalDeniedLogger.Records.ShouldBeEmpty();
				observer.Records.ShouldHaveSingleItem();
				MapperProbe.ScopeCalls.ShouldBe(1, "the replayed native frame is retained by private correspondence, not remapped as external input");
				var outerEnvelope = Envelope(forwarding.Records.Single().State);
				var sourceEntry = outerEnvelope[0].Value;
				var sourceContext = Read<LogContext>(sourceEntry, "NativeContext");
				var observerRecord = observer.Records.Single();
				observerRecord.Context.Labels.EffectiveLabels.ShouldContain(hostOnlyLabel);
				observerRecord.Context.Labels.Origins.Any(origin => origin.Label.Equals(hostOnlyLabel) && origin.ScopeIndex == 1).ShouldBeTrue();
				observerRecord.Context.Labels.EffectiveLabels.ShouldContain(sourceScopeLabel);
				observerRecord.Context.Labels.Origins.Any(origin => origin.Label.Equals(sourceScopeLabel) && origin.ScopeIndex == 0).ShouldBeTrue();
				var capturedEntry = observerRecord.Metadata;
				capturedEntry.ShouldBeSameAs(sourceEntry);
				Read<LogContext>(capturedEntry, "NativeContext").ShouldBeSameAs(sourceContext);
				Read<LogAnnotations>(capturedEntry, "Annotations").ShouldBeSameAs(Read<LogAnnotations>(sourceEntry, "Annotations"));
				var outputEnvelope = Envelope(positiveLogger.Records.Single().State);
				outputEnvelope.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.Entry", "ProphetsWay.NativeContexts", "ProphetsWay.ExternalScopes" });
				outputEnvelope[0].Value.ShouldBeSameAs(capturedEntry);
				var contexts = (ReadOnlyCollection<LogContext>)outputEnvelope[1].Value;
				contexts.Count.ShouldBe(2);
				contexts[0].ShouldBeSameAs(sourceContext);
				contexts[0].Labels.EffectiveLabels.ShouldContain(sourceEntryLabel);
				contexts[0].Labels.EffectiveLabels.ShouldContain(sourceScopeLabel);
				contexts[0].Labels.Origins.Any(origin => origin.Label.Equals(sourceScopeLabel) && origin.ScopeIndex == 0).ShouldBeTrue();
				contexts[0].Labels.EffectiveLabels.ShouldNotContain(hostOnlyLabel);
				contexts[0].Labels.EffectiveLabels.ShouldNotContain(externalLabel);
				contexts[1].Labels.EffectiveLabels.ShouldContain(sourceScopeLabel);
				contexts[1].Labels.EffectiveLabels.ShouldContain(hostOnlyLabel);
				contexts[1].Labels.EffectiveLabels.ShouldNotContain(externalLabel);
				contexts[1].Labels.Origins.Any(origin => origin.Label.Equals(hostOnlyLabel) && origin.ScopeIndex == 1).ShouldBeTrue();
				contexts[1].Scopes.Count.ShouldBe(2);
				var outputExternalFrames = (ReadOnlyCollection<LogBridgeScope>)outputEnvelope[2].Value;
				var outputHostFrames = outputExternalFrames.Where(frame => ReferenceEquals(frame.State, hostExternalState)).ToArray();
				outputHostFrames.ShouldHaveSingleItem();
				outputHostFrames[0].Annotations.LabelOccurrences.ShouldContain(externalLabel);
				var outputScopeKeys = positiveLogger.BeginScopeStates.SelectMany(state => Envelope(state).Select(pair => pair.Key)).ToArray();
				outputScopeKeys.ShouldContain("ProphetsWay.NativeScope");
				outputScopeKeys.ShouldContain("ProphetsWay.ExternalScope");
				var firstExternal = Array.IndexOf(outputScopeKeys, "ProphetsWay.ExternalScope");
				outputScopeKeys.Take(firstExternal).ShouldAllBe(key => key == "ProphetsWay.NativeScope");
				outputScopeKeys.Skip(firstExternal).ShouldAllBe(key => key == "ProphetsWay.ExternalScope");
				var sourceFrame = sourceContext.Scopes.Single();
				var exportedNativeFrames = positiveLogger.BeginScopeStates.Select(state => Envelope(state).Single())
					.Where(pair => pair.Key == "ProphetsWay.NativeScope").Select(pair => pair.Value).ToArray();
				exportedNativeFrames.Count(frame => ReferenceEquals(frame, sourceFrame)).ShouldBe(1);
				var hostNativeFrame = contexts[1].Scopes.Single(frame => frame.Annotations != null && frame.Annotations.LabelOccurrences.Contains(hostOnlyLabel));
				exportedNativeFrames.Count(frame => ReferenceEquals(frame, hostNativeFrame)).ShouldBe(1);
				var exportedHostFrames = positiveLogger.BeginScopeStates.Select(state => Envelope(state).Single())
					.Where(pair => pair.Key == "ProphetsWay.ExternalScope").Select(pair => (LogBridgeScope)pair.Value)
					.Where(frame => ReferenceEquals(frame.State, hostExternalState)).ToArray();
				exportedHostFrames.ShouldHaveSingleItem();
				_environment.AssertNoAutomaticOutput();
			}
			finally
			{
				InboundTestSupport.DisposeProvider(provider);
				MapperProbe.Reset();
			}
		}

		[Fact]
		public void ControlledTransferRetainsDistinctCurrentNativeContextWhenFramesAndLabelsAreAlreadyRepresented()
		{
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			var providerLogger = InboundTestSupport.CreateLogger(provider, "represented-context");
			var forwarding = new ForwardingRecordingLogger(providerLogger, "represented-context");
			var root = new MicrosoftLoggerDestination(LogLevels.Trace, forwarding);
			var outputLogger = new RecordingMicrosoftLogger();
			var output = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, outputLogger);
			var observer = new InboundCaptureDestination<LogBridgeEntry>(LogLevels.Trace);
			var sharedLabel = new SensitivityLabel("already-represented-context");
			var frameProperties = new[] { new KeyValuePair<string, object>("shared-frame", "same frame") };

			try
			{
				using (RegisterOrdinary(root, Settings()))
				using (RegisterExact<LogBridgeEntry>(output, Settings()))
				using (RegisterExact<LogBridgeEntry>(observer, Settings()))
				using (NativeLogger.BeginScope(new LogAnnotations(new[] { sharedLabel }), frameProperties))
					NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "represented native context", (Exception)null);

				forwarding.Records.ShouldHaveSingleItem();
				outputLogger.Records.ShouldHaveSingleItem();
				observer.Records.ShouldHaveSingleItem();
				var sourceEntry = Envelope(forwarding.Records.Single().State)[0].Value;
				var sourceContext = Read<LogContext>(sourceEntry, "NativeContext");
				var currentContext = observer.Records.Single().Context;
				currentContext.ShouldNotBeSameAs(sourceContext);
				sourceContext.Labels.EffectiveLabels.Select(label => label.Identifier).ShouldBe(new[] { "already-represented-context" });
				currentContext.Labels.EffectiveLabels.Select(label => label.Identifier).ShouldBe(new[] { "already-represented-context" });
				sourceContext.Scopes.ShouldHaveSingleItem();
				currentContext.Scopes.ShouldHaveSingleItem();
				var sharedFrame = sourceContext.Scopes[0];
				currentContext.Scopes[0].ShouldBeSameAs(sharedFrame);

				var outputContexts = (ReadOnlyCollection<LogContext>)Envelope(outputLogger.Records.Single().State)[1].Value;
				outputContexts.Count.ShouldBe(2);
				outputContexts[0].ShouldBeSameAs(sourceContext);
				outputContexts[1].ShouldBeSameAs(currentContext);
				outputContexts[0].ShouldNotBeSameAs(outputContexts[1]);
				outputContexts[0].Scopes.ShouldHaveSingleItem();
				outputContexts[1].Scopes.ShouldHaveSingleItem();
				outputContexts[0].Scopes[0].ShouldBeSameAs(sharedFrame);
				outputContexts[1].Scopes[0].ShouldBeSameAs(sharedFrame);
			}
			finally
			{
				InboundTestSupport.DisposeProvider(provider);
			}
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void FailedSharedStructuredCaptureWithholdsTheEntryAndMicrosoftCallbacks()
		{
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			var destination = new GenericMicrosoftLoggerDestination<IEnumerable<KeyValuePair<string, object>>>(LogLevels.Trace, factory);
			var independent = new CountingExactDestination<IEnumerable<KeyValuePair<string, object>>>();
			var sequence = new DisposalFailureSequence();
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => reports.Add(report);
			var priorError = Console.Error;
			using (RegisterExact<IEnumerable<KeyValuePair<string, object>>>(destination, Settings()))
			using (RegisterExact<IEnumerable<KeyValuePair<string, object>>>(independent, Settings()))
			using (var error = new StringWriter())
			{
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(error);
					NativeLogger.LogAnnotated<IEnumerable<KeyValuePair<string, object>>>(null,
						LogLevels.InformationOnly, sequence, "capture failure");
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
				}
			}
			reports.ShouldHaveSingleItem();
			reports.Single().CoreCaptureFailureCount.ShouldBe(1);
			reports.Single().Failures.ShouldBeEmpty();
			reports.Single().OverflowCount.ShouldBe(0);
			sequence.DisposeCalls.ShouldBe(1);
			independent.Calls.ShouldBe(1, "an independent native exact-T recipient still receives its call");
			independent.LastMetadata.ShouldBeSameAs(sequence);
			independent.LastMessage.ShouldBe("capture failure");
			factory.CreateCalls.ShouldBe(0);
			logger.IsEnabledCalls.ShouldBe(0);
			logger.Records.ShouldBeEmpty();
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void TransportCollectionOwnsOrderedMembershipAndImplementsBothEnumeratorProtocols()
		{
			var nested = new object();
			var source = new List<KeyValuePair<string, object>>
			{
				new KeyValuePair<string, object>("same", nested),
				new KeyValuePair<string, object>("same", null), default(KeyValuePair<string, object>)
			};
			var transport = CreateTransport(source);
			var type = transport.GetType();
			type.IsNotPublic.ShouldBeTrue();
			type.IsSealed.ShouldBeTrue();
			type.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
			transport.ShouldBeAssignableTo<IReadOnlyList<KeyValuePair<string, object>>>();
			transport.Count.ShouldBe(3);
			transport[0].Key.ShouldBe("same");
			transport[0].Value.ShouldBeSameAs(nested);
			transport[1].Key.ShouldBe("same");
			transport[1].Value.ShouldBeNull();
			transport[2].ShouldBe(default(KeyValuePair<string, object>));
			source[0] = new KeyValuePair<string, object>("changed", new object());
			transport[0].Key.ShouldBe("same");
			transport[0].Value.ShouldBeSameAs(nested);
			Should.Throw<ArgumentOutOfRangeException>(() => { var ignored = transport[-1]; }).ParamName.ShouldBe("index");
			Should.Throw<ArgumentOutOfRangeException>(() => { var ignored = transport[transport.Count]; }).ParamName.ShouldBe("index");
			var generic = transport.GetEnumerator();
			var genericPairs = new List<KeyValuePair<string, object>>();
			while (generic.MoveNext()) genericPairs.Add(generic.Current);
			genericPairs.ShouldBe(transport.ToArray());
			generic.MoveNext().ShouldBeFalse();
			var nongeneric = ((IEnumerable)transport).GetEnumerator();
			var nongenericPairs = new List<object>();
			while (nongeneric.MoveNext()) nongenericPairs.Add(nongeneric.Current);
			nongenericPairs.Cast<KeyValuePair<string, object>>().ShouldBe(transport.ToArray());
			CreateTransport(new KeyValuePair<string, object>[0]).Count.ShouldBe(0);
			Should.Throw<ArgumentOutOfRangeException>(() => { var ignored = CreateTransport(new KeyValuePair<string, object>[0])[0]; }).ParamName.ShouldBe("index");
			Should.Throw<ArgumentNullException>(() => CreateTransport(null)).ParamName.ShouldBe("items");
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void TransportDoesNotPublishPrefixWhenEnumeratorDisposalFails()
		{
			AssertTransportConstructionFailsWithoutPublishing(new FaultingTransportSequence(TransportFailurePoint.Dispose));
		}

		[Fact]
		public void TransportDoesNotPublishWhenEnumeratorAcquisitionFails()
		{
			AssertTransportConstructionFailsWithoutPublishing(new FaultingTransportSequence(TransportFailurePoint.GetEnumerator));
		}

		[Fact]
		public void TransportDoesNotPublishPrefixWhenMoveNextFailsAfterAnElement()
		{
			AssertTransportConstructionFailsWithoutPublishing(new FaultingTransportSequence(TransportFailurePoint.MoveNextAfterPrefix));
		}

		[Fact]
		public void TransportDoesNotPublishPrefixWhenCurrentFailsAfterAnElement()
		{
			AssertTransportConstructionFailsWithoutPublishing(new FaultingTransportSequence(TransportFailurePoint.CurrentAfterPrefix));
		}

		[Fact]
		public void CallbackRegistrationChangesApplyToLaterCapturesNotTheAdmittedMembership()
		{
			var logger = new RecordingMicrosoftLogger();
			var destination = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
			var later = new CountingOrdinaryDestination();
			var added = false;
			logger.OnIsEnabled = level =>
			{
				if (!added)
				{
					added = true;
					NativeLogger.AddDestination(later, Settings());
				}
				return true;
			};
			using (RegisterOrdinary(destination, Settings()))
			{
				try
				{
					NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "captured membership", (Exception)null);
					later.Calls.ShouldBe(0);
					logger.OnIsEnabled = null;
					NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "next membership", (Exception)null);
					later.Calls.ShouldBe(1);
				}
				finally { NativeLogger.RemoveDestination(later); }
			}
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ConcurrentCallsKeepTheirOwnCapturedFactsWithoutAssumingCallbackOrder()
		{
			using (var entered = new CountdownEvent(2))
			using (var release = new ManualResetEventSlim(false))
			{
				var logger = new ConcurrentRecordingLogger(entered, release);
				var destination = new MicrosoftLoggerDestination(LogLevels.Trace, logger);
				Task first = null;
				Task second = null;
				bool bothReachedEligibility;
				using (RegisterOrdinary(destination, Settings()))
				{
					first = Task.Run(() =>
					{
						using (NativeLogger.BeginScope(new LogAnnotations(new[] { new SensitivityLabel("parallel-a") })))
							NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "parallel-a", (Exception)null);
					});
					second = Task.Run(() =>
					{
						using (NativeLogger.BeginScope(new LogAnnotations(new[] { new SensitivityLabel("parallel-b") })))
							NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "parallel-b", (Exception)null);
					});
					try { bothReachedEligibility = entered.Wait(TimeSpan.FromSeconds(15)); }
					finally { release.Set(); }
					Task.WaitAll(new[] { first, second }, TimeSpan.FromSeconds(15)).ShouldBeTrue();
					bothReachedEligibility.ShouldBeTrue();
				}
				logger.IsEnabledCalls.ShouldBe(2);
				var records = logger.Records;
				records.Length.ShouldBe(2);
				records.Select(record => record.FormattedMessage).OrderBy(message => message).ShouldBe(new[] { "parallel-a", "parallel-b" });
				foreach (var record in records)
				{
					var entry = Envelope(record.State)[0].Value;
					Read<string>(entry, "OriginalState").ShouldBe(record.FormattedMessage);
					var context = Read<LogContext>(entry, "NativeContext");
					context.Labels.EffectiveLabels.Select(label => label.Identifier).ShouldContain(record.FormattedMessage);
				}
			}
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void RepairedCallWidePositionsUseRootFirstCompleteBlocksAndFailureEncounterOrder()
		{
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			var providerLogger = InboundTestSupport.CreateLogger(provider, "call-wide");
			var factory = new RecordingMicrosoftLoggerFactory(providerLogger);
			var outbound = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
			var disabled = new CountingOrdinaryDestination();
			var rootFailure = new ThrowingOrdinaryDestination(new InvalidOperationException("root output"));
			var exactOutbound = new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, providerLogger);
			var exactFailure = new ThrowingExactDestination<LogBridgeEntry>(new InvalidOperationException("nested output"));
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = observedReport => reports.Add(observedReport);
			var priorError = Console.Error;
			using (RegisterOrdinary(disabled, Settings(LogLevels.Trace, false)))
			using (RegisterOrdinary(outbound, Settings()))
			using (RegisterOrdinary(rootFailure, Settings()))
			using (RegisterExact<LogBridgeEntry>(exactOutbound, Settings()))
			using (RegisterExact<LogBridgeEntry>(exactFailure, Settings()))
			using (var error = new StringWriter())
			{
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(error);
					Should.Throw<LogDispatchException>(() => NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "position oracle", (Exception)null));
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
					InboundTestSupport.DisposeProvider(provider);
				}
			}
			reports.ShouldHaveSingleItem();
			var report = reports.Single();
			report.Failures.Select(failure => failure.RegistrationId).ShouldBe(new[] { 5, 3 });
			report.Failures.Select(failure => failure.Stage).ShouldBe(new[] { LogFailureStage.Output, LogFailureStage.Output });
			report.CoreCaptureFailureCount.ShouldBe(0);
			report.OverflowCount.ShouldBe(0);
			((long)report.CoreCaptureFailureCount + report.Failures.Count + report.OverflowCount).ShouldBe(2L);
			_environment.AssertNoAutomaticOutput();
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void FactoryThrowAndNullLoggerResultAreOneSafeOutputFailure(bool returnNull)
		{
			const string pathSentinel = @"C:\synthetic\factory\private.log";
			const string payloadSentinel = "synthetic-factory-payload-should-not-leak";
			const string dataKeySentinel = "synthetic-factory-key-should-not-leak";
			var inner = new InvalidOperationException("synthetic factory inner cause " + pathSentinel);
			inner.Data[dataKeySentinel] = payloadSentinel;
			var factoryFailure = new InvalidOperationException("foreign factory cause " + pathSentinel + " " + payloadSentinel, inner);
			factoryFailure.Data[dataKeySentinel] = payloadSentinel;
			var logger = new RecordingMicrosoftLogger();
			var factory = new RecordingMicrosoftLoggerFactory(logger);
			factory.OnCreate = category => { if (returnNull) return null; throw factoryFailure; };
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => reports.Add(report);
			var priorError = Console.Error;
			LogDispatchException failure = null;
			string diagnostic;
			using (RegisterOrdinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory), Settings()))
			using (var error = new StringWriter())
			{
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(error);
					failure = Should.Throw<LogDispatchException>(() => NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, payloadSentinel, (Exception)null));
					failure.Report.Failures.ShouldHaveSingleItem();
					failure.Report.Failures[0].RegistrationId.ShouldBe(1);
					failure.Report.Failures[0].Stage.ShouldBe(LogFailureStage.Output);
					failure.InnerException.ShouldBeNull();
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
				}
				diagnostic = error.ToString();
			}
			reports.ShouldHaveSingleItem();
			factory.CreateCalls.ShouldBe(1);
			logger.IsEnabledCalls.ShouldBe(0);
			logger.BeginScopeStates.ShouldBeEmpty();
			logger.Records.ShouldBeEmpty();
			if (returnNull)
				AssertSafeOutputReport(failure, diagnostic, payloadSentinel);
			else
				AssertSafeOutputReport(failure, diagnostic, pathSentinel, payloadSentinel, dataKeySentinel,
					inner.Message, factoryFailure.Message, inner.ToString(), factoryFailure.ToString());
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ThrowingMicrosoftIsEnabledIsOutputAndSuppressesScopeAndLogCalls()
		{
			const string pathSentinel = @"C:\synthetic\private\should-not-leak.log";
			const string payloadSentinel = "synthetic-payload-should-not-leak";
			const string dataKeySentinel = "synthetic-data-key-should-not-leak";
			var inner = new InvalidOperationException("synthetic inner cause " + pathSentinel);
			inner.Data[dataKeySentinel] = payloadSentinel;
			var rawFailure = new InvalidOperationException("foreign enable failure " + pathSentinel + " " + payloadSentinel, inner);
			rawFailure.Data[dataKeySentinel] = payloadSentinel;
			var logger = new RecordingMicrosoftLogger { OnIsEnabled = level => throw rawFailure };
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => reports.Add(report);
			var priorError = Console.Error;
			LogDispatchException failure = null;
			string diagnostic;
			using (RegisterOrdinary(new MicrosoftLoggerDestination(LogLevels.Trace, logger), Settings()))
			using (var error = new StringWriter())
			{
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(error);
					failure = Should.Throw<LogDispatchException>(() => NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, payloadSentinel, (Exception)null));
					failure.Report.Failures.ShouldHaveSingleItem();
					failure.Report.Failures[0].Stage.ShouldBe(LogFailureStage.Output);
					failure.InnerException.ShouldBeNull();
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
				}
				diagnostic = error.ToString();
			}
			reports.ShouldHaveSingleItem();
			logger.IsEnabledCalls.ShouldBe(1);
			logger.BeginScopeStates.ShouldBeEmpty();
			logger.Records.ShouldBeEmpty();
			AssertSafeOutputReport(failure, diagnostic, pathSentinel, payloadSentinel, dataKeySentinel,
				inner.Message, rawFailure.Message, inner.ToString(), rawFailure.ToString());
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void RepairedCallWidePositionsForExactTypedRootUseWholeRootBlockPrefix()
		{
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			var providerLogger = InboundTestSupport.CreateLogger(provider, "exact-root");
			var disabledRoot = new CountingExactDestination<UnfamiliarState>();
			var rootExporter = new GenericMicrosoftLoggerDestination<UnfamiliarState>(LogLevels.Trace, providerLogger);
			var rootFailure = new ThrowingExactDestination<UnfamiliarState>(new InvalidOperationException("exact root output"));
			var disabledNested = new CountingExactDestination<LogBridgeEntry>();
			var nestedFailure = new ThrowingExactDestination<LogBridgeEntry>(new InvalidOperationException("exact nested output"));
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = observedReport => reports.Add(observedReport);
			var priorError = Console.Error;
			LogDispatchException dispatchFailure = null;
			using (var error = new StringWriter())
			{
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(error);
					using (RegisterExact<UnfamiliarState>(disabledRoot, Settings(LogLevels.Trace, false)))
					using (RegisterExact<UnfamiliarState>(rootExporter, Settings()))
					using (RegisterExact<UnfamiliarState>(rootFailure, Settings()))
					using (RegisterExact<LogBridgeEntry>(disabledNested, Settings(LogLevels.Trace, false)))
					using (RegisterExact<LogBridgeEntry>(nestedFailure, Settings()))
						dispatchFailure = Should.Throw<LogDispatchException>(() =>
							NativeLogger.LogAnnotated<UnfamiliarState>(null, LogLevels.InformationOnly, new UnfamiliarState(), "exact-T positions"));
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
					InboundTestSupport.DisposeProvider(provider);
				}
			}
			reports.ShouldHaveSingleItem();
			var report = reports.Single();
			dispatchFailure.ShouldNotBeNull();
			dispatchFailure.Report.ShouldBeSameAs(report);
			dispatchFailure.InnerException.ShouldBeNull();
			report.Failures.Select(failure => failure.RegistrationId).ShouldBe(new[] { 5, 3 });
			report.Failures.Select(failure => failure.Stage).ShouldBe(new[] { LogFailureStage.Output, LogFailureStage.Output });
			report.CoreCaptureFailureCount.ShouldBe(0);
			report.OverflowCount.ShouldBe(0);
			((long)report.CoreCaptureFailureCount + report.Failures.Count + report.OverflowCount).ShouldBe(2L);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void RepairedCallWidePositionsForSuppliedDirectRootKeepDirectPositionOne()
		{
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			var providerLogger = InboundTestSupport.CreateLogger(provider, "direct-root");
			var directLogger = new ForwardingRecordingLogger(providerLogger, "direct-root");
			var directDestination = new MicrosoftLoggerDestination(LogLevels.Trace, directLogger);
			var disabledNested = new CountingExactDestination<LogBridgeEntry>();
			var nestedFailure = new ThrowingExactDestination<LogBridgeEntry>(new InvalidOperationException("direct nested output"));
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = observedReport => reports.Add(observedReport);
			var priorError = Console.Error;
			using (var error = new StringWriter())
			{
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(error);
					using (RegisterExact<LogBridgeEntry>(disabledNested, Settings(LogLevels.Trace, false)))
					using (RegisterExact<LogBridgeEntry>(nestedFailure, Settings()))
					using (NativeLogger.BeginScope(new LogAnnotations(new[] { new SensitivityLabel("direct-root-frame") })))
					{
						var failure = Should.Throw<LogDispatchException>(() => directDestination.Log(LogLevels.InformationOnly, "direct root position"));
						failure.InnerException.ShouldBeNull();
					}
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
					InboundTestSupport.DisposeProvider(provider);
				}
			}
			directLogger.BeginScopeStates.ShouldHaveSingleItem();
			directLogger.Records.ShouldHaveSingleItem();
			reports.ShouldHaveSingleItem();
			var report = reports.Single();
			report.Failures.Select(failure => failure.RegistrationId).ShouldBe(new[] { 3 });
			report.Failures.Select(failure => failure.Stage).ShouldBe(new[] { LogFailureStage.Output });
			report.CoreCaptureFailureCount.ShouldBe(0);
			report.OverflowCount.ShouldBe(0);
			((long)report.CoreCaptureFailureCount + report.Failures.Count + report.OverflowCount).ShouldBe(1L);
			_environment.AssertNoAutomaticOutput();
		}

		[Fact]
		public void ControlledCallAppliesOneGlobalEightDescriptorBoundInFailureEncounterOrder()
		{
			var provider = InboundTestSupport.CreateProvider(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			var providerLogger = InboundTestSupport.CreateLogger(provider, "global-bound");
			var factory = new RecordingMicrosoftLoggerFactory(providerLogger);
			var rootExporter = new MicrosoftLoggerDestination(LogLevels.Trace, factory);
			var nestedFailure = new ThrowingExactDestination<LogBridgeEntry>(new InvalidOperationException("nested slot"));
			var registrations = new List<IDisposable>();
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = observedReport => reports.Add(observedReport);
			var priorError = Console.Error;
			try
			{
				registrations.Add(RegisterOrdinary(rootExporter, Settings()));
				for (var index = 0; index < 8; index++)
					registrations.Add(RegisterOrdinary(new ThrowingOrdinaryDestination(new InvalidOperationException("root slot")), Settings()));
				using (RegisterExact<LogBridgeEntry>(nestedFailure, Settings()))
				using (var error = new StringWriter())
				{
					NativeLogger.DispatchFailed += observer;
					try
					{
						Console.SetError(error);
						Should.Throw<LogDispatchException>(() => NativeLogger.LogAnnotated(null, LogLevels.InformationOnly, "bounded positions", (Exception)null));
					}
					finally
					{
						NativeLogger.DispatchFailed -= observer;
						Console.SetError(priorError);
					}
				}
			}
			finally
			{
				for (var index = registrations.Count - 1; index >= 0; index--) registrations[index].Dispose();
				InboundTestSupport.DisposeProvider(provider);
			}
			reports.ShouldHaveSingleItem();
			var report = reports.Single();
			report.Failures.Select(failure => failure.RegistrationId).ShouldBe(new[] { 10, 2, 3, 4, 5, 6, 7, 8 });
			report.Failures.ShouldAllBe(failure => failure.Stage == LogFailureStage.Output);
			report.OverflowCount.ShouldBe(1);
			report.CoreCaptureFailureCount.ShouldBe(0);
			((long)report.CoreCaptureFailureCount + report.Failures.Count + report.OverflowCount).ShouldBe(9L);
			_environment.AssertNoAutomaticOutput();
		}

		private static IReadOnlyList<KeyValuePair<string, object>> Envelope(object state)
		{
			state.ShouldNotBeNull();
			state.ShouldBeAssignableTo<IReadOnlyList<KeyValuePair<string, object>>>();
			return (IReadOnlyList<KeyValuePair<string, object>>)state;
		}

		private static T Read<T>(object target, string name)
		{
			var property = target.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			property.ShouldNotBeNull("Expected approved bridge property " + name + ".");
			return (T)property.GetValue(target, null);
		}

		private static void AssertSafeOutputReport(LogDispatchException failure, string diagnostic, params string[] unsafeTexts)
		{
			diagnostic.Length.ShouldBeLessThanOrEqualTo(512);
			var safeTexts = new[] { failure.Message, failure.ToString(), failure.Report.ToString() }
				.Concat(failure.Report.Failures.Select(item => item.ToString()));
			foreach (var unsafeText in unsafeTexts)
			{
				diagnostic.ShouldNotContain(unsafeText);
				foreach (var safeText in safeTexts) safeText.ShouldNotContain(unsafeText);
			}
		}

		private static IReadOnlyList<KeyValuePair<string, object>> CreateTransport(IEnumerable<KeyValuePair<string, object>> items)
		{
			var type = typeof(NativeLogger).Assembly.GetType("ProphetsWay.Utilities.MicrosoftLogTransport", true);
			var constructor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
				new[] { typeof(IEnumerable<KeyValuePair<string, object>>) }, null);
			constructor.ShouldNotBeNull("The approved internal Microsoft transport constructor must exist.");
			return (IReadOnlyList<KeyValuePair<string, object>>)InboundTestSupport.Invoke(constructor, null, new object[] { items });
		}

		private void AssertTransportConstructionFailsWithoutPublishing(FaultingTransportSequence sequence)
		{
			IReadOnlyList<KeyValuePair<string, object>> published = null;
			var failureObserved = false;
			try { published = CreateTransport(sequence); }
			catch (Exception) { failureObserved = true; }
			failureObserved.ShouldBeTrue("sequence access failure must prevent transport construction");
			published.ShouldBeNull("a failing sequence prefix is not published");
			_environment.AssertNoAutomaticOutput();
		}

		private static DestinationRegistrationSettings Settings(LogLevels mask = LogLevels.Trace, bool enabled = true,
			DestinationLabelPolicy policy = null)
		{
			return new DestinationRegistrationSettings(enabled, mask,
				policy ?? new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]));
		}

		private static IDisposable RegisterOrdinary(ILoggingDestination destination, DestinationRegistrationSettings settings)
		{
			NativeLogger.AddDestination(destination, settings);
			return new ActionDisposable(() => NativeLogger.RemoveDestination(destination));
		}

		private static IDisposable RegisterExact<T>(ProphetsWay.Utilities.Generics.ILoggingDestination<T> destination, DestinationRegistrationSettings settings)
		{
			NativeLogger.AddDestination<T>(destination, settings);
			return new ActionDisposable(() => NativeLogger.RemoveDestination<T>(destination));
		}

		private sealed class ActionDisposable : IDisposable
		{
			private readonly Action _action;
			private bool _disposed;
			public ActionDisposable(Action action) { _action = action; }
			public void Dispose()
			{
				if (_disposed) return;
				_disposed = true;
				_action();
			}
		}

		private sealed class RecordingMicrosoftLoggerFactory : ILoggerFactory
		{
			private readonly ILogger _logger;
			public RecordingMicrosoftLoggerFactory(ILogger logger) { _logger = logger; }
			public List<string> Categories { get; } = new List<string>();
			public Func<string, ILogger> OnCreate { get; set; }
			public int CreateCalls { get; private set; }
			public int DisposeCalls { get; private set; }
			public void AddProvider(ILoggerProvider provider) { }
			public ILogger CreateLogger(string categoryName)
			{
				CreateCalls++;
				Categories.Add(categoryName);
				return OnCreate == null ? _logger : OnCreate(categoryName);
			}
			public void Dispose() { DisposeCalls++; }
		}

		private sealed class RecordingMicrosoftLogger : ILogger
		{
			public List<LogRecord> Records { get; } = new List<LogRecord>();
			public List<object> BeginScopeStates { get; } = new List<object>();
			public List<string> DisposeOrder { get; } = new List<string>();
			public Func<LogLevel, bool> OnIsEnabled { get; set; }
			public Func<object, int, IDisposable> BeginScopeHandler { get; set; }
			public Action<LogRecord> OnLog { get; set; }
			public bool IsEnabledResult { get; set; } = true;
			public int IsEnabledCalls { get; private set; }
			public int DisposeCalls { get; private set; }
			public void Dispose() { DisposeCalls++; }
			public IDisposable BeginScope<TState>(TState state)
			{
				object boxed = state;
				BeginScopeStates.Add(boxed);
				return BeginScopeHandler == null
					? new TrackingScopeHandle("scope-" + BeginScopeStates.Count, DisposeOrder)
					: BeginScopeHandler(boxed, BeginScopeStates.Count);
			}
			public bool IsEnabled(LogLevel logLevel)
			{
				IsEnabledCalls++;
				return OnIsEnabled == null ? IsEnabledResult : OnIsEnabled(logLevel);
			}
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
				Func<TState, Exception, string> formatter)
			{
				var record = new LogRecord(logLevel, eventId, state, exception, formatter);
				Records.Add(record);
				record.FormattedMessage = formatter(state, exception);
				if (OnLog != null) OnLog(record);
			}
		}

		private sealed class LogRecord
		{
			public LogRecord(LogLevel level, EventId eventId, object state, Exception exception, Delegate formatter)
			{
				Level = level;
				EventId = eventId;
				State = state;
				Exception = exception;
				Formatter = formatter;
			}
			public LogLevel Level { get; }
			public EventId EventId { get; }
			public object State { get; }
			public Exception Exception { get; }
			public Delegate Formatter { get; }
			public string FormattedMessage { get; set; }
			public string RepeatedFormattedMessage { get; set; }
			public Func<object, Exception, string> FormatterInvoker { get; set; }
		}

		private sealed class ForwardingRecordingLogger : ILogger
		{
			private readonly ILogger _inner;
			public ForwardingRecordingLogger(ILogger inner, string fixedCategory) { _inner = inner; FixedCategory = fixedCategory; }
			public string FixedCategory { get; }
			public List<LogRecord> Records { get; } = new List<LogRecord>();
			public List<object> BeginScopeStates { get; } = new List<object>();
			public Func<LogLevel, bool> IsEnabledCallback { get; set; }
			public Action AfterLog { get; set; }
			public Func<object, int, Exception> ScopeDisposeFailure { get; set; }
			public int IsEnabledCalls { get; private set; }
			public bool IsEnabled(LogLevel logLevel)
			{
				IsEnabledCalls++;
				return IsEnabledCallback == null ? _inner.IsEnabled(logLevel) : IsEnabledCallback(logLevel);
			}
			public IDisposable BeginScope<TState>(TState state)
			{
				var boxed = (object)state;
				BeginScopeStates.Add(boxed);
				var innerHandle = _inner.BeginScope(state);
				if (innerHandle == null) return null;
				var failure = ScopeDisposeFailure == null ? null : ScopeDisposeFailure(boxed, BeginScopeStates.Count);
				return new ForwardedScopeHandle(innerHandle, failure);
			}
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				var record = new LogRecord(logLevel, eventId, state, exception, formatter);
				record.FormatterInvoker = (value, error) => formatter((TState)value, error);
				record.FormattedMessage = formatter(state, exception);
				record.RepeatedFormattedMessage = formatter(state, exception);
				Records.Add(record);
				try { _inner.Log(logLevel, eventId, state, exception, formatter); }
				finally { if (AfterLog != null) AfterLog(); }
			}
		}

		private sealed class ForwardedScopeHandle : IDisposable
		{
			private readonly IDisposable _inner;
			private readonly Exception _failure;
			public ForwardedScopeHandle(IDisposable inner, Exception failure) { _inner = inner; _failure = failure; }
			public void Dispose()
			{
				try { _inner.Dispose(); }
				finally { if (_failure != null) throw _failure; }
			}
		}

		private sealed class DynamicTrackingExternalScopeProvider : IExternalScopeProvider
		{
			private readonly List<ScopeNode> _nodes = new List<ScopeNode>();
			public void ForEachScope<TState>(Action<object, TState> callback, TState state)
			{
				foreach (var node in _nodes.ToArray()) callback(node.State, state);
			}
			public IDisposable Push(object state)
			{
				var node = new ScopeNode(state);
				_nodes.Add(node);
				return new DynamicScopeHandle(this, node);
			}
			public object[] Snapshot() { return _nodes.Select(node => node.State).ToArray(); }
			private void Pop(ScopeNode node)
			{
				if (_nodes.Count == 0 || !ReferenceEquals(_nodes[_nodes.Count - 1], node))
					throw new InvalidOperationException("External test scopes must unwind in order.");
				_nodes.RemoveAt(_nodes.Count - 1);
			}
			private sealed class ScopeNode
			{
				public ScopeNode(object state) { State = state; }
				public object State { get; }
			}
			private sealed class DynamicScopeHandle : IDisposable
			{
				private readonly DynamicTrackingExternalScopeProvider _owner;
				private readonly ScopeNode _node;
				private bool _disposed;
				public DynamicScopeHandle(DynamicTrackingExternalScopeProvider owner, ScopeNode node) { _owner = owner; _node = node; }
				public void Dispose()
				{
					if (_disposed) return;
					_owner.Pop(_node);
					_disposed = true;
				}
			}
		}

		private sealed class ConcurrentRecordingLogger : ILogger
		{
			private readonly CountdownEvent _entered;
			private readonly ManualResetEventSlim _release;
			private readonly ConcurrentQueue<LogRecord> _records = new ConcurrentQueue<LogRecord>();
			private int _isEnabledCalls;
			public ConcurrentRecordingLogger(CountdownEvent entered, ManualResetEventSlim release) { _entered = entered; _release = release; }
			public LogRecord[] Records => _records.ToArray();
			public int IsEnabledCalls => Volatile.Read(ref _isEnabledCalls);
			public bool IsEnabled(LogLevel logLevel)
			{
				Interlocked.Increment(ref _isEnabledCalls);
				_entered.Signal();
				return _release.Wait(TimeSpan.FromSeconds(15));
			}
			public IDisposable BeginScope<TState>(TState state) { return null; }
			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				var record = new LogRecord(logLevel, eventId, state, exception, formatter);
				record.FormattedMessage = formatter(state, exception);
				_records.Enqueue(record);
			}
		}

		private sealed class TrackingScopeHandle : IDisposable
		{
			private readonly string _name;
			private readonly IList<string> _order;
			private readonly Exception _failure;
			public TrackingScopeHandle(string name, IList<string> order, Exception failure = null)
			{
				_name = name;
				_order = order;
				_failure = failure;
			}
			public int DisposeCalls { get; private set; }
			public void Dispose()
			{
				DisposeCalls++;
				_order.Add(_name);
				if (_failure != null) throw _failure;
			}
		}

		private sealed class CountingOrdinaryDestination : BaseLoggingDestination
		{
			public CountingOrdinaryDestination() : base(LogLevels.Trace) { }
			public int Calls { get; private set; }
			protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex) { Calls++; }
		}

		private sealed class CountingExactDestination<T> : ProphetsWay.Utilities.Generics.BaseLoggingDestination<T>
		{
			public CountingExactDestination() : base(LogLevels.Trace) { }
			public int Calls { get; private set; }
			public T LastMetadata { get; private set; }
			public string LastMessage { get; private set; }
			protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception ex)
			{
				Calls++;
				LastMetadata = metadata;
				LastMessage = message;
			}
		}

		private sealed class ThrowingOrdinaryDestination : BaseLoggingDestination
		{
			private readonly Exception _failure;
			public ThrowingOrdinaryDestination(Exception failure) : base(LogLevels.Trace) { _failure = failure; }
			protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex) { throw _failure; }
		}

		private sealed class ThrowingExactDestination<T> : ProphetsWay.Utilities.Generics.BaseLoggingDestination<T>
		{
			private readonly Exception _failure;
			public ThrowingExactDestination(Exception failure) : base(LogLevels.Trace) { _failure = failure; }
			protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception ex) { throw _failure; }
		}

		private class UnfamiliarState
		{
			public int ToStringCalls { get; private set; }
			public override string ToString()
			{
				ToStringCalls++;
				throw new InvalidOperationException("Opaque state must not be rendered.");
			}
		}
		private sealed class DerivedState : UnfamiliarState { }

		private enum TransportFailurePoint
		{
			GetEnumerator,
			MoveNextAfterPrefix,
			CurrentAfterPrefix,
			Dispose
		}

		private sealed class FaultingTransportSequence : IEnumerable<KeyValuePair<string, object>>
		{
			private readonly TransportFailurePoint _failurePoint;
			public FaultingTransportSequence(TransportFailurePoint failurePoint) { _failurePoint = failurePoint; }
			public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
			{
				if (_failurePoint == TransportFailurePoint.GetEnumerator)
					throw new Exception("synthetic enumerator acquisition failure");
				return new Enumerator(this);
			}
			IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }

			private sealed class Enumerator : IEnumerator<KeyValuePair<string, object>>
			{
				private readonly FaultingTransportSequence _owner;
				private int _moveCalls;
				private int _currentCalls;
				public Enumerator(FaultingTransportSequence owner) { _owner = owner; }
				public KeyValuePair<string, object> Current
				{
					get
					{
						if (_owner._failurePoint == TransportFailurePoint.CurrentAfterPrefix && _currentCalls == 1)
							throw new Exception("synthetic Current failure after prefix");
						_currentCalls++;
						return new KeyValuePair<string, object>("captured", _currentCalls);
					}
				}
				object IEnumerator.Current => Current;
				public bool MoveNext()
				{
					if (_owner._failurePoint == TransportFailurePoint.MoveNextAfterPrefix && _moveCalls > 0)
						throw new Exception("synthetic MoveNext failure after prefix");
					if (_moveCalls == 0 || (_owner._failurePoint == TransportFailurePoint.CurrentAfterPrefix && _moveCalls == 1))
					{
						_moveCalls++;
						return true;
					}
					_moveCalls++;
					return false;
				}
				public void Reset() { throw new NotSupportedException(); }
				public void Dispose()
				{
					if (_owner._failurePoint == TransportFailurePoint.Dispose)
						throw new Exception("synthetic enumerator disposal failure");
				}
			}
		}

		private sealed class DisposalFailureSequence : IEnumerable<KeyValuePair<string, object>>
		{
			public int DisposeCalls { get; private set; }
			public IEnumerator<KeyValuePair<string, object>> GetEnumerator() { return new Enumerator(this); }
			IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
			private sealed class Enumerator : IEnumerator<KeyValuePair<string, object>>
			{
				private readonly DisposalFailureSequence _owner;
				private bool _moved;
				public Enumerator(DisposalFailureSequence owner) { _owner = owner; }
				public KeyValuePair<string, object> Current => new KeyValuePair<string, object>("captured", 1);
				object IEnumerator.Current => Current;
				public bool MoveNext()
				{
					if (_moved) return false;
					_moved = true;
					return true;
				}
				public void Reset() { _moved = false; }
				public void Dispose()
				{
					_owner.DisposeCalls++;
					throw new InvalidOperationException("sequence disposal failure");
				}
			}
		}
	}
}
