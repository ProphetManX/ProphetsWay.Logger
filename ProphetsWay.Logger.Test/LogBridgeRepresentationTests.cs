using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities;
using Shouldly;
using Xunit;
using NativeLogger = ProphetsWay.Utilities.Logger;

namespace ProphetsWay.Logger.Test
{
	public class LogBridgeRepresentationTests
	{
		[Fact]
		public void ShouldExposeOnlyTheReviewedCommonEntrySurfaceAndInternalConstructor()
		{
			var entryType = RequiredType("LogBridgeEntry");
			var scopeType = RequiredType("LogBridgeScope");
			entryType.IsAbstract.ShouldBeTrue();
			entryType.IsSealed.ShouldBeFalse();
			entryType.BaseType.ShouldBe(typeof(object));
			entryType.ContainsGenericParameters.ShouldBeFalse();
			ShouldHaveSurface(entryType, new Dictionary<string, Type>
			{
				{ "CategoryName", typeof(string) }, { "EventId", typeof(EventId?) },
				{ "OriginalLevel", typeof(LogLevels) }, { "RawMessage", typeof(string) },
				{ "FormattedMessage", typeof(string) }, { "Exception", typeof(Exception) },
				{ "Properties", typeof(ReadOnlyCollection<KeyValuePair<string, object>>) },
				{ "Scopes", typeof(ReadOnlyCollection<>).MakeGenericType(scopeType) },
				{ "Annotations", typeof(LogAnnotations) }, { "NativeContext", typeof(LogContext) },
				{ "BridgeMetadata", typeof(ReadOnlyCollection<KeyValuePair<string, object>>) },
				{ "OriginalState", typeof(object) }, { "StateType", typeof(Type) },
				{ "OriginalProducerFormatter", typeof(Delegate) }
			}, "OriginalState", "StateType", "OriginalProducerFormatter");
			ShouldHaveConstructor(entryType, CommonParameterNames(), CommonParameterTypes(scopeType));
		}

		[Fact]
		public void ShouldExposeOnlyTheReviewedUnconstrainedGenericEntryAndCommonOverrides()
		{
			var entryType = RequiredType("LogBridgeEntry`1");
			var commonType = RequiredType("LogBridgeEntry");
			var scopeType = RequiredType("LogBridgeScope");
			entryType.IsSealed.ShouldBeTrue();
			entryType.IsAbstract.ShouldBeFalse();
			entryType.BaseType.ShouldBe(commonType);
			var stateType = entryType.GetGenericArguments().ShouldHaveSingleItem();
			stateType.Name.ShouldBe("TState");
			stateType.GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
			stateType.GetGenericParameterConstraints().ShouldBeEmpty();
			var formatterType = typeof(Func<,,>).MakeGenericType(stateType, typeof(Exception), typeof(string));
			ShouldHaveSurface(entryType, new Dictionary<string, Type>
			{
				{ "State", stateType }, { "ProducerFormatter", formatterType },
				{ "OriginalState", typeof(object) }, { "StateType", typeof(Type) },
				{ "OriginalProducerFormatter", typeof(Delegate) }
			});
			foreach (var name in new[] { "OriginalState", "StateType", "OriginalProducerFormatter" })
			{
				var getter = entryType.GetProperty(name).GetGetMethod();
				getter.IsVirtual.ShouldBeTrue();
				getter.GetBaseDefinition().DeclaringType.ShouldBe(commonType);
			}
			ShouldHaveConstructor(entryType,
				new[] { "state", "producerFormatter" }.Concat(CommonParameterNames()).ToArray(),
				new[] { stateType, formatterType }.Concat(CommonParameterTypes(scopeType)).ToArray());
		}

		[Fact]
		public void ShouldExposeOnlyTheReviewedExternalScopeDataSurfaceAndInternalConstructor()
		{
			var scopeType = RequiredType("LogBridgeScope");
			scopeType.IsSealed.ShouldBeTrue();
			scopeType.IsAbstract.ShouldBeFalse();
			scopeType.BaseType.ShouldBe(typeof(object));
			scopeType.ContainsGenericParameters.ShouldBeFalse();
			ShouldHaveSurface(scopeType, new Dictionary<string, Type>
			{
				{ "State", typeof(object) },
				{ "Properties", typeof(ReadOnlyCollection<KeyValuePair<string, object>>) },
				{ "Annotations", typeof(LogAnnotations) }
			});
			ShouldHaveConstructor(scopeType, new[] { "state", "properties", "annotations" },
				new[] { typeof(object), typeof(IEnumerable<KeyValuePair<string, object>>), typeof(LogAnnotations) });
		}

		[Theory]
		[InlineData(null, null, null)]
		[InlineData("", "", " \t")]
		[InlineData(" \t", "\t", "")]
		[InlineData("producer.category", "raw {name}", "cached producer text")]
		[InlineData("producer.category", null, "cached producer text")]
		[InlineData("producer.category", "raw {name}", null)]
		public void ShouldRetainOptionalCategoryAndIndependentRawAndCachedText(string category, string raw, string formatted)
		{
			var contract = new EntryContract<object>();
			var entry = contract.Construct(null, EmptyPairs(), Frames(), categoryName: category,
				rawMessage: raw, formattedMessage: formatted);
			Read<string>(entry, "CategoryName").ShouldBe(category);
			Read<string>(entry, "RawMessage").ShouldBe(raw);
			Read<string>(entry, "FormattedMessage").ShouldBe(formatted);
			Read<object>(entry, "State").ShouldBeNull();
			ReadCommon<object>(entry, "OriginalState").ShouldBeNull();
			ReadCommon<Type>(entry, "StateType").ShouldBe(typeof(object));
			Read<Delegate>(entry, "ProducerFormatter").ShouldBeNull();
			ReadCommon<Delegate>(entry, "OriginalProducerFormatter").ShouldBeNull();
			Read<Exception>(entry, "Exception").ShouldBeNull();
			Read<LogContext>(entry, "NativeContext").ShouldBeNull();
			Read<LogAnnotations>(entry, "Annotations").ShouldBeNull();
			ReadPairs(entry).ShouldBeEmpty();
			Read<IList>(entry, "Scopes").Count.ShouldBe(0);
			ShouldHaveMetadata(entry);
		}

		[Fact]
		public void ShouldDistinguishAnAbsentEventIdentityFromAPresentDefaultValue()
		{
			var contract = new EntryContract<object>();
			var absent = contract.Construct(null, EmptyPairs(), Frames());
			var present = contract.Construct(null, EmptyPairs(), Frames(), eventId: default(EventId));
			Read<EventId?>(absent, "EventId").HasValue.ShouldBeFalse();
			ReadPairs(absent, "BridgeMetadata")[1].Value.ShouldBeNull();
			var captured = Read<EventId?>(present, "EventId");
			captured.HasValue.ShouldBeTrue();
			captured.Value.Id.ShouldBe(0);
			captured.Value.Name.ShouldBeNull();
			ShouldHaveMetadata(absent);
			ShouldHaveMetadata(present);
		}

		[Theory]
		[InlineData(-7, "producer.event")]
		[InlineData(0, "")]
		[InlineData(17, null)]
		[InlineData(int.MaxValue, " \t")]
		public void ShouldRetainBothEventIdentityFieldsWithoutNewContentValidation(int identifier, string name)
		{
			var contract = new EntryContract<object>();
			var entry = contract.Construct(null, EmptyPairs(), Frames(), eventId: new EventId(identifier, name));
			var captured = Read<EventId?>(entry, "EventId");
			captured.HasValue.ShouldBeTrue();
			captured.Value.Id.ShouldBe(identifier);
			captured.Value.Name.ShouldBe(name);
			ShouldHaveMetadata(entry);
		}

		[Fact]
		public void ShouldRetainDeclaredStateTypeOriginalDelegateExceptionAndIndependentCachedText()
		{
			var contract = new EntryContract<object>();
			var state = new MutablePayload { Value = "before" };
			var exception = new InvalidOperationException("Synthetic logged payload.");
			var formatterCalls = 0;
			var closureText = "initial closure";
			Func<object, Exception, string> formatter = (actualState, actualException) =>
			{
				formatterCalls++;
				actualState.ShouldBeSameAs(state);
				actualException.ShouldBeSameAs(exception);
				return closureText;
			};
			var entry = contract.Construct(state, EmptyPairs(), Frames(), producerFormatter: formatter,
				originalLevel: (LogLevels)9, formattedMessage: "already captured", exception: exception);
			Read<object>(entry, "State").ShouldBeSameAs(state);
			ReadCommon<object>(entry, "OriginalState").ShouldBeSameAs(state);
			ReadCommon<Type>(entry, "StateType").ShouldBe(typeof(object));
			Read<LogLevels>(entry, "OriginalLevel").ShouldBe((LogLevels)9);
			Read<Exception>(entry, "Exception").ShouldBeSameAs(exception);
			var retained = Read<Func<object, Exception, string>>(entry, "ProducerFormatter");
			retained.ShouldBeSameAs(formatter);
			ReadCommon<Delegate>(entry, "OriginalProducerFormatter").ShouldBeSameAs(formatter);
			Read<string>(entry, "FormattedMessage").ShouldBe("already captured");
			formatterCalls.ShouldBe(0);
			var uncachedEntry = contract.Construct(state, EmptyPairs(), Frames(), producerFormatter: formatter,
				rawMessage: "raw without cached text", formattedMessage: null, exception: exception);
			formatterCalls.ShouldBe(0);
			Read<Func<object, Exception, string>>(uncachedEntry, "ProducerFormatter").ShouldBeSameAs(formatter);
			ReadCommon<Delegate>(uncachedEntry, "OriginalProducerFormatter").ShouldBeSameAs(formatter);
			Read<string>(uncachedEntry, "RawMessage").ShouldBe("raw without cached text");
			Read<string>(uncachedEntry, "FormattedMessage").ShouldBeNull();
			formatterCalls.ShouldBe(0);
			state.Value = "after";
			closureText = "changed closure";
			((MutablePayload)ReadCommon<object>(entry, "OriginalState")).Value.ShouldBe("after");
			retained(state, exception).ShouldBe("changed closure");
			Read<string>(entry, "FormattedMessage").ShouldBe("already captured");
			Read<string>(uncachedEntry, "FormattedMessage").ShouldBeNull();
			formatterCalls.ShouldBe(1);
			ShouldHaveMetadata(entry);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(42)]
		public void ShouldRetainUnconstrainedValueStateWithoutRelyingOnBoxIdentity(int state)
		{
			var contract = new EntryContract<int>();
			var entry = contract.Construct(state, EmptyPairs(), Frames());
			Read<int>(entry, "State").ShouldBe(state);
			ReadCommon<object>(entry, "OriginalState").ShouldBe(state);
			ReadCommon<Type>(entry, "StateType").ShouldBe(typeof(int));
		}

		[Theory]
		[InlineData(null)]
		[InlineData(73)]
		public void ShouldRetainNullableValueStateAndItsDeclaredType(int? state)
		{
			var contract = new EntryContract<int?>();
			var entry = contract.Construct(state, EmptyPairs(), Frames());
			Read<int?>(entry, "State").ShouldBe(state);
			ReadCommon<object>(entry, "OriginalState").ShouldBe((object)state);
			ReadCommon<Type>(entry, "StateType").ShouldBe(typeof(int?));
		}

		[Fact]
		public void ShouldCopyValueStateWhileRetainingItsNestedReferenceAndExactTypedFormatter()
		{
			var contract = new EntryContract<ValuePayload>();
			var nested = new MutablePayload { Value = "before" };
			var state = new ValuePayload { Number = 17, Nested = nested };
			var formatterCalls = 0;
			Func<ValuePayload, Exception, string> formatter = (original, exception) =>
			{
				formatterCalls++;
				original.Number.ShouldBe(17);
				original.Nested.ShouldBeSameAs(nested);
				return original.Nested.Value;
			};
			var entry = contract.Construct(state, EmptyPairs(), Frames(), producerFormatter: formatter, formattedMessage: "cached");
			state.Number = 99;
			nested.Value = "after";
			var captured = Read<ValuePayload>(entry, "State");
			captured.Number.ShouldBe(17);
			captured.Nested.ShouldBeSameAs(nested);
			captured.Nested.Value.ShouldBe("after");
			var common = (ValuePayload)ReadCommon<object>(entry, "OriginalState");
			common.Number.ShouldBe(17);
			common.Nested.ShouldBeSameAs(nested);
			ReadCommon<Type>(entry, "StateType").ShouldBe(typeof(ValuePayload));
			var retained = Read<Func<ValuePayload, Exception, string>>(entry, "ProducerFormatter");
			retained.ShouldBeSameAs(formatter);
			ReadCommon<Delegate>(entry, "OriginalProducerFormatter").ShouldBeSameAs(formatter);
			formatterCalls.ShouldBe(0);
			retained(captured, null).ShouldBe("after");
			Read<string>(entry, "FormattedMessage").ShouldBe("cached");
			formatterCalls.ShouldBe(1);
			var defaultEntry = contract.Construct(default(ValuePayload), EmptyPairs(), Frames());
			Read<ValuePayload>(defaultEntry, "State").Number.ShouldBe(0);
			Read<ValuePayload>(defaultEntry, "State").Nested.ShouldBeNull();
			((ValuePayload)ReadCommon<object>(defaultEntry, "OriginalState")).Number.ShouldBe(0);
			((ValuePayload)ReadCommon<object>(defaultEntry, "OriginalState")).Nested.ShouldBeNull();
		}

		[Theory]
		[InlineData(null)]
		[InlineData(0)]
		[InlineData(17)]
		[InlineData(" \t")]
		public void ShouldRetainScalarOrAbsentExternalStateWithoutInventingPairsOrAnnotations(object state)
		{
			var contract = new ScopeContract();
			var scope = contract.Construct(state, EmptyPairs());
			Read<object>(scope, "State").ShouldBe(state);
			ReadPairs(scope).ShouldBeEmpty();
			Read<LogAnnotations>(scope, "Annotations").ShouldBeNull();
		}

		[Theory]
		[InlineData(1)]
		[InlineData(2)]
		[InlineData(4)]
		[InlineData(8)]
		[InlineData(16)]
		[InlineData(32)]
		[InlineData(9)]
		[InlineData(63)]
		public void ShouldRetainEachKnownBitAndCompositeNativeMasks(int mask)
		{
			var contract = new EntryContract<object>();
			var entry = contract.Construct(null, EmptyPairs(), Frames(), originalLevel: (LogLevels)mask);
			Read<LogLevels>(entry, "OriginalLevel").ShouldBe((LogLevels)mask);
			ReadPairs(entry, "BridgeMetadata")[2].Value.ShouldBe((LogLevels)mask);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		[InlineData(int.MinValue)]
		[InlineData(64)]
		[InlineData(65)]
		[InlineData(int.MaxValue)]
		public void ShouldRejectInvalidMasksBeforeAccessingEitherRequiredSequence(int mask)
		{
			var contract = new EntryContract<object>();
			var pairProbe = new SequenceProbe { RejectAccess = true };
			var frameProbe = new SequenceProbe { RejectAccess = true };
			var properties = new ObservedSequence<KeyValuePair<string, object>>(EmptyPairs(), pairProbe);
			var scopes = ObservedFrames(Frames(), frameProbe);
			ShouldRejectArgument<ArgumentOutOfRangeException>(() =>
				contract.Construct(null, properties, scopes, originalLevel: (LogLevels)mask), "originalLevel");
			pairProbe.Accesses.ShouldBe(0);
			frameProbe.Accesses.ShouldBe(0);
		}

		[Theory]
		[InlineData("properties")]
		[InlineData("scopes")]
		public void ShouldRejectEachMissingEntrySequenceBeforeAccessingTheOther(string missingArgument)
		{
			var contract = new EntryContract<object>();
			var pairProbe = new SequenceProbe { RejectAccess = true };
			var frameProbe = new SequenceProbe { RejectAccess = true };
			IEnumerable<KeyValuePair<string, object>> properties = new ObservedSequence<KeyValuePair<string, object>>(EmptyPairs(), pairProbe);
			var scopes = ObservedFrames(Frames(), frameProbe);
			if (missingArgument == "properties") properties = null;
			else scopes = null;
			ShouldRejectArgument<ArgumentNullException>(() => contract.Construct(null, properties, scopes), missingArgument);
			pairProbe.Accesses.ShouldBe(0);
			frameProbe.Accesses.ShouldBe(0);
		}

		[Fact]
		public void ShouldRejectMissingScopePropertiesWithoutInspectingItsState()
		{
			var contract = new ScopeContract();
			var state = new UninspectedPayload();
			ShouldRejectArgument<ArgumentNullException>(() => contract.Construct(state, null), "properties");
			state.Accesses.ShouldBe(0);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(1)]
		[InlineData(2)]
		public void ShouldRejectNullFramesIncludingATailWithoutReturningAnEntry(int nullPosition)
		{
			var contract = new EntryContract<object>();
			var scopeContract = new ScopeContract();
			var frame = scopeContract.Construct("frame", EmptyPairs());
			var frames = Frames(frame, frame, frame);
			frames.SetValue(null, nullPosition);
			var source = ObservedFrames(frames, new SequenceProbe());
			object returned = null;
			ShouldRejectArgument<ArgumentException>(() => returned = contract.Construct(null, EmptyPairs(), source), "scopes");
			returned.ShouldBeNull();
		}

		[Theory]
		[InlineData("Array")]
		[InlineData("List")]
		[InlineData("ReadOnly")]
		[InlineData("Iterator")]
		public void ShouldSnapshotOrderedMembershipBeforeFirstReadWithoutCloningNestedValues(string sourceShape)
		{
			var entryContract = new EntryContract<object>();
			var scopeContract = new ScopeContract();
			var nested = new MutablePayload { Value = "before" };
			var expected = new[]
			{
				default(KeyValuePair<string, object>), Pair("duplicate", nested), Pair("duplicate", null),
				Pair(null, nested), Pair("", nested), Pair(" \t", 17),
				Pair("ProphetsWay.CategoryName", "application category"), Pair("ProphetsWay.EventId", "application event"),
				Pair("ProphetsWay.OriginalLevel", 0), Pair("ProphetsWay.NativeContext", nested),
				Pair("ProphetsWay.Annotations", nested), Pair("ProphetsWay.Scopes", nested),
				Pair("ProphetsWay.CategoryName", "second application category")
			};
			var pairInput = PairInput(expected, sourceShape);
			var scope = scopeContract.Construct(nested, pairInput.Item1);
			var emptyScope = scopeContract.Construct(null, EmptyPairs());
			var frameInput = FrameInput(Frames(scope, emptyScope, scope), sourceShape);
			var entry = entryContract.Construct(pairInput.Item1, pairInput.Item1, frameInput.Item1,
				categoryName: "real category", eventId: new EventId(29, "real event"));
			pairInput.Item2();
			frameInput.Item2();
			nested.Value = "after";
			ShouldHavePairs(ReadPairs(entry), expected);
			ShouldHavePairs(ReadPairs(scope), expected);
			Read<object>(entry, "State").ShouldBeSameAs(pairInput.Item1);
			ReadCommon<object>(entry, "OriginalState").ShouldBeSameAs(pairInput.Item1);
			Read<object>(scope, "State").ShouldBeSameAs(nested);
			((MutablePayload)ReadPairs(entry)[1].Value).Value.ShouldBe("after");
			ShouldHaveFrames(Read<IList>(entry, "Scopes"), scope, emptyScope, scope);
			Read<LogAnnotations>(entry, "Annotations").ShouldBeNull();
			Read<LogAnnotations>(scope, "Annotations").ShouldBeNull();
			ShouldHaveMetadata(entry);
		}

		[Fact]
		public void ShouldKeepExternalAttachmentsAndFramesSeparateFromOriginalNativeOrigins()
		{
			var entryContract = new EntryContract<object>();
			var scopeContract = new ScopeContract();
			var empty = new LogAnnotations(Array.Empty<SensitivityLabel>());
			var external = Annotations("external", "external");
			var nativeScopeLabels = Annotations("native.scope", "native.scope");
			var nativeEntryLabels = Annotations("native.entry");
			var time = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);
			var native = NativeContext(new[]
			{
				NativeFrame(null, new[] { Pair("native", 1) }), NativeFrame(empty, EmptyPairs()),
				NativeFrame(nativeScopeLabels, new[] { Pair("native", 2) })
			}, nativeEntryLabels, time);
			var emptyFrame = scopeContract.Construct(null, EmptyPairs(), empty);
			var opaqueState = new object();
			var opaqueFrame = scopeContract.Construct(opaqueState, new[] { Pair("same", 1) }, external);
			var scalarFrame = scopeContract.Construct(7, new[] { Pair("same", 2) }, external);
			foreach (var entryAttachment in new[] { null, empty, external })
			{
				object entry;
				using (NativeLogger.BeginScope(Annotations("ambient.only"), new[] { Pair("ambient", 1) }))
				{
					entry = entryContract.Construct(null, new[] { Pair("same", 3) },
						Frames(emptyFrame, opaqueFrame, scalarFrame, opaqueFrame), annotations: entryAttachment, nativeContext: native);
					Read<LogAnnotations>(entry, "Annotations").ShouldBeSameAs(entryAttachment);
					ReadPairs(entry, "BridgeMetadata")[4].Value.ShouldBeSameAs(entryAttachment);
				}
				var frames = Read<IList>(entry, "Scopes");
				ShouldHaveFrames(frames, emptyFrame, opaqueFrame, scalarFrame, opaqueFrame);
				Read<object>(frames[0], "State").ShouldBeNull();
				Read<LogAnnotations>(frames[0], "Annotations").ShouldBeSameAs(empty);
				Read<LogAnnotations>(frames[0], "Annotations").LabelOccurrences.ShouldBeEmpty();
				Read<object>(frames[1], "State").ShouldBeSameAs(opaqueState);
				Read<object>(frames[2], "State").ShouldBe(7);
				ShouldHavePairs(ReadPairs(frames[1]), Pair("same", 1));
				ShouldHavePairs(ReadPairs(frames[2]), Pair("same", 2));
				ShouldHavePairs(ReadPairs(entry), Pair("same", 3));
				foreach (var position in new[] { 1, 2, 3 })
				{
					var attachment = Read<LogAnnotations>(frames[position], "Annotations");
					attachment.ShouldBeSameAs(external);
					attachment.LabelOccurrences.Select(label => label.Identifier).ShouldBe(new[] { "external", "external" });
				}
				Read<LogAnnotations>(entry, "Annotations").ShouldBeSameAs(entryAttachment);
				var capturedNative = Read<LogContext>(entry, "NativeContext");
				capturedNative.ShouldBeSameAs(native);
				capturedNative.EventTimestampUtc.ShouldBe(time);
				capturedNative.Scopes.Count.ShouldBe(3);
				ShouldHavePairs(capturedNative.Scopes[0].Properties, Pair("native", 1));
				ShouldHavePairs(capturedNative.Scopes[2].Properties, Pair("native", 2));
				capturedNative.Labels.EntryAnnotations.ShouldBeSameAs(nativeEntryLabels);
				capturedNative.Labels.ScopeAnnotations.ShouldBe(new[] { empty, nativeScopeLabels });
				capturedNative.Labels.Origins.Select(origin => origin.ScopeIndex).ShouldBe(new int?[] { 1, 1, null });
				capturedNative.Labels.Origins.Select(origin => origin.OccurrenceIndex).ShouldBe(new[] { 0, 1, 0 });
				capturedNative.Labels.Origins.Select(origin => origin.Label.Identifier)
					.ShouldBe(new[] { "native.scope", "native.scope", "native.entry" });
				capturedNative.Labels.EffectiveLabels.Select(label => label.Identifier).OrderBy(identifier => identifier, StringComparer.Ordinal)
					.ShouldBe(new[] { "native.entry", "native.scope" });
				ShouldHaveMetadata(entry);
			}
		}

		[Fact]
		public void ShouldNotInferAttachmentsFromAnnotationShapedStateOrApplicationKeys()
		{
			var entryContract = new EntryContract<LogAnnotations>();
			var scopeContract = new ScopeContract();
			var state = Annotations("not.implicitly.mapped", "not.implicitly.mapped");
			var properties = new[] { Pair("SensitivityLabel", state), Pair("ProphetsWay.Annotations", state) };
			var scope = scopeContract.Construct(state, properties);
			var entry = entryContract.Construct(state, properties, Frames(scope));
			Read<LogAnnotations>(entry, "State").ShouldBeSameAs(state);
			Read<object>(scope, "State").ShouldBeSameAs(state);
			Read<LogAnnotations>(entry, "Annotations").ShouldBeNull();
			Read<LogAnnotations>(scope, "Annotations").ShouldBeNull();
			ShouldHavePairs(ReadPairs(entry), properties);
			ShouldHavePairs(ReadPairs(scope), properties);
			ShouldHaveMetadata(entry);
		}

		[Fact]
		public void ShouldProtectEveryOwnedViewThroughCollectionInterfacesAndSyncRoot()
		{
			var entryContract = new EntryContract<object>();
			var scopeContract = new ScopeContract();
			var expected = new[] { Pair("duplicate", 1), Pair("duplicate", null), default(KeyValuePair<string, object>) };
			var scope = scopeContract.Construct("opaque", expected);
			var entry = entryContract.Construct(null, expected, Frames(scope, scope));
			Action verify = () =>
			{
				ShouldHavePairs(ReadPairs(entry), expected);
				ShouldHavePairs(ReadPairs(scope), expected);
				ShouldHaveFrames(Read<IList>(entry, "Scopes"), scope, scope);
				ShouldHaveMetadata(entry);
			};
			ShouldRejectWrites(ReadPairs(entry), verify);
			ShouldRejectWrites(ReadPairs(scope), verify);
			ShouldRejectWrites(ReadPairs(entry, "BridgeMetadata"), verify);
			ShouldRejectFrameWrites(Read<object>(entry, "Scopes"), verify);
			ShouldRejectFrameWrites(ReadPairs(entry, "BridgeMetadata")[5].Value, verify);
			verify();
		}

		[Theory]
		[InlineData("EntryProperties", "GetEnumerator")]
		[InlineData("EntryProperties", "MoveNext")]
		[InlineData("EntryProperties", "Current")]
		[InlineData("EntryProperties", "Dispose")]
		[InlineData("EntryScopes", "GetEnumerator")]
		[InlineData("EntryScopes", "MoveNext")]
		[InlineData("EntryScopes", "Current")]
		[InlineData("EntryScopes", "Dispose")]
		[InlineData("ScopeProperties", "GetEnumerator")]
		[InlineData("ScopeProperties", "MoveNext")]
		[InlineData("ScopeProperties", "Current")]
		[InlineData("ScopeProperties", "Dispose")]
		public void ShouldFailFiniteCaptureWithoutReturningAPrefixAtEverySequenceBoundary(string source, string failurePoint)
		{
			var probe = new SequenceProbe { FailurePoint = failurePoint };
			var state = new UninspectedPayload();
			var loggedException = new UninspectedException();
			var formatterCalls = 0;
			Func<object, Exception, string> formatter = (original, exception) =>
			{
				formatterCalls++;
				return "not needed";
			};
			var properties = new ObservedSequence<KeyValuePair<string, object>>(
				new[] { Pair("prefix", 1), Pair("tail", 2) }, probe);
			object returned = null;
			Action construct;
			if (source == "ScopeProperties")
			{
				var contract = new ScopeContract();
				construct = () => returned = contract.Construct(state, properties);
			}
			else
			{
				var contract = new EntryContract<object>();
				var frame = new ScopeContract().Construct("frame", EmptyPairs());
				var frames = Frames(frame, frame);
				var scopes = source == "EntryScopes" ? ObservedFrames(frames, probe) : frames;
				construct = () => returned = contract.Construct(state,
					source == "EntryProperties" ? (IEnumerable<KeyValuePair<string, object>>)properties : EmptyPairs(), scopes,
					producerFormatter: formatter, exception: loggedException);
			}
			var failure = Record.Exception(construct);
			failure.ShouldNotBeNull("A failed finite capture must not return a completed carrier.");
			probe.FailureWasReached.ShouldBeTrue();
			returned.ShouldBeNull();
			state.Accesses.ShouldBe(0);
			loggedException.Accesses.ShouldBe(0);
			formatterCalls.ShouldBe(0);
		}

		[Fact]
		public void ShouldNotInspectInvokeDisposeRecaptureOrReenumerateOriginalPayloadsOnCaptureOrRead()
		{
			var entryContract = new EntryContract<UninspectedPayload>();
			var scopeContract = new ScopeContract();
			var state = new UninspectedPayload();
			var exception = new UninspectedException();
			var pairProbe = new SequenceProbe();
			var frameProbe = new SequenceProbe();
			var expected = new[] { Pair("nested", state), Pair("exception", exception) };
			var pairs = new ObservedSequence<KeyValuePair<string, object>>(expected, pairProbe);
			var formatterCalls = 0;
			Func<UninspectedPayload, Exception, string> formatter = (original, loggedException) =>
			{
				Interlocked.Increment(ref formatterCalls);
				throw new InvalidOperationException("Synthetic formatter must not run implicitly.");
			};
			object entry;
			object scope;
			using (NativeLogger.BeginScope(Annotations("ambient.only"), new[] { Pair("ambient", 1) }))
			{
				var before = CaptureAmbient();
				scope = scopeContract.Construct(state, pairs);
				entry = entryContract.Construct(state, pairs, ObservedFrames(Frames(scope), frameProbe),
					producerFormatter: formatter, rawMessage: "raw", formattedMessage: "cached", exception: exception);
				var pairAccesses = pairProbe.Accesses;
				var frameAccesses = frameProbe.Accesses;
				pairProbe.RejectAccess = true;
				frameProbe.RejectAccess = true;
				for (var repetition = 0; repetition < 3; repetition++)
				{
					Read<UninspectedPayload>(entry, "State").ShouldBeSameAs(state);
					ReadCommon<object>(entry, "OriginalState").ShouldBeSameAs(state);
					ReadCommon<Type>(entry, "StateType").ShouldBe(typeof(UninspectedPayload));
					Read<Delegate>(entry, "ProducerFormatter").ShouldBeSameAs(formatter);
					ReadCommon<Delegate>(entry, "OriginalProducerFormatter").ShouldBeSameAs(formatter);
					Read<Exception>(entry, "Exception").ShouldBeSameAs(exception);
					Read<string>(entry, "RawMessage").ShouldBe("raw");
					Read<string>(entry, "FormattedMessage").ShouldBe("cached");
					Read<object>(scope, "State").ShouldBeSameAs(state);
					Read<LogAnnotations>(scope, "Annotations").ShouldBeNull();
					Read<LogAnnotations>(entry, "Annotations").ShouldBeNull();
					ReadPairs(entry, "BridgeMetadata")[4].Value.ShouldBeNull();
					Read<LogContext>(entry, "NativeContext").ShouldBeNull();
					ShouldHavePairs(ReadPairs(entry), expected);
					ShouldHavePairs(ReadPairs(scope), expected);
					ShouldHaveFrames(Read<IList>(entry, "Scopes"), scope);
					ShouldHaveMetadata(entry);
				}
				pairProbe.Accesses.ShouldBe(pairAccesses);
				frameProbe.Accesses.ShouldBe(frameAccesses);
				var after = CaptureAmbient();
				after.Scopes.Count.ShouldBe(before.Scopes.Count);
				for (var index = 0; index < before.Scopes.Count; index++)
				{
					ShouldHavePairs(after.Scopes[index].Properties, before.Scopes[index].Properties.ToArray());
					after.Scopes[index].Annotations.ShouldBeSameAs(before.Scopes[index].Annotations);
				}
			}
			Read<string>(entry, "FormattedMessage").ShouldBe("cached");
			Read<object>(scope, "State").ShouldBeSameAs(state);
			ShouldHavePairs(ReadPairs(scope), expected);
			state.Accesses.ShouldBe(0);
			exception.Accesses.ShouldBe(0);
			formatterCalls.ShouldBe(0);
		}

		[Fact]
		public async Task ShouldSupportConcurrentStableReadbackOfAllCapturedMembershipAndTypedViews()
		{
			var entryContract = new EntryContract<object>();
			var scopeContract = new ScopeContract();
			var state = new object();
			var labels = Annotations("repeated", "repeated");
			var pairs = new[] { Pair("duplicate", state), Pair("duplicate", null), default(KeyValuePair<string, object>) };
			var scope = scopeContract.Construct(state, pairs, labels);
			var formatterCalls = 0;
			Func<object, Exception, string> formatter = (original, exception) =>
			{
				Interlocked.Increment(ref formatterCalls);
				return "not the cache";
			};
			var entry = entryContract.Construct(state, pairs, Frames(scope, scope), producerFormatter: formatter,
				categoryName: "category", eventId: new EventId(12, "event"), formattedMessage: "cached", annotations: labels);
			var start = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
			var readers = Enumerable.Range(0, 8).Select(reader => Task.Run(async () =>
			{
				await start.Task;
				for (var repetition = 0; repetition < 16; repetition++)
				{
					Read<object>(entry, "State").ShouldBeSameAs(state);
					ReadCommon<object>(entry, "OriginalState").ShouldBeSameAs(state);
					ReadCommon<Type>(entry, "StateType").ShouldBe(typeof(object));
					Read<Delegate>(entry, "ProducerFormatter").ShouldBeSameAs(formatter);
					ReadCommon<Delegate>(entry, "OriginalProducerFormatter").ShouldBeSameAs(formatter);
					Read<string>(entry, "CategoryName").ShouldBe("category");
					var eventId = Read<EventId?>(entry, "EventId");
					eventId.HasValue.ShouldBeTrue();
					eventId.Value.Id.ShouldBe(12);
					eventId.Value.Name.ShouldBe("event");
					Read<LogLevels>(entry, "OriginalLevel").ShouldBe((LogLevels)9);
					Read<string>(entry, "FormattedMessage").ShouldBe("cached");
					Read<LogAnnotations>(entry, "Annotations").ShouldBeSameAs(labels);
					Read<LogAnnotations>(scope, "Annotations").LabelOccurrences.Select(label => label.Identifier)
						.ShouldBe(new[] { "repeated", "repeated" });
					ShouldHavePairs(ReadPairs(entry), pairs);
					ShouldHavePairs(ReadPairs(scope), pairs);
					ShouldHaveFrames(Read<IList>(entry, "Scopes"), scope, scope);
					ShouldHaveMetadata(entry);
				}
			}, TestContext.Current.CancellationToken)).ToArray();
			start.SetResult(true);
			await Task.WhenAll(readers);
			formatterCalls.ShouldBe(0);
		}

		private static void ShouldRejectArgument<TException>(Action action, string parameterName) where TException : ArgumentException
		{
			var exception = Should.Throw<TException>(action);
			exception.GetType().ShouldBe(typeof(TException));
			exception.ParamName.ShouldBe(parameterName);
		}

		private static KeyValuePair<string, object> Pair(string key, object value)
		{
			return new KeyValuePair<string, object>(key, value);
		}

		private static void ShouldHavePairs(IEnumerable<KeyValuePair<string, object>> actual, params KeyValuePair<string, object>[] expected)
		{
			actual.ShouldNotBeNull();
			var pairs = actual.ToArray();
			pairs.Select(pair => pair.Key).ShouldBe(expected.Select(pair => pair.Key));
			for (var index = 0; index < expected.Length; index++)
			{
				var value = expected[index].Value;
				if (value != null && value.GetType().IsValueType) pairs[index].Value.ShouldBe(value);
				else pairs[index].Value.ShouldBeSameAs(value);
			}
		}

		private static void ShouldHaveFrames(IList actual, params object[] expected)
		{
			actual.ShouldNotBeNull();
			actual.Count.ShouldBe(expected.Length);
			for (var index = 0; index < expected.Length; index++) actual[index].ShouldBeSameAs(expected[index]);
		}

		private static Tuple<IEnumerable<KeyValuePair<string, object>>, Action> PairInput(KeyValuePair<string, object>[] values, string shape)
		{
			var array = values.ToArray();
			var list = values.ToList();
			IEnumerable<KeyValuePair<string, object>> source;
			if (shape == "Array") source = array;
			else if (shape == "List") source = list;
			else if (shape == "ReadOnly") source = list.AsReadOnly();
			else source = new ObservedSequence<KeyValuePair<string, object>>(array, new SequenceProbe());
			return Tuple.Create(source, (Action)(() =>
			{
				for (var index = 0; index < array.Length; index++) array[index] = Pair("changed", 99);
				list.Clear();
				list.Add(Pair("changed", 99));
			}));
		}

		private static Tuple<object, Action> FrameInput(Array frames, string shape)
		{
			var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(RequiredType("LogBridgeScope")));
			foreach (var frame in frames) list.Add(frame);
			object source;
			if (shape == "Array") source = frames;
			else if (shape == "List") source = list;
			else if (shape == "ReadOnly") source = Invoke(() => list.GetType().GetMethod("AsReadOnly").Invoke(list, null));
			else source = ObservedFrames(frames, new SequenceProbe());
			return Tuple.Create(source, (Action)(() =>
			{
				for (var index = 0; index < frames.Length; index++) frames.SetValue(null, index);
				list.Clear();
			}));
		}

		private static object ObservedFrames(Array frames, SequenceProbe probe)
		{
			return Activator.CreateInstance(typeof(ObservedSequence<>).MakeGenericType(RequiredType("LogBridgeScope")), frames, probe);
		}

		private static LogAnnotations Annotations(params string[] labels)
		{
			return new LogAnnotations(labels.Select(identifier => new SensitivityLabel(identifier)));
		}

		private static LogScopeFrame NativeFrame(LogAnnotations annotations, IEnumerable<KeyValuePair<string, object>> properties)
		{
			var constructor = typeof(LogScopeFrame).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
				new[] { typeof(LogAnnotations), typeof(IEnumerable<KeyValuePair<string, object>>) }, null);
			constructor.ShouldNotBeNull();
			return (LogScopeFrame)Invoke(() => constructor.Invoke(new object[] { annotations, properties }));
		}

		private static LogContext NativeContext(IEnumerable<LogScopeFrame> scopes, LogAnnotations annotations, DateTimeOffset timestamp)
		{
			var constructor = typeof(LogContext).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
				new[] { typeof(IEnumerable<LogScopeFrame>), typeof(LogAnnotations), typeof(DateTimeOffset) }, null);
			constructor.ShouldNotBeNull();
			return (LogContext)Invoke(() => constructor.Invoke(new object[] { scopes, annotations, timestamp }));
		}

		private static LogContext CaptureAmbient()
		{
			var capture = typeof(LogScopeHandle).GetMethod("Capture", BindingFlags.NonPublic | BindingFlags.Static);
			capture.ShouldNotBeNull();
			return (LogContext)Invoke(() => capture.Invoke(null,
				new object[] { null, new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero) }));
		}

		private static void ShouldRejectFrameWrites(object view, Action verify)
		{
			var assertion = typeof(LogBridgeRepresentationTests).GetMethod(nameof(ShouldRejectWrites), BindingFlags.NonPublic | BindingFlags.Static)
				.MakeGenericMethod(RequiredType("LogBridgeScope"));
			Invoke(() => assertion.Invoke(null, new object[] { view, verify }));
		}

		private static void ShouldRejectWrites<T>(ReadOnlyCollection<T> view, Action verify)
		{
			var before = view.ToArray();
			var generic = (IList<T>)view;
			var untyped = (IList)view;
			var replacement = default(T);
			var mutations = new Action[]
			{
				() => generic.Add(replacement), () => generic.Clear(), () => generic[0] = replacement,
				() => generic.Insert(0, replacement), () => generic.Remove(view[0]), () => generic.RemoveAt(0),
				() => untyped.Add(replacement), () => untyped.Clear(), () => untyped[0] = replacement,
				() => untyped.Insert(0, replacement), () => untyped.Remove(view[0]), () => untyped.RemoveAt(0)
			};
			foreach (var mutation in mutations)
			{
				Should.Throw<NotSupportedException>(mutation);
				view.ShouldBe(before);
				verify();
			}
			var rootMutations = new List<Action>();
			var root = ((ICollection)view).SyncRoot;
			if (root is IList<T> genericRoot)
			{
				rootMutations.Add(() => genericRoot.Add(replacement));
				rootMutations.Add(() => genericRoot.Clear());
				rootMutations.Add(() => { if (genericRoot.Count > 0) genericRoot[0] = replacement; });
				rootMutations.Add(() => { if (genericRoot.Count > 0) genericRoot.RemoveAt(0); });
			}
			if (root is IList untypedRoot)
			{
				rootMutations.Add(() => untypedRoot.Add(replacement));
				rootMutations.Add(() => untypedRoot.Clear());
				rootMutations.Add(() => { if (untypedRoot.Count > 0) untypedRoot[0] = replacement; });
				rootMutations.Add(() => { if (untypedRoot.Count > 0) untypedRoot.RemoveAt(0); });
			}
			foreach (var mutation in rootMutations)
			{
				Record.Exception(mutation);
				view.ShouldBe(before);
				verify();
			}
			view.ShouldBe(before);
			verify();
		}

		private static Type RequiredType(string name)
		{
			var type = typeof(NativeLogger).Assembly.GetType("ProphetsWay.Utilities." + name, false, false);
			type.ShouldNotBeNull("Missing reviewed carrier: ProphetsWay.Utilities." + name + " in the real ProphetsWay.Logger assembly.");
			return type;
		}

		private static T Read<T>(object instance, string name)
		{
			var property = instance.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
			property.ShouldNotBeNull("Missing reviewed getter: " + name);
			return (T)Invoke(() => property.GetValue(instance, null));
		}

		private static T ReadCommon<T>(object instance, string name)
		{
			var property = RequiredType("LogBridgeEntry").GetProperty(name);
			property.ShouldNotBeNull("Missing reviewed common getter: " + name);
			return (T)Invoke(() => property.GetValue(instance, null));
		}

		private static ReadOnlyCollection<KeyValuePair<string, object>> ReadPairs(object instance, string name = "Properties")
		{
			var pairs = Read<ReadOnlyCollection<KeyValuePair<string, object>>>(instance, name);
			pairs.ShouldNotBeNull();
			return pairs;
		}

		private static KeyValuePair<string, object>[] EmptyPairs()
		{
			return Array.Empty<KeyValuePair<string, object>>();
		}

		private static Array Frames(params object[] frames)
		{
			var result = Array.CreateInstance(RequiredType("LogBridgeScope"), frames.Length);
			for (var index = 0; index < frames.Length; index++) result.SetValue(frames[index], index);
			return result;
		}

		private static string[] CommonParameterNames()
		{
			return new[] { "categoryName", "eventId", "originalLevel", "rawMessage", "formattedMessage",
				"exception", "properties", "scopes", "annotations", "nativeContext" };
		}

		private static Type[] CommonParameterTypes(Type scopeType)
		{
			return new[] { typeof(string), typeof(EventId?), typeof(LogLevels), typeof(string), typeof(string),
				typeof(Exception), typeof(IEnumerable<KeyValuePair<string, object>>),
				typeof(IEnumerable<>).MakeGenericType(scopeType), typeof(LogAnnotations), typeof(LogContext) };
		}

		private static void ShouldHaveConstructor(Type type, string[] names, Type[] types)
		{
			var constructor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.Where(candidate => !candidate.IsPrivate).ShouldHaveSingleItem();
			constructor.IsAssembly.ShouldBeTrue();
			constructor.GetParameters().Select(parameter => parameter.Name).ShouldBe(names);
			constructor.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe(types);
			constructor.GetParameters().ShouldAllBe(parameter => !parameter.IsOptional && !parameter.IsOut);
		}

		private static void ShouldHaveSurface(Type type, Dictionary<string, Type> properties, params string[] abstractNames)
		{
			type.IsPublic.ShouldBeTrue();
			type.IsClass.ShouldBeTrue();
			type.Assembly.ShouldBeSameAs(typeof(NativeLogger).Assembly);
			type.GetInterfaces().ShouldBeEmpty();
			var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
			var getters = new List<MethodInfo>();
			foreach (var expected in properties)
			{
				var property = type.GetProperty(expected.Key, flags);
				property.ShouldNotBeNull("Missing reviewed property: " + expected.Key);
				property.PropertyType.ShouldBe(expected.Value);
				property.GetIndexParameters().ShouldBeEmpty();
				property.GetSetMethod(true).ShouldBeNull();
				var getter = property.GetGetMethod();
				getter.ShouldNotBeNull();
				getter.IsStatic.ShouldBeFalse();
				getter.IsAbstract.ShouldBe(abstractNames.Contains(expected.Key));
				getters.Add(getter);
			}
			var accessibleMethods = type.GetMethods(flags).Where(method => !method.IsPrivate).ToArray();
			accessibleMethods.Length.ShouldBe(getters.Count);
			foreach (var getter in getters) accessibleMethods.ShouldContain(getter);
			type.GetFields(flags).ShouldAllBe(field => field.IsPrivate);
			type.GetNestedTypes(flags).ShouldAllBe(nested => nested.IsNestedPrivate);
			type.GetEvents(flags).ShouldBeEmpty();
		}

		private static void ShouldHaveMetadata(object entry)
		{
			var metadata = ReadPairs(entry, "BridgeMetadata");
			metadata.Select(pair => pair.Key).ShouldBe(new[] { "ProphetsWay.CategoryName", "ProphetsWay.EventId",
				"ProphetsWay.OriginalLevel", "ProphetsWay.NativeContext", "ProphetsWay.Annotations", "ProphetsWay.Scopes" });
			metadata[0].Value.ShouldBe(Read<string>(entry, "CategoryName"));
			var eventId = Read<EventId?>(entry, "EventId");
			if (eventId.HasValue)
			{
				metadata[1].Value.ShouldBeOfType<EventId>();
				var captured = (EventId)metadata[1].Value;
				captured.Id.ShouldBe(eventId.Value.Id);
				captured.Name.ShouldBe(eventId.Value.Name);
			}
			else metadata[1].Value.ShouldBeNull();
			metadata[2].Value.ShouldBeOfType<LogLevels>();
			metadata[2].Value.ShouldBe(Read<LogLevels>(entry, "OriginalLevel"));
			metadata[3].Value.ShouldBeSameAs(Read<LogContext>(entry, "NativeContext"));
			metadata[4].Value.ShouldBeSameAs(Read<LogAnnotations>(entry, "Annotations"));
			var frames = Read<IList>(entry, "Scopes");
			var metadataFrames = metadata[5].Value.ShouldBeAssignableTo<IList>();
			metadataFrames.Count.ShouldBe(frames.Count);
			for (var index = 0; index < frames.Count; index++) metadataFrames[index].ShouldBeSameAs(frames[index]);
		}

		private static object Invoke(Func<object> member)
		{
			try { return member(); }
			catch (TargetInvocationException exception)
			{
				ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
				throw;
			}
		}

		private sealed class EntryContract<TState>
		{
			private readonly ConstructorInfo _constructor;

			public EntryContract()
			{
				var entryType = RequiredType("LogBridgeEntry`1").MakeGenericType(typeof(TState));
				var parameters = new[] { typeof(TState), typeof(Func<TState, Exception, string>) }
					.Concat(CommonParameterTypes(RequiredType("LogBridgeScope"))).ToArray();
				_constructor = entryType.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null, parameters, null);
				_constructor.ShouldNotBeNull("Missing reviewed internal generic entry constructor.");
			}

			public object Construct(TState state, IEnumerable<KeyValuePair<string, object>> properties, object scopes,
				Func<TState, Exception, string> producerFormatter = null, string categoryName = null,
				EventId? eventId = null, LogLevels originalLevel = (LogLevels)9, string rawMessage = null,
				string formattedMessage = null, Exception exception = null, LogAnnotations annotations = null,
				LogContext nativeContext = null)
			{
				return Invoke(() => _constructor.Invoke(new object[] { state, producerFormatter, categoryName, eventId,
					originalLevel, rawMessage, formattedMessage, exception, properties, scopes, annotations, nativeContext }));
			}
		}

		private sealed class ScopeContract
		{
			private readonly ConstructorInfo _constructor;

			public ScopeContract()
			{
				_constructor = RequiredType("LogBridgeScope").GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
					null, new[] { typeof(object), typeof(IEnumerable<KeyValuePair<string, object>>), typeof(LogAnnotations) }, null);
				_constructor.ShouldNotBeNull("Missing reviewed internal scope constructor.");
			}

			public object Construct(object state, IEnumerable<KeyValuePair<string, object>> properties, LogAnnotations annotations = null)
			{
				return Invoke(() => _constructor.Invoke(new[] { state, properties, annotations }));
			}
		}

		private sealed class MutablePayload
		{
			public string Value { get; set; }
		}

		private struct ValuePayload
		{
			public int Number;
			public MutablePayload Nested;
		}

		private sealed class SequenceProbe
		{
			public int Accesses;
			public bool RejectAccess;
			public string FailurePoint;
			public bool FailureWasReached;

			public void Touch(string point, bool failureApplies = true)
			{
				Interlocked.Increment(ref Accesses);
				if (RejectAccess) throw new InvalidOperationException("Synthetic sequence must not be accessed here.");
				if (FailurePoint == point && failureApplies)
				{
					FailureWasReached = true;
					throw new InvalidOperationException("Synthetic finite sequence failure.");
				}
			}
		}

		private sealed class ObservedSequence<T> : IEnumerable<T>
		{
			private readonly T[] _values;
			private readonly SequenceProbe _probe;

			public ObservedSequence(T[] values, SequenceProbe probe)
			{
				_values = values;
				_probe = probe;
			}

			public IEnumerator<T> GetEnumerator()
			{
				_probe.Touch("GetEnumerator");
				return new ObservedEnumerator(_values, _probe);
			}

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			private sealed class ObservedEnumerator : IEnumerator<T>
			{
				private readonly T[] _values;
				private readonly SequenceProbe _probe;
				private int _position = -1;

				public ObservedEnumerator(T[] values, SequenceProbe probe)
				{
					_values = values;
					_probe = probe;
				}

				public T Current
				{
					get
					{
						_probe.Touch("Current", _position == _values.Length - 1);
						return _values[_position];
					}
				}

				object IEnumerator.Current => Current;

				public bool MoveNext()
				{
					_position++;
					_probe.Touch("MoveNext", _position == _values.Length);
					return _position < _values.Length;
				}

				public void Dispose() => _probe.Touch("Dispose");
				public void Reset() => throw new NotSupportedException();
			}
		}

		private sealed class UninspectedPayload : IEnumerable<KeyValuePair<string, object>>, IDisposable
		{
			public int Accesses;
			public object DangerousProperty => Fail();
			public override string ToString() => (string)Fail();
			public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => (IEnumerator<KeyValuePair<string, object>>)Fail();
			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
			public void Dispose() => Fail();

			private object Fail()
			{
				Interlocked.Increment(ref Accesses);
				throw new InvalidOperationException("Synthetic original payload must not be inspected or disposed.");
			}
		}

		private sealed class UninspectedException : Exception, IDisposable
		{
			public int Accesses;
			public override string Message => (string)Fail();
			public override string StackTrace => (string)Fail();
			public override IDictionary Data => (IDictionary)Fail();
			public override string ToString() => (string)Fail();
			public void Dispose() => Fail();

			private object Fail()
			{
				Interlocked.Increment(ref Accesses);
				throw new InvalidOperationException("Synthetic logged exception must not be inspected or disposed.");
			}
		}
	}
}