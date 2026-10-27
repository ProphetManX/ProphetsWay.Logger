using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities;
using Shouldly;
using Xunit;
using NativeLogger = ProphetsWay.Utilities.Logger;

namespace ProphetsWay.Logger.Tests
{
	[CollectionDefinition(Name, DisableParallelization = true)]
	public sealed class MicrosoftInboundTestCollection : ICollectionFixture<InboundTestEnvironment>
	{
		public const string Name = "Microsoft inbound bridge";
	}

	public sealed class InboundTestEnvironment : IDisposable
	{
		private readonly IsolatedFileFixture _fixture;
		private bool _disposed;

		public InboundTestEnvironment()
		{
			_fixture = new IsolatedFileFixture();
			DirectoryPath = _fixture.RootPath;
			try
			{
				NativeLogger.ConfigureAutomaticFileHostDirectory(DirectoryPath);
			}
			catch
			{
				_fixture.Dispose();
				throw;
			}
		}

		public string DirectoryPath { get; private set; }

		public void AssertNoAutomaticOutput()
		{
			_fixture.ValidateRootPath();
			Directory.GetFiles(DirectoryPath).ShouldBeEmpty("No inbound test should trigger automatic file fallback.");
			Directory.GetDirectories(DirectoryPath).ShouldBeEmpty();
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			_fixture.Dispose();
		}
	}

	[Collection(MicrosoftInboundTestCollection.Name)]
	public class MicrosoftLoggerProviderTests
	{
		private readonly InboundTestEnvironment _environment;

		public MicrosoftLoggerProviderTests(InboundTestEnvironment environment)
		{
			_environment = environment;
		}

		[Fact]
		public void ShouldExposeOnlyTheReviewedProviderAndKeepTheLoggerFacadeInternal()
		{
			var assembly = typeof(NativeLogger).Assembly;
			var providerType = InboundTestSupport.RequiredType("MicrosoftLoggerProvider");
			providerType.IsPublic.ShouldBeTrue();
			providerType.IsSealed.ShouldBeTrue();
			providerType.GetInterfaces().ShouldContain(typeof(ILoggerProvider));
			providerType.GetInterfaces().ShouldContain(typeof(ISupportExternalScope));

			var mapperType = InboundTestSupport.RequiredType("IMicrosoftLogLabelMapper");
			mapperType.IsPublic.ShouldBeTrue();
			var constructor = providerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
				.ShouldHaveSingleItem();
			var constructorParameter = constructor.GetParameters().ShouldHaveSingleItem();
			constructorParameter.ParameterType.ShouldBe(mapperType);
			constructorParameter.Name.ShouldBe("labelMapper");
			constructorParameter.IsOptional.ShouldBeTrue();
			constructorParameter.HasDefaultValue.ShouldBeTrue();
			constructorParameter.DefaultValue.ShouldBeNull();

			var publicMethods = providerType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			publicMethods.Select(method => method.Name).OrderBy(name => name)
				.ShouldBe(new[] { "CreateLogger", "Dispose", "SetScopeProvider" });
			var createLogger = providerType.GetMethod("CreateLogger", new[] { typeof(string) });
			createLogger.ReturnType.ShouldBe(typeof(ILogger));
			createLogger.GetParameters().Single().Name.ShouldBe("categoryName");
			var setScopeProvider = providerType.GetMethod("SetScopeProvider", new[] { typeof(IExternalScopeProvider) });
			setScopeProvider.ReturnType.ShouldBe(typeof(void));
			setScopeProvider.GetParameters().Single().Name.ShouldBe("scopeProvider");
			providerType.GetMethod("Dispose", Type.EmptyTypes).ReturnType.ShouldBe(typeof(void));

			var facadeType = InboundTestSupport.RequiredType("MicrosoftLogger");
			facadeType.IsPublic.ShouldBeFalse();
			assembly.GetExportedTypes().ShouldNotContain(facadeType);
			facadeType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).ShouldBeEmpty();
		}
		[Fact]
		public void ShouldDeliverOneCommonRouteEntryWithOriginalStateDuplicatePropertiesAndSeparateScopes()
		{
			using (var scenario = new InboundScenario(_environment, typeof(List<KeyValuePair<string, object>>)))
			{
				var properties = new List<KeyValuePair<string, object>>
				{
					new KeyValuePair<string, object>("duplicate", 1),
					new KeyValuePair<string, object>("duplicate", null),
					new KeyValuePair<string, object>("", "empty key"),
					new KeyValuePair<string, object>(null, "null key"),
					default(KeyValuePair<string, object>)
				};
				var outerScope = new List<KeyValuePair<string, object>>
				{
					new KeyValuePair<string, object>("scope-key", "outer")
				};
				var innerScope = new object();
				var exception = new InvalidOperationException("synthetic inbound exception");
				var formatterCalls = 0;
				Func<List<KeyValuePair<string, object>>, Exception, string> formatter = (state, actualException) =>
				{
					formatterCalls++;
					state.ShouldBeSameAs(properties);
					actualException.ShouldBeSameAs(exception);
					return "producer formatted text";
				};

				using (scenario.Logger.BeginScope(outerScope))
				using (scenario.Logger.BeginScope(innerScope))
				{
					scenario.Logger.Log(LogLevel.Warning, new EventId(-17, "producer-event"),
						properties, exception, formatter);
				}

				formatterCalls.ShouldBe(1);
				scenario.Recipient.Records.Count.ShouldBe(1);
				var record = scenario.Recipient.Records.Single();
				record.Level.ShouldBe(LogLevels.WarningOnly);
				record.Message.ShouldBe("producer formatted text");
				record.Exception.ShouldBeSameAs(exception);
				record.Context.ShouldNotBeNull();

				var entry = record.Metadata;
				InboundTestSupport.Read<string>(entry, "CategoryName").ShouldBe("category \t");
				var eventId = InboundTestSupport.Read<EventId?>(entry, "EventId");
				eventId.HasValue.ShouldBeTrue();
				eventId.Value.Id.ShouldBe(-17);
				eventId.Value.Name.ShouldBe("producer-event");
				InboundTestSupport.Read<LogLevels>(entry, "OriginalLevel").ShouldBe(LogLevels.WarningOnly);
				InboundTestSupport.Read<object>(entry, "State").ShouldBeSameAs(properties);
				InboundTestSupport.Read<object>(entry, "OriginalState").ShouldBeSameAs(properties);
				InboundTestSupport.Read<Type>(entry, "StateType").ShouldBe(properties.GetType());
				InboundTestSupport.Read<Delegate>(entry, "ProducerFormatter").ShouldBeSameAs(formatter);
				InboundTestSupport.Read<Delegate>(entry, "OriginalProducerFormatter").ShouldBeSameAs(formatter);
				InboundTestSupport.Read<string>(entry, "RawMessage").ShouldBeNull();
				InboundTestSupport.Read<string>(entry, "FormattedMessage").ShouldBe("producer formatted text");

				var capturedProperties = InboundTestSupport.Read<IEnumerable<KeyValuePair<string, object>>>(entry, "Properties").ToArray();
				capturedProperties.ShouldBe(properties);
				var scopes = InboundTestSupport.Read<IEnumerable<object>>(entry, "Scopes").ToArray();
				scopes.Length.ShouldBe(2);
				InboundTestSupport.Read<object>(scopes[0], "State").ShouldBeSameAs(outerScope);
				InboundTestSupport.Read<IEnumerable<KeyValuePair<string, object>>>(scopes[0], "Properties")
					.ShouldBe(outerScope);
				InboundTestSupport.Read<object>(scopes[1], "State").ShouldBeSameAs(innerScope);
				InboundTestSupport.Read<IEnumerable<KeyValuePair<string, object>>>(scopes[1], "Properties")
					.ShouldBeEmpty();
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
			}
		}

		[Theory]
		[InlineData(LogLevel.Trace, LogLevels.TraceOnly)]
		[InlineData(LogLevel.Debug, LogLevels.DebugOnly)]
		[InlineData(LogLevel.Information, LogLevels.InformationOnly)]
		[InlineData(LogLevel.Warning, LogLevels.WarningOnly)]
		[InlineData(LogLevel.Error, LogLevels.ErrorOnly)]
		[InlineData(LogLevel.Critical, LogLevels.Critical)]
		public void ShouldMapEachMicrosoftSeverityAndLogWithoutAPriorEnablementPrecheck(LogLevel microsoftLevel,
			LogLevels nativeLevel)
		{
			using (var scenario = new InboundScenario(_environment, typeof(string), registrationMask: nativeLevel))
			{
				var calls = 0;
				scenario.Logger.Log(microsoftLevel, default(EventId), "opaque scalar state", null,
					(state, exception) =>
					{
						calls++;
						state.ShouldBe("opaque scalar state");
						exception.ShouldBeNull();
						return "captured";
					});

				calls.ShouldBe(1);
				scenario.Logger.IsEnabled(microsoftLevel).ShouldBeTrue();
				scenario.Recipient.Records.Select(record => record.Level).ShouldBe(new[] { nativeLevel });
				scenario.Recipient.Records.Single().Message.ShouldBe("captured");
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
			}
		}

        [Fact]
        public void ShouldTreatNoneAsNoOpAndRejectUnsupportedLevelsBeforeCallbacks()
        {
            MapperProbe.Reset();
            MapperProbe.Entry = (category, level, eventId, state, exception, properties) => null;
            MapperProbe.Scope = (state, properties) => null;
            var externalScopes = new CountingExternalScopeProvider(new object[] { new object() });
            try
            {
                using (var scenario = new InboundScenario(_environment, typeof(object),
                    labelMapper: InboundTestSupport.CreateMapper()))
                {
                    InboundTestSupport.SetScopeProvider(scenario.Provider, externalScopes);
                    var formatterCalls = 0;
                    scenario.Logger.IsEnabled(LogLevel.None).ShouldBeFalse();
                    scenario.Logger.Log(LogLevel.None, default(EventId), new object(), null,
                        (state, exception) =>
                        {
                            formatterCalls++;
                            return "None must remain a no-op";
                        });

                    var unsupportedLevel = (LogLevel)12345;
                    var enabledError = Should.Throw<ArgumentOutOfRangeException>(
                        () => scenario.Logger.IsEnabled(unsupportedLevel));
                    enabledError.ParamName.ShouldBe("logLevel");
                    var logError = Should.Throw<ArgumentOutOfRangeException>(() =>
                        scenario.Logger.Log(unsupportedLevel, default(EventId), new object(), null,
                            (state, exception) =>
                            {
                                formatterCalls++;
                                return "unsupported levels must be rejected";
                            }));
                    logError.ParamName.ShouldBe("logLevel");

                    formatterCalls.ShouldBe(0);
                    MapperProbe.EntryCalls.ShouldBe(0);
                    MapperProbe.ScopeCalls.ShouldBe(0);
                    externalScopes.ForEachCalls.ShouldBe(0);
                    scenario.Recipient.Records.ShouldBeEmpty();
                    scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
                    scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
                    _environment.AssertNoAutomaticOutput();
                }
            }
            finally
            {
                MapperProbe.Reset();
            }
        }
		[Fact]
		public void ShouldKeepEnabledRejectAllRouteFromFallbackAndWithholdDeniedPayload()
		{
			var rejectAll = new DestinationLabelPolicy(LabelFilterMode.AllowOnly,
				new[] { new SensitivityLabel("not-attached") });
			using (var scenario = new InboundScenario(_environment, typeof(object),
				runtimeStateType: typeof(UnfamiliarInboundState), exactRoutePolicy: rejectAll))
			{
				var formatterCalls = 0;
				scenario.Logger.IsEnabled(LogLevel.Information).ShouldBeTrue();
				scenario.Logger.Log(LogLevel.Information, default(EventId), new UnfamiliarInboundState(), null,
					(state, exception) =>
					{
						formatterCalls++;
						return "must not be formatted";
					});

				formatterCalls.ShouldBe(0);
				scenario.Recipient.Records.ShouldBeEmpty();
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
				_environment.AssertNoAutomaticOutput();
			}
		}

		[Fact]
		public void ShouldInvokeEntryAndScopeMapperOnceAndKeepEachAttachmentAndOriginSeparate()
		{
			var entryLabel = new SensitivityLabel("entry:unfamiliar/v1");
			var outerLabel = new SensitivityLabel("scope:outer");
			var innerLabel = new SensitivityLabel("scope:inner");
			var entryAnnotations = new LogAnnotations(new[] { entryLabel, entryLabel });
			var outerAnnotations = new LogAnnotations(new[] { outerLabel });
			var innerAnnotations = new LogAnnotations(new[] { innerLabel });
			var allowAllMappedLabels = new DestinationLabelPolicy(LabelFilterMode.AllowOnly,
				new[] { entryLabel, outerLabel, innerLabel });
			var entryState = new List<KeyValuePair<string, object>>
			{
				new KeyValuePair<string, object>("duplicate", null),
				new KeyValuePair<string, object>("duplicate", new object())
			};
			var outerState = new List<KeyValuePair<string, object>>
			{
				new KeyValuePair<string, object>("frame", "outer")
			};
			var innerState = new object();
			var scopes = new CountingExternalScopeProvider(new object[] { outerState, innerState });
			var exception = new InvalidOperationException("mapper input exception");
			var eventId = new EventId(41, "mapped-entry");
			var formatterCalls = 0;
			MapperProbe.Reset();
			MapperProbe.Entry = (category, level, actualEvent, state, actualException, properties) =>
			{
				category.ShouldBe("category \t");
				level.ShouldBe(LogLevel.Warning);
				actualEvent.ShouldBe(eventId);
				state.ShouldBeSameAs(entryState);
				actualException.ShouldBeSameAs(exception);
				properties.Select(pair => pair.Key).ShouldBe(new[] { "duplicate", "duplicate" });
				properties[0].Value.ShouldBeNull();
				properties[1].Value.ShouldBeSameAs(entryState[1].Value);
				return entryAnnotations;
			};
			MapperProbe.Scope = (state, properties) =>
			{
				if (ReferenceEquals(state, outerState))
				{
					properties.Select(pair => pair.Key).ShouldBe(new[] { "frame" });
					return outerAnnotations;
				}
				state.ShouldBeSameAs(innerState);
				properties.ShouldBeEmpty();
				return innerAnnotations;
			};

			try
			{
				using (var scenario = new InboundScenario(_environment, entryState.GetType(),
					exactRoutePolicy: allowAllMappedLabels, labelMapper: InboundTestSupport.CreateMapper()))
				{
					InboundTestSupport.SetScopeProvider(scenario.Provider, scopes);
					scenario.Logger.IsEnabled(LogLevel.Warning).ShouldBeTrue();
					MapperProbe.EntryCalls.ShouldBe(0);
					MapperProbe.ScopeCalls.ShouldBe(0);
					scopes.ForEachCalls.ShouldBe(0);
					scenario.Logger.Log(LogLevel.Warning, eventId, entryState, exception, (state, actualException) =>
					{
						formatterCalls++;
						state.ShouldBeSameAs(entryState);
						actualException.ShouldBeSameAs(exception);
						return "producer result";
					});

					MapperProbe.EntryCalls.ShouldBe(1);
					MapperProbe.ScopeCalls.ShouldBe(2);
					formatterCalls.ShouldBe(1);
					scopes.ForEachCalls.ShouldBe(1);
					scenario.Recipient.Records.ShouldHaveSingleItem();
					var record = scenario.Recipient.Records.Single();
					var entry = record.Metadata;
					var nativeLabels = InboundTestSupport.Read<LogLabelContext>(record.Context, "Labels");
					nativeLabels.EntryAnnotations.ShouldBeNull();
					nativeLabels.ScopeAnnotations.ShouldBeEmpty();
					nativeLabels.Origins.ShouldBeEmpty();
					nativeLabels.EffectiveLabels.ShouldBeEmpty();
					var sourceNativeContext = InboundTestSupport.Read<LogContext>(entry, "NativeContext");
					var sourceNativeLabels = sourceNativeContext.Labels;
					sourceNativeLabels.EntryAnnotations.ShouldBeNull();
					sourceNativeLabels.ScopeAnnotations.ShouldBeEmpty();
					sourceNativeLabels.Origins.ShouldBeEmpty();
					sourceNativeLabels.EffectiveLabels.ShouldBeEmpty();
					InboundTestSupport.Read<LogAnnotations>(entry, "Annotations").ShouldBeSameAs(entryAnnotations);
					entryAnnotations.LabelOccurrences.ShouldBe(new[] { entryLabel, entryLabel });
					var frames = InboundTestSupport.Read<IEnumerable<object>>(entry, "Scopes").ToArray();
					frames.Length.ShouldBe(2);
					var outerFrameAnnotations = InboundTestSupport.Read<LogAnnotations>(frames[0], "Annotations");
					outerFrameAnnotations.ShouldBeSameAs(outerAnnotations);
					outerFrameAnnotations.LabelOccurrences.ShouldBe(new[] { outerLabel });
					var innerFrameAnnotations = InboundTestSupport.Read<LogAnnotations>(frames[1], "Annotations");
					innerFrameAnnotations.ShouldBeSameAs(innerAnnotations);
					innerFrameAnnotations.LabelOccurrences.ShouldBe(new[] { innerLabel });
					scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
					scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
				}
			}
			finally
			{
				MapperProbe.Reset();
			}
		}

		[Fact]
		public void ShouldSelectAcrossCurrentNativeAndMappedExternalOriginsWithoutRewritingNativeFacts()
		{
			var nativeLabel = new SensitivityLabel("native:current");
			var entryLabel = new SensitivityLabel("microsoft:entry");
			var scopeLabel = new SensitivityLabel("microsoft:scope");
			var nativeAnnotations = new LogAnnotations(new[] { nativeLabel });
			var entryAnnotations = new LogAnnotations(new[] { entryLabel });
			var externalScopeAnnotations = new LogAnnotations(new[] { scopeLabel });
			var externalScopeState = new object();
			var externalScopes = new CountingExternalScopeProvider(new[] { externalScopeState });
			MapperProbe.Reset();
			MapperProbe.Entry = (category, level, eventId, state, exception, properties) => entryAnnotations;
			MapperProbe.Scope = (state, properties) =>
			{
				state.ShouldBeSameAs(externalScopeState);
				properties.ShouldBeEmpty();
				return externalScopeAnnotations;
			};

			try
			{
				using (var scenario = new InboundScenario(_environment, typeof(string),
					labelMapper: InboundTestSupport.CreateMapper()))
				{
					var rejectsNative = scenario.Routes.AddExactRecipient(
						new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { nativeLabel }));
					var rejectsEntry = scenario.Routes.AddExactRecipient(
						new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { entryLabel }));
					var rejectsExternalScope = scenario.Routes.AddExactRecipient(
						new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { scopeLabel }));
					InboundTestSupport.SetScopeProvider(scenario.Provider, externalScopes);
					using (NativeLogger.BeginScope(nativeAnnotations))
					{
						scenario.Logger.Log(LogLevel.Information, default(EventId), "combined input", null,
							(state, exception) => "combined formatted");
					}

					MapperProbe.EntryCalls.ShouldBe(1);
					MapperProbe.ScopeCalls.ShouldBe(1);
					scenario.Recipient.Records.ShouldHaveSingleItem();
					rejectsNative.Records.ShouldBeEmpty();
					rejectsEntry.Records.ShouldBeEmpty();
					rejectsExternalScope.Records.ShouldBeEmpty();
					var record = scenario.Recipient.Records.Single();
					var entry = record.Metadata;
					var nativeContext = InboundTestSupport.Read<LogContext>(entry, "NativeContext");
					nativeContext.ShouldNotBeNull();
					nativeContext.Labels.EntryAnnotations.ShouldBeNull();
					nativeContext.Labels.EffectiveLabels.ShouldBe(new[] { nativeLabel });
					nativeContext.Labels.Origins.ShouldHaveSingleItem();
					nativeContext.Labels.Origins[0].Label.ShouldBe(nativeLabel);
					nativeContext.Labels.Origins[0].ScopeIndex.ShouldBe(0);
					nativeContext.Labels.Origins[0].OccurrenceIndex.ShouldBe(0);
					InboundTestSupport.Read<LogAnnotations>(entry, "Annotations").ShouldBeSameAs(entryAnnotations);
					var externalFrames = InboundTestSupport.Read<IEnumerable<object>>(entry, "Scopes").ToArray();
					externalFrames.ShouldHaveSingleItem();
					InboundTestSupport.Read<object>(externalFrames[0], "State").ShouldBeSameAs(externalScopeState);
					InboundTestSupport.Read<LogAnnotations>(externalFrames[0], "Annotations")
						.ShouldBeSameAs(externalScopeAnnotations);
				}
			}
			finally
			{
				MapperProbe.Reset();
			}
		}

		[Fact]
		public void ShouldMapRepeatedAndEmptyExternalScopeOccurrencesOnlyDuringLogCapture()
		{
			var repeatedState = new List<KeyValuePair<string, object>>
			{
				new KeyValuePair<string, object>("repeated", "frame")
			};
			var emptyState = new object();
			var repeatedAnnotations = new LogAnnotations(new[] { new SensitivityLabel("external:repeated") });
			var emptyAnnotations = new LogAnnotations(new SensitivityLabel[0]);
			var externalScopes = new CountingExternalScopeProvider(new object[] { repeatedState, repeatedState, emptyState });
			var repeatedMapCalls = 0;
			var emptyMapCalls = 0;
			MapperProbe.Reset();
			MapperProbe.Entry = (category, level, eventId, state, exception, properties) => null;
			MapperProbe.Scope = (state, properties) =>
			{
				if (ReferenceEquals(state, repeatedState))
				{
					repeatedMapCalls++;
					properties.ShouldBe(repeatedState);
					return repeatedAnnotations;
				}
				state.ShouldBeSameAs(emptyState);
				emptyMapCalls++;
				properties.ShouldBeEmpty();
				return emptyAnnotations;
			};

			try
			{
				using (var scenario = new InboundScenario(_environment, typeof(object),
					labelMapper: InboundTestSupport.CreateMapper()))
				{
					var secondExactRecipient = scenario.Routes.AddExactRecipient(
						new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]));
					InboundTestSupport.SetScopeProvider(scenario.Provider, externalScopes);
					using (scenario.Logger.BeginScope(new object()))
					{
						MapperProbe.EntryCalls.ShouldBe(0);
						MapperProbe.ScopeCalls.ShouldBe(0);
						scenario.Logger.Log(LogLevel.Information, default(EventId), new object(), null,
							(state, exception) => "repeated external frames");
						MapperProbe.EntryCalls.ShouldBe(1);
						MapperProbe.ScopeCalls.ShouldBe(3);
						externalScopes.ForEachCalls.ShouldBe(1);
						externalScopes.PushCalls.ShouldBe(1);
					}

					repeatedMapCalls.ShouldBe(2);
					emptyMapCalls.ShouldBe(1);
					MapperProbe.ScopeCalls.ShouldBe(3, "Mapping is per captured occurrence, not per recipient or handle disposal.");
					scenario.Recipient.Records.ShouldHaveSingleItem();
					secondExactRecipient.Records.ShouldHaveSingleItem();
					var frames = InboundTestSupport.Read<IEnumerable<object>>(
						scenario.Recipient.Records.Single().Metadata, "Scopes").ToArray();
					frames.Length.ShouldBe(3);
					ReferenceEquals(frames[0], frames[1]).ShouldBeFalse();
					InboundTestSupport.Read<object>(frames[0], "State").ShouldBeSameAs(repeatedState);
					InboundTestSupport.Read<object>(frames[1], "State").ShouldBeSameAs(repeatedState);
					InboundTestSupport.Read<LogAnnotations>(frames[0], "Annotations").ShouldBeSameAs(repeatedAnnotations);
					InboundTestSupport.Read<LogAnnotations>(frames[1], "Annotations").ShouldBeSameAs(repeatedAnnotations);
					InboundTestSupport.Read<object>(frames[2], "State").ShouldBeSameAs(emptyState);
					InboundTestSupport.Read<LogAnnotations>(frames[2], "Annotations").ShouldBeSameAs(emptyAnnotations);
					InboundTestSupport.Read<LogAnnotations>(frames[2], "Annotations").LabelOccurrences.ShouldBeEmpty();
				}
			}
			finally
			{
				MapperProbe.Reset();
			}
		}

		[Fact]
		public void ShouldRejectOutOfOrderNativeScopeDisposalWithoutChangingLocalStackAndIgnoreDoubleDisposal()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				var outerLabel = new SensitivityLabel("native:outer");
				var innerLabel = new SensitivityLabel("native:inner");
				var outer = NativeLogger.BeginScope(new LogAnnotations(new[] { outerLabel }));
				var inner = NativeLogger.BeginScope(new LogAnnotations(new[] { innerLabel }));
				try
				{
					Should.Throw<InvalidOperationException>(() => outer.Dispose());
					scenario.Logger.Log(LogLevel.Information, default(EventId), "after rejected disposal", null,
						(state, exception) => (string)state);
					NativeLabelIds(scenario.Recipient.Records.Last()).ShouldBe(new[] { "native:inner", "native:outer" });
					scenario.Recipient.Records.Last().Context.Labels.Origins
						.Select(origin => Tuple.Create(origin.Label.Identifier, origin.ScopeIndex, origin.OccurrenceIndex))
						.ShouldBe(new[] { Tuple.Create("native:outer", (int?)0, 0), Tuple.Create("native:inner", (int?)1, 0) });

					inner.Dispose();
					inner.Dispose();
					scenario.Logger.Log(LogLevel.Information, default(EventId), "after inner double disposal", null,
						(state, exception) => (string)state);
					NativeLabelIds(scenario.Recipient.Records.Last()).ShouldBe(new[] { "native:outer" });
					outer.Dispose();
					outer.Dispose();
					scenario.Logger.Log(LogLevel.Information, default(EventId), "after outer double disposal", null,
						(state, exception) => (string)state);
					NativeLabelIds(scenario.Recipient.Records.Last()).ShouldBeEmpty();
				}
				finally
				{
					inner.Dispose();
					outer.Dispose();
				}
			}
		}

		[Fact]
		public async System.Threading.Tasks.Task ShouldKeepNormalExecutionFlowInheritanceAndChildScopeCleanupLocal()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				var parentLabel = new SensitivityLabel("flow:parent");
				var childLabel = new SensitivityLabel("flow:child");
				using (NativeLogger.BeginScope(new LogAnnotations(new[] { parentLabel })))
				{
					await System.Threading.Tasks.Task.Yield();
					scenario.Logger.Log(LogLevel.Information, default(EventId), "parent before child", null,
						(state, exception) => (string)state);
					await System.Threading.Tasks.Task.Run(async () =>
					{
						using (NativeLogger.BeginScope(new LogAnnotations(new[] { childLabel })))
						{
							scenario.Logger.Log(LogLevel.Information, default(EventId), "child scope", null,
								(state, exception) => (string)state);
							await System.Threading.Tasks.Task.Yield();
							scenario.Logger.Log(LogLevel.Information, default(EventId), "child after await", null,
								(state, exception) => (string)state);
						}
						scenario.Logger.Log(LogLevel.Information, default(EventId), "child after cleanup", null,
							(state, exception) => (string)state);
					}, TestContext.Current.CancellationToken);
					scenario.Logger.Log(LogLevel.Information, default(EventId), "parent after child", null,
						(state, exception) => (string)state);
				}
				scenario.Logger.Log(LogLevel.Information, default(EventId), "outside parent scope", null,
					(state, exception) => (string)state);

				scenario.Recipient.Records.Count.ShouldBe(6);
				NativeLabelIds(scenario.Recipient.Records[0]).ShouldBe(new[] { "flow:parent" });
				NativeLabelIds(scenario.Recipient.Records[1]).ShouldBe(new[] { "flow:child", "flow:parent" });
				NativeLabelIds(scenario.Recipient.Records[2]).ShouldBe(new[] { "flow:child", "flow:parent" });
				NativeLabelIds(scenario.Recipient.Records[3]).ShouldBe(new[] { "flow:parent" });
				NativeLabelIds(scenario.Recipient.Records[4]).ShouldBe(new[] { "flow:parent" });
				NativeLabelIds(scenario.Recipient.Records[4]).ShouldNotContain("flow:child");
				NativeLabelIds(scenario.Recipient.Records[5]).ShouldBeEmpty();
			}
		}

        [Fact]
        public void ShouldPassOriginalMicrosoftScopeToSuppliedSourceAndCleanItsHandleOnce()
        {
            var externalScopes = new CountingExternalScopeProvider(new object[0]);
            var outerState = new object();
            var innerState = new object();
            using (var scenario = new InboundScenario(_environment, typeof(string)))
            {
                InboundTestSupport.SetScopeProvider(scenario.Provider, externalScopes);
                var outer = scenario.Logger.BeginScope(outerState);
                var outerSourceHandle = externalScopes.PushedHandles.Single();
                var inner = scenario.Logger.BeginScope(innerState);
                var innerSourceHandle = externalScopes.PushedHandles.Last();

                externalScopes.PushCalls.ShouldBe(2);
                externalScopes.PushedStates.Count.ShouldBe(2);
                externalScopes.PushedStates[0].ShouldBeSameAs(outerState);
                externalScopes.PushedStates[1].ShouldBeSameAs(innerState);
                Should.Throw<InvalidOperationException>(() => outer.Dispose());
                outerSourceHandle.DisposeCalls.ShouldBe(0);
                innerSourceHandle.DisposeCalls.ShouldBe(0);

                inner.Dispose();
                innerSourceHandle.DisposeCalls.ShouldBe(1);
                inner.Dispose();
                innerSourceHandle.DisposeCalls.ShouldBe(1);
                outer.Dispose();
                outerSourceHandle.DisposeCalls.ShouldBe(1);
                outer.Dispose();
                outerSourceHandle.DisposeCalls.ShouldBe(1);
                externalScopes.PushedHandles[0].ShouldBeSameAs(outerSourceHandle);
                externalScopes.PushedHandles[1].ShouldBeSameAs(innerSourceHandle);
            }
            externalScopes.DisposeCalls.ShouldBe(0);
        }
		[Fact]
		public void ShouldEnforceInboundAdapterScopeLifoAndIgnoreDoubleDisposal()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				var outerState = new object();
				var innerState = new object();
				var outer = scenario.Logger.BeginScope(outerState);
				var inner = scenario.Logger.BeginScope(innerState);
				try
				{
					Should.Throw<InvalidOperationException>(() => outer.Dispose());
					scenario.Logger.Log(LogLevel.Information, default(EventId), "after rejected adapter disposal", null,
						(state, exception) => (string)state);
					var afterRejected = CapturedScopeStates(scenario.Recipient.Records.Last());
					afterRejected.Length.ShouldBe(2);
					afterRejected.ShouldContain(outerState);
					afterRejected.ShouldContain(innerState);

					inner.Dispose();
					inner.Dispose();
					scenario.Logger.Log(LogLevel.Information, default(EventId), "after adapter inner double disposal", null,
						(state, exception) => (string)state);
					var afterInner = CapturedScopeStates(scenario.Recipient.Records.Last());
					afterInner.Length.ShouldBe(1);
					afterInner[0].ShouldBeSameAs(outerState);

					outer.Dispose();
					outer.Dispose();
					scenario.Logger.Log(LogLevel.Information, default(EventId), "after adapter outer double disposal", null,
						(state, exception) => (string)state);
					CapturedScopeStates(scenario.Recipient.Records.Last()).ShouldBeEmpty();
				}
				finally
				{
					inner.Dispose();
					outer.Dispose();
				}
			}
		}

		[Fact]
		public async System.Threading.Tasks.Task ShouldInheritInboundAdapterScopesAcrossAsyncChildAndIsolateChildCleanup()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				var parentState = new object();
				var childState = new object();
				using (scenario.Logger.BeginScope(parentState))
				{
					await System.Threading.Tasks.Task.Yield();
					scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter parent before child", null,
						(state, exception) => (string)state);
					await System.Threading.Tasks.Task.Run(async () =>
					{
						using (scenario.Logger.BeginScope(childState))
						{
							scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter child scope", null,
								(state, exception) => (string)state);
							await System.Threading.Tasks.Task.Yield();
							scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter child after await", null,
								(state, exception) => (string)state);
						}
						scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter child after cleanup", null,
							(state, exception) => (string)state);
					}, TestContext.Current.CancellationToken);
					scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter parent after child", null,
						(state, exception) => (string)state);
				}
				scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter outside parent scope", null,
					(state, exception) => (string)state);

				var records = scenario.Recipient.Records;
				records.Count.ShouldBe(6);
				var parentBefore = CapturedScopeStates(records[0]);
				parentBefore.Length.ShouldBe(1);
				parentBefore[0].ShouldBeSameAs(parentState);
				foreach (var index in new[] { 1, 2 })
				{
					var childScopes = CapturedScopeStates(records[index]);
					childScopes.Length.ShouldBe(2);
					childScopes.ShouldContain(parentState);
					childScopes.ShouldContain(childState);
				}
				foreach (var index in new[] { 3, 4 })
				{
					var parentScopes = CapturedScopeStates(records[index]);
					parentScopes.Length.ShouldBe(1);
					parentScopes[0].ShouldBeSameAs(parentState);
				}
				CapturedScopeStates(records[5]).ShouldBeEmpty();
			}
		}

		[Fact]
		public async System.Threading.Tasks.Task ShouldIsolateInboundAdapterScopesAcrossSiblingsAndRetainCapturedChildAfterParentCleanup()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				var parentState = new object();
				var childState = new object();
				var childScopeOpened = new System.Threading.Tasks.TaskCompletionSource<bool>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				var siblingObserved = new System.Threading.Tasks.TaskCompletionSource<bool>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				var childAContinued = new System.Threading.Tasks.TaskCompletionSource<bool>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				var siblingContinued = new System.Threading.Tasks.TaskCompletionSource<bool>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				var releaseChildren = new System.Threading.Tasks.TaskCompletionSource<bool>(
					System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
				var parentScope = scenario.Logger.BeginScope(parentState);
				var parentScopeDisposed = false;
				System.Threading.Tasks.Task childA = null;
				System.Threading.Tasks.Task sibling = null;
				try
				{
					childA = System.Threading.Tasks.Task.Run(async () =>
					{
						try
						{
							using (scenario.Logger.BeginScope(childState))
							{
								scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter child A while local scope active", null,
									(state, exception) => (string)state);
								childScopeOpened.TrySetResult(true);
								await releaseChildren.Task;
								try
								{
									scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter child A after parent cleanup", null,
										(state, exception) => (string)state);
								}
								finally
								{
									childAContinued.TrySetResult(true);
								}
								await siblingContinued.Task;
							}
							scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter child A after local cleanup", null,
								(state, exception) => (string)state);
						}
						finally
						{
							childScopeOpened.TrySetResult(true);
							childAContinued.TrySetResult(true);
						}
					});

					sibling = System.Threading.Tasks.Task.Run(async () =>
					{
						try
						{
							await childScopeOpened.Task;
							scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter sibling while child A active", null,
								(state, exception) => (string)state);
							siblingObserved.TrySetResult(true);
							await releaseChildren.Task;
							await childAContinued.Task;
							scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter sibling after parent cleanup", null,
								(state, exception) => (string)state);
						}
						finally
						{
							siblingObserved.TrySetResult(true);
							siblingContinued.TrySetResult(true);
						}
					});

					await siblingObserved.Task;
					scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter parent while child A active", null,
						(state, exception) => (string)state);
					parentScope.Dispose();
					parentScopeDisposed = true;
					scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter parent after own cleanup", null,
						(state, exception) => (string)state);
					releaseChildren.TrySetResult(true);
					await System.Threading.Tasks.Task.WhenAll(childA, sibling);
					scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter parent after child completion", null,
						(state, exception) => (string)state);
				}
				finally
				{
					releaseChildren.TrySetResult(true);
					var children = new[] { childA, sibling }.Where(task => task != null).ToArray();
					if (children.Length > 0)
						await System.Threading.Tasks.Task.WhenAll(children);
					if (!parentScopeDisposed) parentScope.Dispose();
				}

				var records = scenario.Recipient.Records;
				records.Count.ShouldBe(8);
				Func<string, IInboundCapturedRecord> record = message => records.Single(item => item.Message == message);
				var childAActive = CapturedScopeStates(record("adapter child A while local scope active"));
				childAActive.Length.ShouldBe(2);
				childAActive.ShouldContain(parentState);
				childAActive.ShouldContain(childState);
				var siblingWhileChildActive = CapturedScopeStates(record("adapter sibling while child A active"));
				siblingWhileChildActive.Length.ShouldBe(1);
				siblingWhileChildActive[0].ShouldBeSameAs(parentState);
				var parentWhileChildActive = CapturedScopeStates(record("adapter parent while child A active"));
				parentWhileChildActive.Length.ShouldBe(1);
				parentWhileChildActive[0].ShouldBeSameAs(parentState);
				CapturedScopeStates(record("adapter parent after own cleanup")).ShouldBeEmpty();
				var childAAfterParentCleanup = CapturedScopeStates(record("adapter child A after parent cleanup"));
				childAAfterParentCleanup.Length.ShouldBe(2);
				childAAfterParentCleanup.ShouldContain(parentState);
				childAAfterParentCleanup.ShouldContain(childState);
				var siblingAfterParentCleanup = CapturedScopeStates(record("adapter sibling after parent cleanup"));
				siblingAfterParentCleanup.Length.ShouldBe(1);
				siblingAfterParentCleanup[0].ShouldBeSameAs(parentState);
				var childAAfterCleanup = CapturedScopeStates(record("adapter child A after local cleanup"));
				childAAfterCleanup.Length.ShouldBe(1);
				childAAfterCleanup[0].ShouldBeSameAs(parentState);
				CapturedScopeStates(record("adapter parent after child completion")).ShouldBeEmpty();
			}
		}

		[Fact]
		public async System.Threading.Tasks.Task ShouldNotInheritInboundAdapterScopesWhenExecutionContextFlowIsSuppressed()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				var parentState = new object();
				using (scenario.Logger.BeginScope(parentState))
				{
					scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter before suppressed child", null,
						(state, exception) => (string)state);
					System.Threading.Tasks.Task suppressedChild;
					using (System.Threading.ExecutionContext.SuppressFlow())
					{
						suppressedChild = System.Threading.Tasks.Task.Run(() =>
							scenario.Logger.Log(LogLevel.Information, default(EventId), "suppressed adapter child", null,
								(state, exception) => (string)state), TestContext.Current.CancellationToken);
					}
					await suppressedChild;
					scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter parent after suppressed child", null,
						(state, exception) => (string)state);
				}
				scenario.Logger.Log(LogLevel.Information, default(EventId), "adapter outside suppressed scope", null,
					(state, exception) => (string)state);

				scenario.Recipient.Records.Count.ShouldBe(4);
				var beforeSuppressed = CapturedScopeStates(scenario.Recipient.Records[0]);
				beforeSuppressed.Length.ShouldBe(1);
				beforeSuppressed[0].ShouldBeSameAs(parentState);
				CapturedScopeStates(scenario.Recipient.Records[1]).ShouldBeEmpty();
				var afterSuppressed = CapturedScopeStates(scenario.Recipient.Records[2]);
				afterSuppressed.Length.ShouldBe(1);
				afterSuppressed[0].ShouldBeSameAs(parentState);
				CapturedScopeStates(scenario.Recipient.Records[3]).ShouldBeEmpty();
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldWithholdAndSafelyReportMapperCaptureFailureWithoutPayload(bool failScopeMapping)
		{
			const string categorySecret = "category-secret";
			const string stateSecret = "state-secret";
			const string messageSecret = "message-secret";
			const string exceptionSecret = "exception-secret";
			MapperProbe.Reset();
			MapperProbe.Entry = (category, level, eventId, state, exception, properties) =>
			{
				if (!failScopeMapping) throw new InvalidOperationException("mapper-secret");
				return null;
			};
			MapperProbe.Scope = (state, properties) =>
			{
				if (failScopeMapping) throw new InvalidOperationException("scope-mapper-secret");
				return null;
			};
			using (var scenario = new InboundScenario(_environment, typeof(SecretInboundState), labelMapper: InboundTestSupport.CreateMapper()))
			{
				if (failScopeMapping)
					InboundTestSupport.SetScopeProvider(scenario.Provider,
						new CountingExternalScopeProvider(new object[] { new SecretInboundState("scope-secret") }));
				var formatterCalls = 0;
				var entryMapCalls = 0;
				var scopeMapCalls = 0;
				var reports = new List<LogFailureReport>();
				Action<LogFailureReport> observer = report => reports.Add(report);
				NativeLogger.DispatchFailed += observer;
				try
				{
					var logger = InboundTestSupport.CreateLogger(scenario.Provider, categorySecret);
					var state = new SecretInboundState(stateSecret);
					if (failScopeMapping)
						logger.Log(LogLevel.Error, default(EventId), state,
							new InvalidOperationException(exceptionSecret), (actual, exception) =>
							{
								formatterCalls++;
								throw new InvalidOperationException(messageSecret);
							});
					else
						logger.Log(LogLevel.Error, default(EventId), state,
							new InvalidOperationException(exceptionSecret), (actual, exception) =>
						{
							formatterCalls++;
							throw new InvalidOperationException(messageSecret);
						});
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					entryMapCalls = MapperProbe.EntryCalls;
					scopeMapCalls = MapperProbe.ScopeCalls;
					MapperProbe.Reset();
				}

				reports.ShouldHaveSingleItem();
				formatterCalls.ShouldBe(0);
				entryMapCalls.ShouldBeLessThanOrEqualTo(1);
				scopeMapCalls.ShouldBeLessThanOrEqualTo(1);
				if (failScopeMapping)
					scopeMapCalls.ShouldBe(1, "The configured scope mapper failure must be reached.");
				else
					entryMapCalls.ShouldBe(1, "The configured entry mapper failure must be reached.");
				reports[0].CoreCaptureFailureCount.ShouldBe(1);
				reports[0].Failures.ShouldBeEmpty();
				reports[0].OverflowCount.ShouldBe(0);
				scenario.Recipient.Records.ShouldBeEmpty();
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
				_environment.AssertNoAutomaticOutput();
				var safeReport = reports[0].ToString();
				foreach (var secret in new[] { categorySecret, stateSecret, messageSecret, exceptionSecret, "mapper-secret", "scope-secret", "scope-mapper-secret" })
					safeReport.ShouldNotContain(secret);
			}
		}

		[Fact]
		public void ShouldReportOnlySafeRecipientFailureFactsAndNeverExposeInboundPayload()
		{
			const string categorySecret = "category-secret";
			const string stateSecret = "state-secret";
			const string messageSecret = "message-secret";
			const string exceptionSecret = "exception-secret";
			const string outputSecret = "output-cause-secret";
			using (var scenario = new InboundScenario(_environment, typeof(SecretInboundState)))
			{
				var originalException = new InvalidOperationException(exceptionSecret);
				var state = new SecretInboundState(stateSecret);
				var failure = new InvalidOperationException(outputSecret);
				scenario.Routes.AddThrowingEntryRecipient(failure);
				var reports = new List<LogFailureReport>();
				Action<LogFailureReport> observer = report => reports.Add(report);
				NativeLogger.DispatchFailed += observer;
				LogDispatchException thrown = null;
				try
				{
					var logger = InboundTestSupport.CreateLogger(scenario.Provider, categorySecret);
					thrown = Should.Throw<LogDispatchException>(() => logger.Log(LogLevel.Error, default(EventId),
						state, originalException, (actual, exception) => messageSecret));
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
				}

				reports.ShouldHaveSingleItem();
				thrown.Report.CorrelationId.ShouldBe(reports[0].CorrelationId);
				var failureReport = thrown.Report;
				failureReport.Failures.ShouldHaveSingleItem();
				failureReport.Failures[0].RegistrationId.ShouldBe(2);
				failureReport.Failures[0].Stage.ShouldBe(LogFailureStage.Output);
				failureReport.CoreCaptureFailureCount.ShouldBe(0);
				failureReport.OverflowCount.ShouldBe(0);
				failureReport.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Select(property => property.Name).OrderBy(name => name)
					.ShouldBe(new[] { "CoreCaptureFailureCount", "CorrelationId", "Failures", "OverflowCount" });
				failureReport.Failures[0].GetType()
					.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Select(property => property.Name).OrderBy(name => name)
					.ShouldBe(new[] { "RegistrationId", "Stage" });
				thrown.InnerException.ShouldBeNull();
				thrown.Data.Count.ShouldBe(0);
				thrown.StackTrace.ShouldBeNull();
				var safeText = thrown.Message + " " + thrown.ToString() + " " + failureReport.ToString();
				foreach (var secret in new[] { categorySecret, stateSecret, messageSecret, exceptionSecret, outputSecret })
					safeText.ShouldNotContain(secret);
				scenario.Recipient.Records.ShouldHaveSingleItem();
				_environment.AssertNoAutomaticOutput();
			}
		}

		[Fact]
		public void ShouldNotTreatAPropertyNameAsMappedSensitivityAndShouldNotPrecheckLabels()
		{
			var allowOnly = new DestinationLabelPolicy(LabelFilterMode.AllowOnly,
				new[] { new SensitivityLabel("sensitivity") });
			using (var scenario = new InboundScenario(_environment, typeof(List<KeyValuePair<string, object>>),
				exactRoutePolicy: allowOnly))
			{
				var formatterCalls = 0;
				scenario.Logger.IsEnabled(LogLevel.Information).ShouldBeTrue();
				scenario.Logger.Log(LogLevel.Information, default(EventId),
					new List<KeyValuePair<string, object>>
					{
						new KeyValuePair<string, object>("sensitivity", "looks like a classification")
					},
					null, (state, exception) =>
					{
						formatterCalls++;
						return "must remain withheld without explicit mapping";
					});

				formatterCalls.ShouldBe(0);
				scenario.Recipient.Records.ShouldBeEmpty();
				_environment.AssertNoAutomaticOutput();
			}
		}

		[Fact]
		public void ShouldUseOnlyTheLatestBorrowedScopeSourceAndAvoidScopeWorkDuringIsEnabled()
		{
			var first = new CountingExternalScopeProvider(new object[] { "retired source frame" });
			var expectedFrames = new object[]
			{
				new List<KeyValuePair<string, object>> { new KeyValuePair<string, object>("frame", "outer") },
				new object()
			};
			var current = new CountingExternalScopeProvider(expectedFrames);
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				InboundTestSupport.SetScopeProvider(scenario.Provider, first);
				InboundTestSupport.SetScopeProvider(scenario.Provider, current);
				first.ForEachCalls.ShouldBe(0);
				current.ForEachCalls.ShouldBe(0);
				scenario.Logger.IsEnabled(LogLevel.Information).ShouldBeTrue();
				current.ForEachCalls.ShouldBe(0);

				scenario.Logger.Log(LogLevel.Information, default(EventId), "state", null,
					(state, exception) => "scoped entry");
				first.ForEachCalls.ShouldBe(0);
				current.ForEachCalls.ShouldBe(1);
				current.PushCalls.ShouldBe(0);
				var scopes = InboundTestSupport.Read<IEnumerable<object>>(
					scenario.Recipient.Records.Single().Metadata, "Scopes").ToArray();
				scopes.Length.ShouldBe(2);
				InboundTestSupport.Read<object>(scopes[0], "State").ShouldBeSameAs(expectedFrames[0]);
				InboundTestSupport.Read<object>(scopes[1], "State").ShouldBeSameAs(expectedFrames[1]);
			}

			first.DisposeCalls.ShouldBe(0);
			current.DisposeCalls.ShouldBe(0);
		}

		[Fact]
		public void ShouldReportFormatterCaptureFailureOnceWithoutDeliveringAnIncompleteEntry()
		{
			using (var scenario = new InboundScenario(_environment, typeof(object)))
			using (var errorOutput = new StringWriter(CultureInfo.InvariantCulture))
			{
				const string categorySecret = "formatter-category-secret";
				const string stateSecret = "formatter-state-secret";
				const string messageSecret = "formatter-message-secret";
				const string exceptionSecret = "formatter-original-exception-secret";
				const string labelSecret = "formatter-label-secret";
				const string dataKeySecret = "formatter-data-key-secret";
				const string dataValueSecret = "formatter-data-value-secret";
				const string causeSecret = "formatter-cause-secret";
				var state = new SecretInboundState(stateSecret);
				var originalException = new InvalidOperationException(exceptionSecret);
				originalException.Data[dataKeySecret] = dataValueSecret;
				var formatterFailure = new InvalidOperationException(messageSecret + " " + causeSecret);
				formatterFailure.Data[dataKeySecret] = dataValueSecret;
				var reports = new List<LogFailureReport>();
				Action<LogFailureReport> observer = report => reports.Add(report);
				var priorError = Console.Error;
				NativeLogger.DispatchFailed += observer;
				try
				{
					Console.SetError(errorOutput);
					using (NativeLogger.BeginScope(new LogAnnotations(new[]
						{ new SensitivityLabel(labelSecret) })))
					{
						var logger = InboundTestSupport.CreateLogger(scenario.Provider, categorySecret);
						var thrown = Record.Exception(() => logger.Log(LogLevel.Error, default(EventId), state,
							originalException, (actualState, exception) => throw formatterFailure));
						thrown.ShouldBeNull("Shared formatter capture failure reports and returns under the approved default policy.");
					}
				}
				finally
				{
					NativeLogger.DispatchFailed -= observer;
					Console.SetError(priorError);
				}

				reports.ShouldHaveSingleItem();
				var failureReport = reports.Single();
				failureReport.CorrelationId.ShouldNotBe(Guid.Empty);
				failureReport.CoreCaptureFailureCount.ShouldBe(1);
				failureReport.Failures.ShouldBeEmpty("A shared formatter capture failure has no recipient stage descriptor.");
				failureReport.OverflowCount.ShouldBe(0);
				failureReport.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Select(property => property.Name).OrderBy(name => name)
					.ShouldBe(new[] { "CoreCaptureFailureCount", "CorrelationId", "Failures", "OverflowCount" });
				failureReport.Failures.Count.ShouldBeLessThanOrEqualTo(8);
				((long)failureReport.CoreCaptureFailureCount + failureReport.Failures.Count + failureReport.OverflowCount).ShouldBe(1L);
				var diagnostic = errorOutput.ToString();
				var formatterStack = formatterFailure.StackTrace;
				formatterStack.ShouldNotBeNullOrEmpty();
				diagnostic.Length.ShouldBeLessThanOrEqualTo(512);
				diagnostic.ShouldContain(failureReport.CorrelationId.ToString("D"));
				diagnostic.ShouldContain("failures=1");
				diagnostic.ShouldContain("overflow=0");
				foreach (var secret in new[]
				{
					categorySecret, stateSecret, messageSecret, exceptionSecret, labelSecret,
					dataKeySecret, dataValueSecret, causeSecret, formatterFailure.ToString(), originalException.ToString(), formatterStack
				})
				{
					diagnostic.ShouldNotContain(secret);
					failureReport.ToString().ShouldNotContain(secret);
				}
				scenario.Recipient.Records.ShouldBeEmpty();
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
			}
		}

        [Fact]
        public void ShouldCompleteCapturedRecipientsWhenProviderDisposesDuringAnAdmittedCall()
        {
            using (var scenario = new InboundScenario(_environment, typeof(string)))
            {
                var laterRecipient = scenario.Routes.AddExactRecipient(new DestinationLabelPolicy(
                    LabelFilterMode.NoFilter, new SensitivityLabel[0]));
                var callbackOrder = new List<string>();
                scenario.Recipient.AfterLog = () =>
                {
                    callbackOrder.Add("first");
                    InboundTestSupport.DisposeProvider(scenario.Provider);
                };
                laterRecipient.AfterLog = () => callbackOrder.Add("later");
                var formatterCalls = 0;
                scenario.Logger.Log(LogLevel.Information, default(EventId), "admitted before retirement", null,
                    (state, exception) =>
                    {
                        formatterCalls++;
                        return "captured call completed";
                    });

                callbackOrder.ShouldBe(new[] { "first", "later" });
                formatterCalls.ShouldBe(1);
                scenario.Recipient.Records.ShouldHaveSingleItem();
                laterRecipient.Records.ShouldHaveSingleItem();
                ReferenceEquals(scenario.Recipient.Records[0].Metadata, laterRecipient.Records[0].Metadata)
                    .ShouldBeTrue();
                scenario.Logger.IsEnabled(LogLevel.Information).ShouldBeFalse();

                var inertFormatterCalls = 0;
                scenario.Logger.Log(LogLevel.Information, default(EventId), "after retirement", null,
                    (state, exception) =>
                    {
                        inertFormatterCalls++;
                        return "must remain inert";
                    });
                inertFormatterCalls.ShouldBe(0);
                scenario.Recipient.Records.Count.ShouldBe(1);
                laterRecipient.Records.Count.ShouldBe(1);
                scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
                scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
                _environment.AssertNoAutomaticOutput();
            }
        }
		[Fact]
		public void ShouldMakeExistingAndLaterLoggersInertAfterProviderRetirement()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				InboundTestSupport.DisposeProvider(scenario.Provider);
				InboundTestSupport.DisposeProvider(scenario.Provider);
				var laterLogger = InboundTestSupport.CreateLogger(scenario.Provider, "later category");
				scenario.Logger.IsEnabled(LogLevel.Error).ShouldBeFalse();
				laterLogger.IsEnabled(LogLevel.Error).ShouldBeFalse();

				var formatterCalls = 0;
				scenario.Logger.Log(LogLevel.Error, default(EventId), "state", null,
					(state, exception) =>
					{
						formatterCalls++;
						return "retired";
					});
				laterLogger.Log(LogLevel.Error, default(EventId), "state", null,
					(state, exception) =>
					{
						formatterCalls++;
						return "retired";
					});
				scenario.Logger.BeginScope(new object()).Dispose();
				formatterCalls.ShouldBe(0);
				scenario.Recipient.Records.ShouldBeEmpty();
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
			}
		}

		[Fact]
		public void ShouldPreserveAnUnfamiliarRuntimeValueWithoutReroutingByItsType()
		{
			using (var scenario = new InboundScenario(_environment, typeof(object), runtimeStateType: typeof(UnfamiliarInboundState)))
			{
				var state = new UnfamiliarInboundState();
				scenario.Logger.Log<object>(LogLevel.Information, default(EventId), state, null,
					(actual, exception) =>
					{
						actual.ShouldBeSameAs(state);
						return "unfamiliar value accepted";
					});

				scenario.Recipient.Records.ShouldHaveSingleItem();
				InboundTestSupport.Read<object>(scenario.Recipient.Records[0].Metadata, "OriginalState")
					.ShouldBeSameAs(state);
				InboundTestSupport.Read<Type>(scenario.Recipient.Records[0].Metadata, "StateType").ShouldBe(typeof(object));
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
			}
		}

		[Fact]
		public void ShouldDeliverFreshIdenticalTextCallsAsIndependentEntries()
		{
			using (var scenario = new InboundScenario(_environment, typeof(string)))
			{
				for (var index = 0; index < 2; index++)
				{
					scenario.Logger.Log(LogLevel.Information, default(EventId), "same state", null,
						(state, exception) => "same fresh text");
				}

				scenario.Recipient.Records.Count.ShouldBe(2);
				scenario.Recipient.Records.Select(record => record.Message)
					.ShouldBe(new[] { "same fresh text", "same fresh text" });
				ReferenceEquals(scenario.Recipient.Records[0].Metadata, scenario.Recipient.Records[1].Metadata)
					.ShouldBeFalse();
				scenario.Routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
				scenario.Routes.OrdinaryRecipient.Calls.ShouldBe(0);
			}
		}

		private sealed class UnfamiliarInboundState
		{
		}

		private sealed class SecretInboundState
		{
			private readonly string _secret;
			public SecretInboundState(string secret) { _secret = secret; }
			public override string ToString() => _secret;
		}

		private static object[] CapturedScopeStates(IInboundCapturedRecord record)
		{
			return InboundTestSupport.Read<IEnumerable<object>>(record.Metadata, "Scopes").ToArray()
				.Select(frame => InboundTestSupport.Read<object>(frame, "State")).ToArray();
		}

		private static string[] NativeLabelIds(IInboundCapturedRecord record)
		{
			return record.Context.Labels.EffectiveLabels.Select(label => label.Identifier).OrderBy(identifier => identifier).ToArray();
		}
	}

	internal sealed class InboundScenario : IDisposable
	{
		private readonly Type _providerType;
		private bool _disposed;

		public InboundScenario(InboundTestEnvironment environment, Type producerStateType, LogLevels intrinsicMask = LogLevels.Trace,
			LogLevels registrationMask = LogLevels.Trace, Type runtimeStateType = null,
			DestinationLabelPolicy exactRoutePolicy = null, object labelMapper = null)
		{
			_providerType = InboundTestSupport.RequiredType("MicrosoftLoggerProvider");
			Routes = new InboundRoutes(environment, producerStateType, runtimeStateType, intrinsicMask, registrationMask, exactRoutePolicy);
			try
			{
				Provider = InboundTestSupport.CreateProvider(_providerType, labelMapper);
				Logger = InboundTestSupport.CreateLogger(Provider, "category \t");
			}
			catch
			{
				try
				{
					if (Provider != null) InboundTestSupport.DisposeProvider(Provider);
				}
				finally
				{
					Routes.Dispose();
				}
				throw;
			}
		}

		public object Provider { get; private set; }
		public ILogger Logger { get; private set; }
		public InboundRoutes Routes { get; private set; }
		public IInboundCapture Recipient => Routes.Recipient;

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			try
			{
				if (Provider != null) InboundTestSupport.DisposeProvider(Provider);
			}
			finally
			{
				Routes.Dispose();
			}
		}
	}

	internal sealed class InboundRoutes : IDisposable
	{
		private readonly List<Tuple<Type, IInboundCapture>> _typedRecipients = new List<Tuple<Type, IInboundCapture>>();
		private readonly List<Tuple<Type, object>> _additionalTypedRecipients = new List<Tuple<Type, object>>();
		private readonly InboundTestEnvironment _environment;
		private readonly Type _entryRouteType;
		private bool _disposed;

		public InboundRoutes(InboundTestEnvironment environment, Type producerStateType, Type runtimeStateType, LogLevels intrinsicMask,
			LogLevels registrationMask, DestinationLabelPolicy exactRoutePolicy = null)
		{
			_environment = environment;
			try
			{
				var entryType = InboundTestSupport.RequiredType("LogBridgeEntry");
				_entryRouteType = entryType;
				var genericEntryType = InboundTestSupport.RequiredType("LogBridgeEntry`1");
				Recipient = AddTypedRecipient(entryType, intrinsicMask, registrationMask, exactRoutePolicy);
				AddTypedRecipient(producerStateType, LogLevels.Trace, LogLevels.Trace);
				AddTypedRecipient(genericEntryType.MakeGenericType(producerStateType), LogLevels.Trace, LogLevels.Trace);
				if (runtimeStateType != null && runtimeStateType != producerStateType)
					AddTypedRecipient(runtimeStateType, LogLevels.Trace, LogLevels.Trace);
				OrdinaryRecipient = new InboundOrdinaryDestination();
				NativeLogger.AddDestination(OrdinaryRecipient);
			}
			catch
			{
				Dispose();
				throw;
			}
		}

		public IInboundCapture Recipient { get; private set; }
		public IList<IInboundCapture> UnexpectedRecipients =>
			_typedRecipients.Where(item => item.Item1 != _entryRouteType)
				.Select(item => item.Item2).ToList();
		public InboundOrdinaryDestination OrdinaryRecipient { get; private set; }

		private IInboundCapture AddTypedRecipient(Type routeType, LogLevels intrinsicMask, LogLevels registrationMask,
			DestinationLabelPolicy labelPolicy = null)
		{
			var recipient = InboundTestSupport.CreateRecipient(routeType, intrinsicMask);
			InboundTestSupport.RegisterExactRoute(routeType, recipient, registrationMask, labelPolicy);
			_typedRecipients.Add(Tuple.Create(routeType, recipient));
			return recipient;
		}

		public IInboundCapture AddExactRecipient(DestinationLabelPolicy labelPolicy)
		{
			return AddTypedRecipient(_entryRouteType, LogLevels.Trace, LogLevels.Trace, labelPolicy);
		}

		public void AddThrowingEntryRecipient(Exception failure)
		{
			var recipient = Activator.CreateInstance(
				typeof(ThrowingInboundDestination<>).MakeGenericType(_entryRouteType), LogLevels.Trace, failure);
			InboundTestSupport.RegisterExactRoute(_entryRouteType, recipient, LogLevels.Trace);
			_additionalTypedRecipients.Add(Tuple.Create(_entryRouteType, recipient));
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;
			try
			{
				for (var index = _additionalTypedRecipients.Count - 1; index >= 0; index--)
				{
					var recipient = _additionalTypedRecipients[index];
					InboundTestSupport.Invoke(InboundTestSupport.RouteMethod("RemoveDestination", recipient.Item1, 1),
						null, new[] { recipient.Item2 });
				}
				for (var index = _typedRecipients.Count - 1; index >= 0; index--)
				{
					var recipient = _typedRecipients[index];
					InboundTestSupport.Invoke(InboundTestSupport.RouteMethod("RemoveDestination", recipient.Item1, 1),
						null, new object[] { recipient.Item2 });
				}
			}
			finally
			{
				if (OrdinaryRecipient != null) NativeLogger.RemoveDestination(OrdinaryRecipient);
				_environment.AssertNoAutomaticOutput();
			}
		}
	}

	internal sealed class InboundOrdinaryDestination : ILoggingDestination
	{
		public int Calls { get; private set; }
		public bool ValidateMessageLevel(LogLevels messageLevel) => true;
		public void Log(LogLevels level, string message = null, Exception ex = null) { Calls++; }
	}

	internal sealed class ThrowingInboundDestination<T> : ProphetsWay.Utilities.Generics.BaseLoggingDestination<T>
	{
		private readonly Exception _failure;
		public ThrowingInboundDestination(LogLevels intrinsicMask, Exception failure) : base(intrinsicMask)
		{
			_failure = failure;
		}
		protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception exception)
		{
			throw _failure;
		}
	}

	internal sealed class CountingExternalScopeProvider : IExternalScopeProvider, IDisposable
{
    private readonly object[] _frames;

    public CountingExternalScopeProvider(object[] frames)
    {
        _frames = frames;
    }

    public int ForEachCalls { get; private set; }
    public int PushCalls { get; private set; }
    public int DisposeCalls { get; private set; }
    public List<object> PushedStates { get; } = new List<object>();
    public List<TrackingScopeHandle> PushedHandles { get; } = new List<TrackingScopeHandle>();

    public void ForEachScope<TState>(Action<object, TState> callback, TState state)
    {
        ForEachCalls++;
        foreach (var frame in _frames) callback(frame, state);
    }

    public IDisposable Push(object state)
    {
        PushCalls++;
        PushedStates.Add(state);
        var handle = new TrackingScopeHandle();
        PushedHandles.Add(handle);
        return handle;
    }

    public void Dispose()
    {
        DisposeCalls++;
    }

    public sealed class TrackingScopeHandle : IDisposable
    {
        public int DisposeCalls { get; private set; }
        public void Dispose() { DisposeCalls++; }
    }
}

internal interface IInboundCapture
{
    IList<IInboundCapturedRecord> Records { get; }
    Action AfterLog { get; set; }
}
internal interface IInboundCapturedRecord
	{
		LogContext Context { get; }
		LogLevels Level { get; }
		object Metadata { get; }
		string Message { get; }
		Exception Exception { get; }
	}

	internal sealed class InboundCaptureDestination<T> : ProphetsWay.Utilities.Generics.BaseLoggingDestination<T>, IInboundCapture
{
    public InboundCaptureDestination(LogLevels intrinsicMask) : base(intrinsicMask) { }

    public List<IInboundCapturedRecord> Records { get; } = new List<IInboundCapturedRecord>();
    public Action AfterLog { get; set; }
    IList<IInboundCapturedRecord> IInboundCapture.Records => Records;

    protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception exception)
    {
        Records.Add(new InboundCapturedRecord<T>(context, level, metadata, message, exception));
        if (AfterLog != null) AfterLog();
    }
}
public static class MapperProbe
	{
		public static Func<string, LogLevel, EventId, object, Exception,
			ReadOnlyCollection<KeyValuePair<string, object>>, LogAnnotations> Entry;
		public static Func<object, ReadOnlyCollection<KeyValuePair<string, object>>, LogAnnotations> Scope;
		public static int EntryCalls;
		public static int ScopeCalls;

		public static LogAnnotations MapEntry<TState>(string category, LogLevel level, EventId eventId,
			TState state, Exception exception, ReadOnlyCollection<KeyValuePair<string, object>> properties)
		{
			EntryCalls++;
			return Entry(category, level, eventId, state, exception, properties);
		}

		public static LogAnnotations MapScope(object state,
			ReadOnlyCollection<KeyValuePair<string, object>> properties)
		{
			ScopeCalls++;
			return Scope(state, properties);
		}

		public static void Reset()
		{
			Entry = null;
			Scope = null;
			EntryCalls = 0;
			ScopeCalls = 0;
		}
	}

	internal static class MapperEmitter
	{
		public static object Create(Type mapperInterface)
		{
			var name = new AssemblyName("InboundMapperProbe" + Guid.NewGuid().ToString("N"));
			var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
			var module = assembly.DefineDynamicModule(name.Name);
			var type = module.DefineType(name.Name + "Implementation",
				TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Class);
			type.AddInterfaceImplementation(mapperInterface);

			foreach (var contractMethod in mapperInterface.GetMethods())
			{
				var sourceGenericArguments = contractMethod.GetGenericArguments();
				var method = type.DefineMethod(contractMethod.Name,
					MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final
					| MethodAttributes.HideBySig | MethodAttributes.NewSlot,
					contractMethod.CallingConvention);
				var genericMap = new Dictionary<Type, Type>();
				GenericTypeParameterBuilder[] emittedGenericArguments = null;
				if (sourceGenericArguments.Length > 0)
				{
					emittedGenericArguments = method.DefineGenericParameters(
						sourceGenericArguments.Select(argument => argument.Name).ToArray());
					for (var index = 0; index < sourceGenericArguments.Length; index++)
						genericMap.Add(sourceGenericArguments[index], emittedGenericArguments[index]);
				}

				var sourceParameters = contractMethod.GetParameters();
				method.SetReturnType(Substitute(contractMethod.ReturnType, genericMap));
				method.SetParameters(sourceParameters.Select(parameter => Substitute(parameter.ParameterType, genericMap)).ToArray());
				for (var index = 0; index < sourceParameters.Length; index++)
					method.DefineParameter(index + 1, sourceParameters[index].Attributes, sourceParameters[index].Name);

				var il = method.GetILGenerator();
				for (var index = 0; index < sourceParameters.Length; index++)
					il.Emit(OpCodes.Ldarg, (short)(index + 1));
				MethodInfo callback = typeof(MapperProbe).GetMethod(
					contractMethod.Name == "MapEntry" ? nameof(MapperProbe.MapEntry) : nameof(MapperProbe.MapScope));
				if (emittedGenericArguments != null)
					callback = callback.MakeGenericMethod(emittedGenericArguments.Cast<Type>().ToArray());
				il.Emit(OpCodes.Call, callback);
				il.Emit(OpCodes.Ret);
				type.DefineMethodOverride(method, contractMethod);
			}

			return Activator.CreateInstance(type.CreateType());
		}

		private static Type Substitute(Type source, IDictionary<Type, Type> genericMap)
		{
			Type replacement;
			if (source.IsGenericParameter && genericMap.TryGetValue(source, out replacement))
				return replacement;
			if (source.IsArray)
				return Substitute(source.GetElementType(), genericMap).MakeArrayType();
			if (!source.IsGenericType) return source;
			var arguments = source.GetGenericArguments().Select(argument => Substitute(argument, genericMap)).ToArray();
			return source.GetGenericTypeDefinition().MakeGenericType(arguments);
		}
	}

	internal sealed class InboundCapturedRecord<T> : IInboundCapturedRecord
	{
		public InboundCapturedRecord(LogContext context, LogLevels level, T metadata, string message, Exception exception)
		{
			Context = context;
			Level = level;
			Metadata = metadata;
			Message = message;
			Exception = exception;
		}

		public LogContext Context { get; private set; }
		public LogLevels Level { get; private set; }
		public object Metadata { get; private set; }
		public string Message { get; private set; }
		public Exception Exception { get; private set; }
	}

	internal static class InboundTestSupport
	{
		private static readonly Assembly ProductAssembly = typeof(NativeLogger).Assembly;

		public static Type RequiredType(string name)
		{
			var type = ProductAssembly.GetType("ProphetsWay.Utilities." + name, false);
			type.ShouldNotBeNull("The approved inbound contract requires ProphetsWay.Utilities." + name + ".");
			return type;
		}

		public static IInboundCapture CreateRecipient(Type routeType, LogLevels intrinsicMask)
		{
			return (IInboundCapture)Activator.CreateInstance(
				typeof(InboundCaptureDestination<>).MakeGenericType(routeType), intrinsicMask);
		}

		public static void RegisterExactRoute(Type routeType, object recipient, LogLevels registrationMask,
			DestinationLabelPolicy labelPolicy = null)
		{
			var settings = new DestinationRegistrationSettings(true, registrationMask,
				labelPolicy ?? new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]));
			Invoke(RouteMethod("AddDestination", routeType, 2), null, new[] { recipient, settings });
		}

		public static MethodInfo RouteMethod(string name, Type routeType, int parameterCount)
		{
			return typeof(NativeLogger).GetMethods(BindingFlags.Public | BindingFlags.Static)
				.Where(method => method.Name == name && method.IsGenericMethodDefinition
					&& method.GetParameters().Length == parameterCount)
				.Select(method => method.MakeGenericMethod(routeType))
				.Single(method => method.GetParameters().Last().ParameterType.IsInstanceOfType(
					parameterCount == 1 ? CreateMethodProbe(routeType) : new DestinationRegistrationSettings(
						true, LogLevels.Trace, new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]))));
		}

		private static object CreateMethodProbe(Type routeType)
		{
			return Activator.CreateInstance(typeof(InboundCaptureDestination<>).MakeGenericType(routeType), LogLevels.Trace);
		}

		public static object CreateProvider(Type providerType, object labelMapper = null)
		{
			var constructor = providerType.GetConstructors(BindingFlags.Public | BindingFlags.Instance).Single();
			constructor.GetParameters().Length.ShouldBe(1);
			return Invoke(constructor, null, new[] { labelMapper });
		}

		public static object CreateMapper()
		{
			return MapperEmitter.Create(RequiredType("IMicrosoftLogLabelMapper"));
		}

		public static ILogger CreateLogger(object provider, string category)
		{
			var method = provider.GetType().GetMethod("CreateLogger", new[] { typeof(string) });
			method.ShouldNotBeNull("CreateLogger(string) is part of the reviewed inbound provider contract.");
			return (ILogger)Invoke(method, provider, new object[] { category });
		}

		public static void SetScopeProvider(object provider, IExternalScopeProvider scopeProvider)
		{
			var method = provider.GetType().GetMethod("SetScopeProvider", new[] { typeof(IExternalScopeProvider) });
			method.ShouldNotBeNull("SetScopeProvider(IExternalScopeProvider) is part of the reviewed provider contract.");
			Invoke(method, provider, new object[] { scopeProvider });
		}

		public static object Read(object target, string propertyName)
		{
			var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
			property.ShouldNotBeNull("Missing reviewed inbound entry/scope property " + propertyName + ".");
			return property.GetValue(target, null);
		}

		public static T Read<T>(object target, string propertyName)
		{
			return (T)Read(target, propertyName);
		}

		public static void DisposeProvider(object provider)
		{
			Invoke(provider.GetType().GetMethod("Dispose", Type.EmptyTypes), provider, new object[0]);
		}

		public static object Invoke(MethodBase method, object target, object[] arguments)
		{
			try
			{
				return method is ConstructorInfo constructor
					? constructor.Invoke(arguments)
					: ((MethodInfo)method).Invoke(target, arguments);
			}
			catch (TargetInvocationException exception)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
				throw;
			}
		}
	}
}
