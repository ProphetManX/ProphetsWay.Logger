using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using Moq;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class TextRenderingTests
	{
		[Theory]
		[InlineData("")]
		[InlineData("fr-FR")]
		[InlineData("tr-TR")]
		public void ShouldRenderSupportedScalarsInvariantly(string cultureName)
		{
			var previousCulture = CultureInfo.CurrentCulture;
			var previousUiCulture = CultureInfo.CurrentUICulture;
			try
			{
				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
				CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(cultureName);
				var destination = new ScalarProbe();
				var samples = new[]
				{
					Scalar(null, null), Scalar((int?)null, null), Scalar((int?)42, "42"),
					Scalar("", ""), Scalar("  \t", "  \t"), Scalar("null", "null"), Scalar('\ud800', "\ud800"),
					Scalar(true, "True"), Scalar(false, "False"), Scalar((sbyte)-128, "-128"), Scalar((byte)255, "255"),
					Scalar(short.MinValue, "-32768"), Scalar(ushort.MaxValue, "65535"),
					Scalar(int.MinValue, "-2147483648"), Scalar(uint.MaxValue, "4294967295"),
					Scalar(long.MinValue, "-9223372036854775808"), Scalar(ulong.MaxValue, "18446744073709551615"),
					Scalar(0, "0"), Scalar(new IntPtr(-42), "-42"), Scalar(new UIntPtr(42), "42"),
					Scalar(0m, "0"), Scalar(-1234.500m, "-1234.500"),
					Scalar(decimal.MaxValue, "79228162514264337593543950335"),
					Scalar(Guid.Parse("12345678-abcd-1234-9876-123456789abc"), "12345678-abcd-1234-9876-123456789abc"),
					Scalar(TimeSpan.FromTicks(-1234567890123), "-1.10:17:36.7890123"),
					Scalar(TimeSpan.Zero, "00:00:00"), Scalar(SampleFlags.First, "First"),
					Scalar(SampleFlags.First | SampleFlags.Second, "First, Second"), Scalar((SampleFlags)8, "8"),
					Scalar((SampleFlags)(-1), "-1"), Scalar((UnsignedSample)ulong.MaxValue, "18446744073709551615")
				};
				foreach (var sample in samples)
					destination.Format(sample.Key).ShouldBe(sample.Value);
				foreach (var number in new[] { 0f, -0f, -1.25f, float.Epsilon, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
					destination.Format(number).ShouldBe(number.ToString("R", CultureInfo.InvariantCulture));
				foreach (var number in new[] { 0d, -0d, -1.25d, double.Epsilon, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
					destination.Format(number).ShouldBe(number.ToString("R", CultureInfo.InvariantCulture));
				foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Local, DateTimeKind.Utc })
				{
					var value = new DateTime(2024, 2, 29, 12, 34, 56, kind).AddTicks(1234567);
					destination.Format(value).ShouldBe(value.ToString("O", CultureInfo.InvariantCulture));
				}
				var offsetTime = new DateTimeOffset(2024, 2, 29, 12, 34, 56, TimeSpan.FromHours(5.5)).AddTicks(1234567);
				destination.Format(offsetTime).ShouldBe("2024-02-29T12:34:56.1234567+05:30");
			}
			finally
			{
				CultureInfo.CurrentCulture = previousCulture;
				CultureInfo.CurrentUICulture = previousUiCulture;
			}
		}

		[Fact]
		public void ShouldRenderUnsupportedValuesWithoutInspectingThem()
		{
			var value = new InspectionCanary();
			var scalar = new ScalarProbe();
			scalar.Format(value).ShouldBe("[no formatter: InspectionCanary]");
			scalar.Format(new Dictionary<string, object> { { "not-native", value } }).ShouldBe("[no formatter: Dictionary`2]");
			var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("M4A" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.Run);
			var type = assembly.DefineDynamicModule("Values").DefineType("M4Type\"\\\n\u0085\u2028]", TypeAttributes.Public).CreateTypeInfo().AsType();
			var unusual = Activator.CreateInstance(type);
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(null, new[] { Pair("opaque", value), Pair("type", unusual) }))
			{
				var destination = new TextProbe();
				destination.Log(LogLevels.InformationOnly, "unsupported");
				AssertRecord(destination.Data, LogLevels.InformationOnly, "\"unsupported\"",
					" | entryLabels=null | scopes=[{labels=null,properties=[(\"opaque\",\"[no formatter: InspectionCanary]\"),(\"type\"," + Token("[no formatter: " + type.Name + "]") + ")]}]");
				destination.Data.Values.Count.ShouldBe(2);
				ReferenceEquals(destination.Data.Values[0], value).ShouldBeTrue();
			}
			value.Inspections.ShouldBe(0);
			value.Disposals.ShouldBe(0);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldEscapeEveryRenderedComponentWithoutCreatingAnotherRecord(bool typed)
		{
			var controls = new string(Enumerable.Range(0, 160).Select(number => (char)number).Where(char.IsControl).ToArray());
			var hostile = "\\n\n\r\t\" | scopes=[{2030-01-01 :: Critical: forged}" + controls + "\u2028\u2029\u00e9e\u0301\ud800";
			const string label = "label\"\\],|entryLabels=[";
			var annotations = Labels(label, label);
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(annotations, new[] { Pair(hostile, hostile) }))
			{
				var data = new Observation();
				if (typed)
				{
					routes.AddTyped(new TypedText<object>(data));
					Utilities.Logger.LogAnnotated<object>(annotations, (LogLevels)9, hostile, hostile);
				}
				else
				{
					routes.Add(new TextProbe(data));
					Utilities.Logger.LogAnnotated(annotations, (LogLevels)9, message: hostile);
				}
				var attachment = "[" + Token(label) + "," + Token(label) + "]";
				AssertRecord(data, (LogLevels)9, Token(hostile),
					(typed ? " | metadata=" + Token(hostile) : "") + " | entryLabels=" + attachment +
					" | scopes=[{labels=" + attachment + ",properties=[(" + Token(hostile) + "," + Token(hostile) + ")]}]");
				data.Printed[0].ShouldContain("\\\\n\\n\\r\\t\\\" | scopes=", Case.Sensitive);
				data.Printed[0].ShouldContain("\\u0000", Case.Sensitive);
				data.Printed[0].ShouldContain("\\u0085", Case.Sensitive);
				data.Printed[0].ShouldContain("\\u2028\\u2029\u00e9e\u0301\ud800", Case.Sensitive);
				data.Values.Count.ShouldBe(typed ? 2 : 1);
				data.Values.All(item => ReferenceEquals(item, hostile)).ShouldBeTrue();
				data.MassageCalls.ShouldBe(1);
			}
		}

		[Fact]
		public void ShouldPreserveEveryScopeFrameAndPropertyOccurrence()
		{
			var mutable = new MutableValue { Text = "before" };
			var source = new List<KeyValuePair<string, object>>
			{
				Pair(null, null), Pair("", ""), Pair(" ", 0), Pair("dup", "first"), Pair("dup", mutable)
			};
			var sequence = new SingleReadPairs(source);
			var repeated = Labels("scope", "scope");
			var data = new Observation { Formatter = item => item is MutableValue ? ((MutableValue)item).Text : DefaultValue(item) };
			var observer = new RawProbe();
			LogScopeHandle callbackScope = null;
			observer.OnEntry = (context, message) =>
			{
				source.Clear(); source.Add(Pair("late", "absent")); mutable.Text = "after";
				callbackScope = Utilities.Logger.BeginScope(Labels("callback-only"), new[] { Pair("late", "absent") });
			};
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(null))
			using (Utilities.Logger.BeginScope(Labels(), sequence))
			using (Utilities.Logger.BeginScope(repeated))
			using (Utilities.Logger.BeginScope(repeated))
			{
				routes.Add(observer);
				routes.Add(new TextProbe(data));
				var entries = new[] { null, Labels(), Labels("entry", "entry") };
				var entryTokens = new[] { "null", "[]", "[\"entry\",\"entry\"]" };
				for (var index = 0; index < entries.Length; index++)
				{
					try { Utilities.Logger.LogAnnotated(entries[index], LogLevels.InformationOnly, message: "frames"); }
					finally { if (callbackScope != null) { callbackScope.Dispose(); callbackScope = null; } }
					AssertRecordAt(data, index, LogLevels.InformationOnly, "\"frames\"", " | entryLabels=" + entryTokens[index] +
						" | scopes=[{labels=null,properties=[]},{labels=[],properties=[(null,null),(\"\",\"\"),(\" \",\"0\"),(\"dup\",\"first\"),(\"dup\",\"after\")]},{labels=[\"scope\",\"scope\"],properties=[]},{labels=[\"scope\",\"scope\"],properties=[]}]");
					var context = data.Contexts[index];
					context.Scopes.Count.ShouldBe(4);
					context.Scopes[1].Properties.Select(pair => pair.Key).ShouldBe(new[] { null, "", " ", "dup", "dup" });
					ReferenceEquals(context.Scopes[1].Properties[4].Value, mutable).ShouldBeTrue();
					context.Labels.Origins.Select(origin => origin.ScopeIndex ?? -1).ShouldBe(index == 2
						? new[] { 1, 1, 2, 2, -1, -1 } : new[] { 1, 1, 2, 2 });
				}
				data.Printed.Count.ShouldBe(3);
				data.Values.Count.ShouldBe(15);
			}
			sequence.Reads.ShouldBe(1);
			data.Contexts[0].Scopes.Count.ShouldBe(4);
			data.Contexts[0].Scopes[1].Properties.Count.ShouldBe(5);
		}

		[Theory]
		[InlineData("null")]
		[InlineData("default")]
		[InlineData("annotations")]
		[InlineData("dictionary")]
		public void ShouldSeparateTypedMetadataFromAnnotations(string kind)
		{
			switch (kind)
			{
				case "null": AssertTypedMetadata<string>(null, "null"); break;
				case "default": AssertTypedMetadata(0, "\"0\""); break;
				case "annotations": AssertTypedMetadata(Labels("metadata-not-a-label"), "\"[no formatter: LogAnnotations]\""); break;
				default: AssertTypedMetadata(new Dictionary<string, object> { { "not-a-scope", 7 } }, "\"[no formatter: Dictionary`2]\""); break;
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldShareOneUtcTimestampAcrossRecipients(bool typed)
		{
			var first = new Observation();
			var second = new Observation();
			var eligibility = new EligibilityProbe();
			var ordinary = new RawProbe();
			var generic = new TypedRawProbe<object>();
			var previous = CultureInfo.CurrentCulture;
			DateTimeOffset callbackAfterAdvance = default(DateTimeOffset);
			first.BeforeRender = (context, message) =>
			{
				callbackAfterAdvance = AdvanceClock(DateTimeOffset.UtcNow);
				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
			};
			using (var routes = new Routes<object>())
			{
				try
				{
					if (typed)
					{
						routes.AddTyped(eligibility); routes.AddTyped(new TypedText<object>(first)); routes.AddTyped(generic); routes.AddTyped(new TypedText<object>(second));
					}
					else
					{
						routes.Add(eligibility); routes.Add(new TextProbe(first)); routes.Add(ordinary); routes.Add(new TextProbe(second));
					}
					var before = DateTimeOffset.UtcNow;
					if (typed) Utilities.Logger.Info<object>("same call", 7); else Utilities.Logger.Info("same call");
					var after = DateTimeOffset.UtcNow;
					var rawContext = typed ? generic.Contexts.Single() : ordinary.Contexts.Single();
					var timestamp = rawContext.EventTimestampUtc;
					timestamp.Offset.ShouldBe(TimeSpan.Zero);
					timestamp.ShouldBeInRange(before, after);
					(timestamp < eligibility.EligibilityFinished).ShouldBeTrue();
					eligibility.Contexts.Single().EventTimestampUtc.ShouldBe(timestamp);
					eligibility.LegacyCalls.ShouldBe(0);
					(timestamp < callbackAfterAdvance).ShouldBeTrue();
					first.Contexts.Single().EventTimestampUtc.ShouldBe(timestamp);
					second.Contexts.Single().EventTimestampUtc.ShouldBe(timestamp);
					var suffix = (typed ? " | metadata=\"7\"" : "") + " | entryLabels=null | scopes=[]";
					AssertRecord(first, LogLevels.InformationOnly, "\"same call\"", suffix);
					AssertRecord(second, LogLevels.InformationOnly, "\"same call\"", suffix);
					first.Printed.Single().ShouldBe(second.Printed.Single());
				}
				finally { CultureInfo.CurrentCulture = previous; }
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldRefreshDirectContextTimeWithoutChangingItsFacts(bool typed)
		{
			var capture = new RawProbe();
			var data = new Observation();
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(null))
			using (Utilities.Logger.BeginScope(Labels("scope"), new[] { Pair("key", "value") }))
			{
				routes.Add(capture);
				Utilities.Logger.LogAnnotated(Labels("entry"), LogLevels.InformationOnly, message: "capture");
				var retained = capture.Contexts.Single();
				var originalTime = retained.EventTimestampUtc;
				var ordinary = typed ? null : new TextProbe(data);
				var generic = typed ? new TypedText<object>(data) : null;
				for (var index = 0; index < 2; index++)
				{
					AdvanceClock(index == 0 ? originalTime : data.Contexts[index - 1].EventTimestampUtc);
					var before = DateTimeOffset.UtcNow;
					if (typed) generic.LogWithContext(retained, LogLevels.InformationOnly, 0, "direct");
					else ordinary.LogWithContext(retained, LogLevels.InformationOnly, "direct");
					var after = DateTimeOffset.UtcNow;
					var selected = data.Contexts[index];
					selected.EventTimestampUtc.ShouldBeInRange(before, after);
					selected.EventTimestampUtc.Offset.ShouldBe(TimeSpan.Zero);
					retained.EventTimestampUtc.ShouldBe(originalTime);
					selected.Scopes.ShouldBe(retained.Scopes);
					selected.Labels.EntryAnnotations.LabelOccurrences.Select(label => label.Identifier).ShouldBe(new[] { "entry" });
					AssertRecordAt(data, index, LogLevels.InformationOnly, "\"direct\"", (typed ? " | metadata=\"0\"" : "") +
						" | entryLabels=[\"entry\"] | scopes=[{labels=null,properties=[]},{labels=[\"scope\"],properties=[(\"key\",\"value\")]}]");
				}
				data.Printed.Count.ShouldBe(2);
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldKeepOuterTimeDuringNestedAndPublicReentrantCalls(bool typed)
		{
			var first = new Observation();
			var later = new Observation();
			var direct = new Observation();
			DateTimeOffset nestedBefore = default(DateTimeOffset), nestedAfter = default(DateTimeOffset);
			DateTimeOffset directBefore = default(DateTimeOffset), directAfter = default(DateTimeOffset);
			using (var routes = new Routes<object>())
			{
				var directOrdinary = typed ? null : new TextProbe(direct);
				var directTyped = typed ? new TypedText<object>(direct) : null;
				first.BeforeRender = (context, message) =>
				{
					if (message != "outer") return;
					AdvanceClock(DateTimeOffset.UtcNow);
					using (Utilities.Logger.BeginScope(Labels("nested")))
					{
						nestedBefore = DateTimeOffset.UtcNow;
						if (typed) Utilities.Logger.Info<object>("nested", 1); else Utilities.Logger.Info("nested");
						nestedAfter = DateTimeOffset.UtcNow;
					}
					AdvanceClock(DateTimeOffset.UtcNow);
					directBefore = DateTimeOffset.UtcNow;
					if (typed) directTyped.LogWithContext(context, LogLevels.InformationOnly, 2, "reentry");
					else directOrdinary.LogWithContext(context, LogLevels.InformationOnly, "reentry");
					directAfter = DateTimeOffset.UtcNow;
				};
				if (typed) { routes.AddTyped(new TypedText<object>(first)); routes.AddTyped(new TypedText<object>(later)); }
				else { routes.Add(new TextProbe(first)); routes.Add(new TextProbe(later)); }
				var before = DateTimeOffset.UtcNow;
				if (typed) Utilities.Logger.Info<object>("outer", 0); else Utilities.Logger.Info("outer");
				var after = DateTimeOffset.UtcNow;
				var outer = first.Contexts[0];
				outer.EventTimestampUtc.ShouldBeInRange(before, after);
				first.Contexts[1].EventTimestampUtc.ShouldBeInRange(nestedBefore, nestedAfter);
				direct.Contexts.Single().EventTimestampUtc.ShouldBeInRange(directBefore, directAfter);
				later.Contexts[0].EventTimestampUtc.ShouldBe(first.Contexts[1].EventTimestampUtc);
				later.Contexts[1].EventTimestampUtc.ShouldBe(outer.EventTimestampUtc);
				outer.Scopes.ShouldBeEmpty();
				later.Contexts[1].Scopes.ShouldBeEmpty();
				first.Contexts[1].Scopes.Single().Annotations.LabelOccurrences.Single().Identifier.ShouldBe("nested");
				direct.Contexts.Single().Scopes.ShouldBeEmpty();
				first.Printed.Count.ShouldBe(2); later.Printed.Count.ShouldBe(2); direct.Printed.Count.ShouldBe(1);
				ReadTime(first.Printed[1]).ShouldBe(outer.EventTimestampUtc);
				ReadTime(later.Printed[1]).ShouldBe(outer.EventTimestampUtc);
				ReadTime(direct.Printed[0]).ShouldBe(direct.Contexts[0].EventTimestampUtc);
			}
		}

		[Theory]
		[InlineData(false, "registration label")]
		[InlineData(true, "registration label")]
		[InlineData(false, "intrinsic label")]
		[InlineData(true, "intrinsic label")]
		[InlineData(false, "registration mask")]
		[InlineData(true, "registration mask")]
		[InlineData(false, "intrinsic mask")]
		[InlineData(true, "intrinsic mask")]
		[InlineData(false, "reject all")]
		[InlineData(true, "reject all")]
		public void ShouldWithholdFormattingUntilEveryGatePermits(bool typed, string gate)
		{
			var data = new Observation { Failure = "formatter" };
			var value = new InspectionCanary();
			var deny = new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { new SensitivityLabel("private") });
			var allow = new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]);
			var settings = new DestinationRegistrationSettings(true, gate == "registration mask" ? LogLevels.Critical : LogLevels.Trace,
				gate == "registration label" ? deny : allow);
			var mask = gate == "reject all" ? (LogLevels)0 : gate == "intrinsic mask" ? LogLevels.Critical : LogLevels.Trace;
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = reports.Add;
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(Labels("private"), new[] { Pair("value", value) }))
			{
				Utilities.Logger.DispatchFailed += observer;
				try
				{
					if (typed)
					{
						var destination = new TypedText<object>(data, mask) { LabelPolicy = gate == "intrinsic label" ? deny : allow };
						routes.AddTyped(destination, settings);
						Utilities.Logger.LogAnnotated<object>(null, (LogLevels)9, value, "denied");
					}
					else
					{
						var destination = new TextProbe(data, mask) { LabelPolicy = gate == "intrinsic label" ? deny : allow };
						routes.Add(destination, settings);
						Utilities.Logger.LogAnnotated(null, (LogLevels)9, message: "denied");
					}
					data.Contexts.ShouldBeEmpty(); data.Values.ShouldBeEmpty(); data.Printed.ShouldBeEmpty();
					data.MassageCalls.ShouldBe(0); data.PrintAttempts.ShouldBe(0);
					value.Inspections.ShouldBe(0); reports.ShouldBeEmpty();
				}
				finally { Utilities.Logger.DispatchFailed -= observer; }
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldEscapeExplicitFormatterOutputAndKeepItRecipientLocal(bool typed)
		{
			var value = new InspectionCanary();
			var first = new Observation { Formatter = item => ReferenceEquals(item, value) ? "first\"\n\\n" : DefaultValue(item) };
			var second = new Observation { Formatter = item => ReferenceEquals(item, value) ? null : DefaultValue(item) };
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(Labels("not-formatted"), new[] { Pair("key", value), Pair("key", value), Pair("number", 42) }))
			{
				if (typed) { routes.AddTyped(new TypedText<object>(first)); routes.AddTyped(new TypedText<object>(second)); }
				else { routes.Add(new TextProbe(first)); routes.Add(new TextProbe(second)); }
				if (typed) Utilities.Logger.Info<object>("message", value); else Utilities.Logger.Info("message");
				AssertRecord(first, LogLevels.InformationOnly, "\"message\"", (typed ? " | metadata=\"first\\\"\\n\\\\n\"" : "") +
					" | entryLabels=null | scopes=[{labels=[\"not-formatted\"],properties=[(\"key\",\"first\\\"\\n\\\\n\"),(\"key\",\"first\\\"\\n\\\\n\"),(\"number\",\"42\")]}]");
				AssertRecord(second, LogLevels.InformationOnly, "\"message\"", (typed ? " | metadata=null" : "") +
					" | entryLabels=null | scopes=[{labels=[\"not-formatted\"],properties=[(\"key\",null),(\"key\",null),(\"number\",\"42\")]}]");
				foreach (var data in new[] { first, second })
				{
					data.Values.Count.ShouldBe(typed ? 4 : 3);
					data.Values.Count(item => ReferenceEquals(item, value)).ShouldBe(typed ? 3 : 2);
					data.Values.Count(item => item is int && (int)item == 42).ShouldBe(1);
					data.MassageCalls.ShouldBe(1);
				}
			}
			value.Inspections.ShouldBe(0); value.Disposals.ShouldBe(0);
		}

		[Fact]
		public void ShouldKeepOriginalRawAndEventValues()
		{
			const string message = "raw\n\\n\"\t";
			var exception = NestedException();
			var value = new InspectionCanary();
			var ordinary = new RawProbe();
			var generic = new TypedRawProbe<object>();
			var ordinaryEvents = new List<EventDestination.LoggerEventArgs>();
			var typedEvents = new List<GenericEventDestination<object>.LoggerEventArgs>();
			var ordinaryEvent = new EventDestination(LogLevels.Trace);
			var typedEvent = new GenericEventDestination<object>(LogLevels.Trace);
			ordinaryEvent.LoggingEvent += (sender, args) => ordinaryEvents.Add(args);
			typedEvent.LoggingEvent += (sender, args) => typedEvents.Add(args);
			var text = new TextProbe();
			var typedText = new TypedText<object>(new Observation());
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(null, new[] { Pair("raw", value) }))
			{
				routes.Add(text); routes.Add(ordinary); routes.Add(ordinaryEvent);
				routes.AddTyped(typedText); routes.AddTyped(generic); routes.AddTyped(typedEvent);
				var before = DateTime.Now;
				Utilities.Logger.LogAnnotated(null, (LogLevels)9, message, exception);
				Utilities.Logger.LogAnnotated<object>(null, (LogLevels)9, value, message, exception);
				var after = DateTime.Now;
				ordinary.Messages.Single().ShouldBeSameAs(message);
				ordinary.Exceptions.Single().ShouldBeSameAs(exception);
				generic.Metadata.Single().ShouldBeSameAs(value);
				generic.Exceptions.Single().ShouldBeSameAs(exception);
				var ordinaryArgs = ordinaryEvents.Single();
				var typedArgs = typedEvents.Single();
				ordinaryArgs.RawMessage.ShouldBeSameAs(message); typedArgs.RawMessage.ShouldBeSameAs(message);
				ordinaryArgs.Exception.ShouldBeSameAs(exception); typedArgs.Exception.ShouldBeSameAs(exception);
				typedArgs.Metadata.ShouldBeSameAs(value);
				foreach (var rendered in new[] { ordinaryArgs.Message, typedArgs.Message })
				{
					rendered.ShouldContain(message, Case.Sensitive);
					rendered.ShouldContain(exception.StackTrace, Case.Sensitive);
					rendered.ShouldContain(exception.InnerException.StackTrace, Case.Sensitive);
				}
				foreach (var timestamp in new[] { ordinaryArgs.Timestamp, typedArgs.Timestamp })
				{
					timestamp.Kind.ShouldBe(DateTimeKind.Local); timestamp.ShouldBeInRange(before, after);
				}
				ordinaryArgs.Context.EventTimestampUtc.ShouldBe(text.Data.Contexts.Single().EventTimestampUtc);
				typedArgs.Context.EventTimestampUtc.ShouldBe(typedText.Data.Contexts.Single().EventTimestampUtc);
				ReferenceEquals(ordinaryArgs.Context.Scopes.Single().Properties.Single().Value, value).ShouldBeTrue();
				ReferenceEquals(typedArgs.Context.Scopes.Single().Properties.Single().Value, value).ShouldBeTrue();
				text.Data.Values.Count.ShouldBe(1); typedText.Data.Values.Count.ShouldBe(2);
			}
			value.Inspections.ShouldBe(0); value.Disposals.ShouldBe(0);
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldPreserveExceptionDetailsAtEveryValidMask(bool typed)
		{
			var exception = NestedException();
			const string message = " context\"\n\\n\u2028 ";
			using (var routes = new Routes<object>())
			{
				var data = new Observation();
				var ordinary = typed ? null : new TextProbe(data);
				var generic = typed ? new TypedText<object>(data) : null;
				for (var mask = 1; mask <= 63; mask++)
				{
					if (typed) generic.Log((LogLevels)mask, null, message, exception); else ordinary.Log((LogLevels)mask, message, exception);
					var record = data.Printed.Last();
					foreach (var detail in new[] { message, exception.Message, exception.StackTrace, exception.InnerException.Message, exception.InnerException.StackTrace })
						record.ShouldContain(Escape(detail), Case.Sensitive);
					AssertSingleLine(record);
					data.Exceptions.Last().ShouldBeSameAs(exception);
					record.ShouldContain(" :: " + ((LogLevels)mask).ToString("G").PadLeft(12) + ":  ", Case.Sensitive);
				}
				data.Printed.Count.ShouldBe(63); data.MassageCalls.ShouldBe(63);
				foreach (var absent in new[] { null, "", " ", "null" })
				{
					if (typed) generic.Log(LogLevels.InformationOnly, null, absent); else ordinary.Log(LogLevels.InformationOnly, absent);
					AssertRecordAt(data, data.Printed.Count - 1, LogLevels.InformationOnly, Token(absent),
						(typed ? " | metadata=null" : "") + " | entryLabels=null | scopes=[]");
				}
			}
		}

		[Theory]
		[InlineData(false, "massage", false)]
		[InlineData(false, "massage", true)]
		[InlineData(true, "massage", false)]
		[InlineData(true, "massage", true)]
		[InlineData(false, "formatter", false)]
		[InlineData(false, "formatter", true)]
		[InlineData(true, "formatter", false)]
		[InlineData(true, "formatter", true)]
		[InlineData(false, "print", false)]
		[InlineData(false, "print", true)]
		[InlineData(true, "print", false)]
		[InlineData(true, "print", true)]
		public void ShouldContinueAfterRenderingOrPrintFailureAndReportSafely(bool typed, string boundary, bool failingFirst)
		{
			var failed = new Observation { Failure = boundary };
			var good = new Observation();
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = report => { reports.Add(report); throw new Exception("reporter-private"); };
			var previousError = Console.Error;
			using (var error = new StringWriter())
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(Labels("label-private"), new[] { Pair("key-private", "value-private") }))
			{
				Console.SetError(error);
				Utilities.Logger.DispatchFailed += observer;
				try
				{
					foreach (var data in failingFirst ? new[] { failed, good } : new[] { good, failed })
					{
						if (typed) routes.AddTyped(new TypedText<object>(data)); else routes.Add(new TextProbe(data));
					}
					var failure = Should.Throw<LogDispatchException>(() =>
					{
						if (typed) Utilities.Logger.Info<object>("message-private", "metadata-private"); else Utilities.Logger.Info("message-private");
					});
					good.Printed.Count.ShouldBe(1);
					good.PrintAttempts.ShouldBe(1);
					failed.PrintAttempts.ShouldBe(boundary == "print" ? 1 : 0);
					failed.Printed.Count.ShouldBe(boundary == "print" ? 1 : 0);
					var report = reports.ShouldHaveSingleItem();
					report.Failures.Single().RegistrationId.ShouldBe(failingFirst ? 2 : 3);
					AssertSafeFailure(failure, report, error.ToString(), 1, 0);
				}
				finally { Utilities.Logger.DispatchFailed -= observer; Console.SetError(previousError); }
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldBoundRenderingFailureReportsWithoutLimitingIndependentAttempts(bool typed)
		{
			var failures = Enumerable.Range(0, 10).Select(index => new Observation { Failure = "formatter" }).ToArray();
			var good = new Observation();
			var reports = new List<LogFailureReport>();
			Action<LogFailureReport> observer = reports.Add;
			var previous = Console.Error;
			using (var error = new StringWriter())
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(null, new[] { Pair("key-private", "value-private") }))
			{
				Console.SetError(error); Utilities.Logger.DispatchFailed += observer;
				try
				{
					foreach (var data in failures.Concat(new[] { good }))
						if (typed) routes.AddTyped(new TypedText<object>(data)); else routes.Add(new TextProbe(data));
					var failure = Should.Throw<LogDispatchException>(() =>
					{
						if (typed) Utilities.Logger.Info<object>("message-private", "metadata-private"); else Utilities.Logger.Info("message-private");
					});
					foreach (var data in failures) { data.Values.Count.ShouldBe(1); data.PrintAttempts.ShouldBe(0); }
					good.Printed.Count.ShouldBe(1);
					var report = reports.ShouldHaveSingleItem();
					report.Failures.Select(item => item.RegistrationId).ShouldBe(Enumerable.Range(2, 8));
					AssertSafeFailure(failure, report, error.ToString(), 8, 2);
				}
				finally { Utilities.Logger.DispatchFailed -= observer; Console.SetError(previous); }
			}
		}

		[Fact]
		public void ShouldContainRecursiveReporterFailureAndRestoreDirectNotifications()
		{
			var destination = new TextProbe(new Observation { Failure = "formatter" });
			var reports = new List<LogFailureReport>();
			Exception nested = null;
			Action<LogFailureReport> observer = report =>
			{
				reports.Add(report);
				nested = Record.Exception(() => destination.Log(LogLevels.InformationOnly, "nested-private"));
				throw new Exception("reporter-private");
			};
			var previous = Console.Error;
			using (var error = new StringWriter())
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(null, new[] { Pair("key-private", "value-private") }))
			{
				Console.SetError(error); Utilities.Logger.DispatchFailed += observer;
				try
				{
					var first = Should.Throw<LogDispatchException>(() => destination.Log(LogLevels.InformationOnly, "message-private"));
					nested.ShouldBeOfType<LogDispatchException>();
					reports.Count.ShouldBe(1);
					reports[0].Failures.Single().RegistrationId.ShouldBe(1);
					AssertSafeFailure(first, reports[0], error.ToString(), 1, 0);
					error.GetStringBuilder().Clear();
					var second = Should.Throw<LogDispatchException>(() => destination.Log(LogLevels.InformationOnly, "message-private"));
					reports.Count.ShouldBe(2);
					second.Report.CorrelationId.ShouldNotBe(first.Report.CorrelationId);
					destination.Data.Values.Count.ShouldBe(4);
					destination.Data.PrintAttempts.ShouldBe(0);
					AssertSafeFailure(second, reports[1], error.ToString(), 1, 0);
				}
				finally { Utilities.Logger.DispatchFailed -= observer; Console.SetError(previous); }
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldRejectInvalidOrNoncurrentDirectInputBeforePayloadWork(bool typed)
		{
			var data = new Observation();
			using (var routes = new Routes<object>())
			{
				var ordinary = typed ? null : new TextProbe(data, (LogLevels)0);
				var generic = typed ? new TypedText<object>(data, (LogLevels)0) : null;
				var capture = new RawProbe();
				LogContext retained;
				Action<LogContext, LogLevels> direct = (context, level) =>
				{
					if (typed) generic.LogWithContext(context, level, new InspectionCanary(), "invalid");
					else ordinary.LogWithContext(context, level, "invalid");
				};
				using (Utilities.Logger.BeginScope(null))
				{
					capture.Log(LogLevels.InformationOnly, "capture"); retained = capture.Contexts.Single();
					Should.Throw<ArgumentNullException>(() => direct(null, LogLevels.InformationOnly)).ParamName.ShouldBe("context");
					foreach (var mask in new[] { 0, -1, 64, 65, int.MinValue })
						Should.Throw<ArgumentOutOfRangeException>(() => direct(retained, (LogLevels)mask)).ParamName.ShouldBe("level");
					using (Utilities.Logger.BeginScope(null))
						Should.Throw<ArgumentException>(() => direct(retained, LogLevels.InformationOnly)).ParamName.ShouldBe("context");
				}
				Should.Throw<ArgumentException>(() => direct(retained, LogLevels.InformationOnly)).ParamName.ShouldBe("context");
				using (Utilities.Logger.BeginScope(null))
					Should.Throw<ArgumentException>(() => direct(retained, LogLevels.InformationOnly)).ParamName.ShouldBe("context");
				data.MassageCalls.ShouldBe(0); data.Values.ShouldBeEmpty(); data.PrintAttempts.ShouldBe(0); data.Contexts.ShouldBeEmpty();
			}
		}

		[Fact]
		public void ShouldPreserveTypedTextConstructorGrammar()
		{
			for (var mask = 0; mask <= 63; mask++)
			{
				var destinations = new[] { new TypedText<object>(new Observation(), (LogLevels)mask), new TypedText<object>(mask), new TypedText<object>(mask.ToString(CultureInfo.InvariantCulture)) };
				foreach (var destination in destinations)
				{
					for (var level = 1; level <= 63; level++) destination.ValidateMessageLevel((LogLevels)level).ShouldBe((mask & level) == level);
					destination.Data.PrintAttempts.ShouldBe(0); destination.Data.Values.ShouldBeEmpty(); destination.Data.MassageCalls.ShouldBe(0);
				}
			}
			new TypedText<object>(" Critical , InformationOnly ").ValidateMessageLevel((LogLevels)9).ShouldBeTrue();
			foreach (var mask in new[] { -1, 64, 65, int.MinValue, int.MaxValue })
			{
				Should.Throw<ArgumentOutOfRangeException>(() => new TypedText<object>(mask)).ParamName.ShouldBe("intReportingLevel");
				Should.Throw<ArgumentOutOfRangeException>(() => new TypedText<object>(new Observation(), (LogLevels)mask)).ParamName.ShouldBe("reportingLevel");
			}
			Should.Throw<ArgumentNullException>(() => new TypedText<object>((string)null)).ParamName.ShouldBe("strReportingLevel");
			foreach (var text in new[] { "", " ", "critical", "64", "-1", "Critical, 8", "Critical,", "Unknown" })
				Should.Throw<ArgumentException>(() => new TypedText<object>(text)).ParamName.ShouldBe("strReportingLevel");
		}

		[Fact]
		public void ShouldExposeOnlyTheReviewedRenderingSurface()
		{
			var timestamp = typeof(LogContext).GetProperty("EventTimestampUtc", BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			timestamp.ShouldNotBeNull(); timestamp.PropertyType.ShouldBe(typeof(DateTimeOffset)); timestamp.GetSetMethod(true).ShouldBeNull();
			timestamp.GetGetMethod().IsVirtual.ShouldBeFalse(); typeof(LogContext).GetConstructors().ShouldBeEmpty();
			var format = typeof(LoggingDestinationCore).GetMethod("FormatValue", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
			format.IsFamily.ShouldBeTrue(); format.IsVirtual.ShouldBeTrue(); format.IsAbstract.ShouldBeFalse(); format.IsFinal.ShouldBeFalse();
			format.ReturnType.ShouldBe(typeof(string)); format.GetParameters().Single().Name.ShouldBe("value");
			format.GetParameters().Single().ParameterType.ShouldBe(typeof(object)); format.GetParameters().Single().IsOptional.ShouldBeFalse();
			var generic = typeof(GenericTextBasedDestination<>);
			generic.IsAbstract.ShouldBeTrue(); generic.IsPublic.ShouldBeTrue();
			generic.Namespace.ShouldBe("ProphetsWay.Utilities.LoggerDestinations");
			generic.BaseType.GetGenericTypeDefinition().ShouldBe(typeof(Utilities.Generics.BaseLoggingDestination<>));
			generic.GetGenericArguments().Single().GenericParameterAttributes.ShouldBe(GenericParameterAttributes.None);
			generic.GetGenericArguments().Single().GetGenericParameterConstraints().ShouldBeEmpty();
			var constructors = generic.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			constructors.Length.ShouldBe(3); constructors.All(constructor => constructor.IsFamily).ShouldBeTrue();
			constructors.Select(constructor => constructor.GetParameters().Single().ParameterType).OrderBy(type => type.FullName)
				.ShouldBe(new[] { typeof(LogLevels), typeof(int), typeof(string) }.OrderBy(type => type.FullName));
			constructors.Select(constructor => constructor.GetParameters().Single().Name).OrderBy(name => name)
				.ShouldBe(new[] { "intReportingLevel", "reportingLevel", "strReportingLevel" }.OrderBy(name => name));
			foreach (var type in new[] { typeof(TextBasedDestination), typeof(GenericTextBasedDestination<object>) })
			{
				var core = type.GetMethod("LogCore", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				core.IsFamily.ShouldBeTrue(); core.IsVirtual.ShouldBeTrue(); core.IsFinal.ShouldBeFalse(); core.IsAbstract.ShouldBeFalse();
				core.ReturnType.ShouldBe(typeof(void));
				core.GetParameters().Select(parameter => parameter.ParameterType).ShouldBe(type.IsGenericType
					? new[] { typeof(LogContext), typeof(LogLevels), typeof(object), typeof(string), typeof(Exception) }
					: new[] { typeof(LogContext), typeof(LogLevels), typeof(string), typeof(Exception) });
				var print = type.GetMethod("PrintLogEntry", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				print.IsFamily.ShouldBeTrue(); print.IsAbstract.ShouldBeTrue(); print.ReturnType.ShouldBe(typeof(void));
				print.GetParameters().Single().ParameterType.ShouldBe(typeof(string)); print.GetParameters().Single().Name.ShouldBe("message");
			}
			generic.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).ShouldBeEmpty();
		}

		[Fact]
		public void ShouldPassOneUnterminatedRecordToConsoleWrapper()
		{
			var records = new List<string>();
			var wrapper = new Mock<ConsoleDestination.ConsoleWrapper>(MockBehavior.Strict);
			wrapper.Setup(instance => instance.WriteLine(It.IsAny<string>())).Callback<string>(records.Add);
			using (var routes = new Routes<object>())
			using (Utilities.Logger.BeginScope(null, new[] { Pair("key", "line\nvalue") }))
			{
				var destination = new ConsoleDestination(LogLevels.Trace, wrapper.Object);
				var before = DateTimeOffset.UtcNow;
				destination.Log(LogLevels.InformationOnly, "line\r\n\\n");
				var after = DateTimeOffset.UtcNow;
				var record = records.ShouldHaveSingleItem();
				var timestamp = ReadTime(record);
				timestamp.Offset.ShouldBe(TimeSpan.Zero); timestamp.ShouldBeInRange(before, after);
				record.ShouldBe(timestamp.ToString("O", CultureInfo.InvariantCulture) + " :: " + LogLevels.InformationOnly.ToString("G").PadLeft(12) +
					":  \"line\\r\\n\\\\n\" | entryLabels=null | scopes=[{labels=null,properties=[(\"key\",\"line\\nvalue\")]}]");
				AssertSingleLine(record);
				wrapper.Verify(instance => instance.WriteLine(It.IsAny<string>()), Times.Once);
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldKeepCompletedRecordsIsolatedDuringFormatterReentry(bool typed)
		{
			var outer = new OverlapCall("outer");
			var nested = new OverlapCall("nested");
			var formatterEntries = 0;
			var nestedPrintedInsideFormatter = false;
			var callbackFailures = new ConcurrentQueue<Exception>();
			using (var selected = new ThreadLocal<Observation>())
			using (var routes = new Routes<object>())
			{
				AddOverlapProbe(routes, typed, selected);
				outer.Data.Formatter = value =>
				{
					if ((string)value == "outer-value" && Interlocked.Increment(ref formatterEntries) == 1)
					{
						try
						{
							RunOverlapCall(typed, nested, selected);
							nestedPrintedInsideFormatter = nested.Data.Printed.Count == 1;
						}
						catch (Exception failure) { callbackFailures.Enqueue(failure); }
					}
					return (string)value;
				};
				RunOverlapCall(typed, outer, selected);
				callbackFailures.ShouldBeEmpty();
				outer.Failure.ShouldBeNull(); nested.Failure.ShouldBeNull();
				formatterEntries.ShouldBe(1);
				nestedPrintedInsideFormatter.ShouldBeTrue();
				AssertOverlapCall(outer, typed, "outer");
				AssertOverlapCall(nested, typed, "outer", "nested");
			}
		}

		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldKeepCompletedRecordsIsolatedDuringConcurrentNativeRendering(bool typed)
		{
			var firstCall = new OverlapCall("first");
			var secondCall = new OverlapCall("second");
			var failures = new ConcurrentQueue<Exception>();
			var formatterEntries = 0;
			var secondPrintedInsideFormatter = false;
			using (var selected = new ThreadLocal<Observation>())
			using (var firstReached = new ManualResetEventSlim())
			using (var secondFinished = new ManualResetEventSlim())
			using (var routes = new Routes<object>())
			{
				AddOverlapProbe(routes, typed, selected);
				firstCall.Data.Formatter = value =>
				{
					if ((string)value == "first-value")
					{
						Interlocked.Increment(ref formatterEntries);
						try
						{
							firstReached.Set();
							if (!secondFinished.Wait(TimeSpan.FromSeconds(3)))
								failures.Enqueue(new TimeoutException("The second caller did not complete while the first formatter was active."));
							else secondPrintedInsideFormatter = secondCall.Data.Printed.Count == 1;
						}
						catch (Exception failure) { failures.Enqueue(failure); }
					}
					return (string)value;
				};
				var first = new Thread(() =>
				{
					try { RunOverlapCall(typed, firstCall, selected); }
					catch (Exception failure) { failures.Enqueue(failure); }
					finally { firstReached.Set(); }
				}) { IsBackground = true };
				var second = new Thread(() =>
				{
					try
					{
						if (!firstReached.Wait(TimeSpan.FromSeconds(3)))
							failures.Enqueue(new TimeoutException("The first caller neither reached its formatter nor completed."));
						else RunOverlapCall(typed, secondCall, selected);
					}
					catch (Exception failure) { failures.Enqueue(failure); }
					finally { secondFinished.Set(); }
				}) { IsBackground = true };
				var firstStarted = false;
				var secondStarted = false;
				var firstStopped = false;
				var secondStopped = false;
				try
				{
					first.Start(); firstStarted = true;
					second.Start(); secondStarted = true;
					firstStopped = first.Join(TimeSpan.FromSeconds(5));
					secondStopped = second.Join(TimeSpan.FromSeconds(5));
				}
				finally
				{
					firstReached.Set(); secondFinished.Set();
					if (firstStarted && !firstStopped) firstStopped = first.Join(TimeSpan.FromSeconds(3));
					if (secondStarted && !secondStopped) secondStopped = second.Join(TimeSpan.FromSeconds(3));
				}
				firstStopped.ShouldBeTrue(); secondStopped.ShouldBeTrue();
				failures.ShouldBeEmpty();
				firstCall.Failure.ShouldBeNull(); secondCall.Failure.ShouldBeNull();
				formatterEntries.ShouldBe(1);
				secondPrintedInsideFormatter.ShouldBeTrue();
				AssertOverlapCall(firstCall, typed, "first");
				AssertOverlapCall(secondCall, typed, "second");
			}
		}

		private static void AddOverlapProbe(Routes<object> routes, bool typed, ThreadLocal<Observation> selected)
		{
			var data = new Observation { SelectObservation = () => selected.Value };
			if (typed) routes.AddTyped(new TypedText<object>(data)); else routes.Add(new TextProbe(data));
		}

		private static void RunOverlapCall(bool typed, OverlapCall call, ThreadLocal<Observation> selected)
		{
			var previous = selected.Value;
			selected.Value = call.Data;
			try
			{
				using (Utilities.Logger.BeginScope(Labels(call.Name + "-scope"), new[] { Pair(call.Name + "-key", call.Name + "-value") }))
				{
					call.Before = DateTimeOffset.UtcNow;
					try
					{
						if (typed) Utilities.Logger.LogAnnotated<object>(Labels(call.Name + "-entry"), LogLevels.InformationOnly, call.Name + "-metadata", call.Name + "-message");
						else Utilities.Logger.LogAnnotated(Labels(call.Name + "-entry"), LogLevels.InformationOnly, message: call.Name + "-message");
					}
					finally { call.After = DateTimeOffset.UtcNow; }
				}
			}
			catch (Exception failure) { call.Failure = failure; }
			finally { selected.Value = previous; }
		}

		private static void AssertOverlapCall(OverlapCall call, bool typed, params string[] scopes)
		{
			call.Failure.ShouldBeNull();
			call.Data.Messages.ShouldBe(new[] { call.Name + "-message" });
			call.Data.MassageCalls.ShouldBe(1);
			call.CapturedTime.ShouldBeInRange(call.Before, call.After);
			call.Data.Contexts.Single().EventTimestampUtc.ShouldBe(call.CapturedTime);
			var fields = (typed ? " | metadata=" + Token(call.Name + "-metadata") : "") +
				" | entryLabels=[" + Token(call.Name + "-entry") + "] | scopes=[" +
				string.Join(",", scopes.Select(name => "{labels=[" + Token(name + "-scope") + "],properties=[(" + Token(name + "-key") + "," + Token(name + "-value") + ")]}")) + "]";
			AssertRecord(call.Data, LogLevels.InformationOnly, Token(call.Name + "-message"), fields);
			ReadTime(call.Data.Printed.Single()).ShouldBe(call.CapturedTime);
			call.Data.Values.Cast<string>().OrderBy(value => value, StringComparer.Ordinal).ShouldBe(
				scopes.Select(name => name + "-value").Concat(typed ? new[] { call.Name + "-metadata" } : new string[0]).OrderBy(value => value, StringComparer.Ordinal));
		}

		private sealed class OverlapCall
		{
			public readonly string Name;
			public readonly Observation Data = new Observation { Formatter = value => (string)value };
			public DateTimeOffset Before;
			public DateTimeOffset After;
			public DateTimeOffset CapturedTime;
			public Exception Failure;
			public OverlapCall(string name)
			{
				Name = name;
				Data.BeforeRender = (context, message) => CapturedTime = context.EventTimestampUtc;
			}
		}

		private static void AssertTypedMetadata<T>(T metadata, string expected)
		{
			var data = new Observation();
			var raw = new TypedRawProbe<T>();
			var ordinary = new RawProbe();
			var unrelated = new TypedRawProbe<Guid>();
			using (var routes = new Routes<T>())
			using (Utilities.Logger.BeginScope(Labels("scope")))
			{
				routes.AddTyped(new TypedText<T>(data)); routes.AddTyped(raw); routes.Add(ordinary);
				Utilities.Logger.AddDestination<Guid>(unrelated);
				try
				{
					Utilities.Logger.LogAnnotated<T>(Labels("entry"), LogLevels.InformationOnly, metadata, null);
					AssertRecord(data, LogLevels.InformationOnly, "null", " | metadata=" + expected + " | entryLabels=[\"entry\"] | scopes=[{labels=[\"scope\"],properties=[]}]");
					raw.Metadata.Single().ShouldBe(metadata);
					if (!typeof(T).IsValueType) ReferenceEquals(raw.Metadata[0], metadata).ShouldBeTrue();
					data.Values.Count.ShouldBe(1); ordinary.Contexts.ShouldBeEmpty(); unrelated.Contexts.ShouldBeEmpty();
					data.Contexts.Single().Labels.EffectiveLabels.Select(label => label.Identifier).OrderBy(identifier => identifier).ShouldBe(new[] { "entry", "scope" });
				}
				finally { Utilities.Logger.RemoveDestination<Guid>(unrelated); }
			}
		}

		private static void AssertSafeFailure(LogDispatchException failure, LogFailureReport report, string diagnostic, int count, int overflow)
		{
			failure.Report.CorrelationId.ShouldBe(report.CorrelationId);
			report.CorrelationId.ShouldNotBe(Guid.Empty); report.CoreCaptureFailureCount.ShouldBe(0);
			report.Failures.Count.ShouldBe(count); report.OverflowCount.ShouldBe(overflow);
			report.Failures.All(item => item.Stage == LogFailureStage.Output).ShouldBeTrue();
			failure.InnerException.ShouldBeNull(); failure.StackTrace.ShouldBeNull(); failure.Data.Count.ShouldBe(0); failure.HelpLink.ShouldBeNull();
			diagnostic.Length.ShouldBeInRange(1, 512);
			var safe = diagnostic + failure.Message + failure.ToString();
			foreach (var canary in new[] { "message-private", "metadata-private", "value-private", "key-private", "label-private", "cause-private", "reporter-private", "nested-private" })
				safe.ShouldNotContain(canary, Case.Sensitive);
		}

		private static void AssertRecord(Observation data, LogLevels level, string messageToken, string fields)
		{
			data.Printed.Count.ShouldBe(1); data.PrintAttempts.ShouldBe(1); data.Contexts.Count.ShouldBe(1);
			AssertRecordAt(data, 0, level, messageToken, fields);
		}

		private static void AssertRecordAt(Observation data, int index, LogLevels level, string messageToken, string fields)
		{
			var timestamp = data.Contexts[index].EventTimestampUtc;
			timestamp.Offset.ShouldBe(TimeSpan.Zero);
			data.Printed[index].ShouldBe(timestamp.ToString("O", CultureInfo.InvariantCulture) + " :: " + level.ToString("G").PadLeft(12) + ":  " + messageToken + fields);
			AssertSingleLine(data.Printed[index]);
		}

		private static void AssertSingleLine(string record)
		{
			record.ShouldNotBeNull();
			record.Any(unit => char.IsControl(unit) || unit == '\u2028' || unit == '\u2029').ShouldBeFalse();
		}

		private static DateTimeOffset ReadTime(string record)
		{
			var separator = record.IndexOf(" :: ", StringComparison.Ordinal);
			separator.ShouldBe(33);
			return DateTimeOffset.ParseExact(record.Substring(0, separator), "O", CultureInfo.InvariantCulture, DateTimeStyles.None);
		}

		private static DateTimeOffset AdvanceClock(DateTimeOffset previous)
		{
			var elapsed = Stopwatch.StartNew();
			do
			{
				var current = DateTimeOffset.UtcNow;
				if (current > previous.AddMilliseconds(20)) return current;
				Thread.Yield();
			} while (elapsed.Elapsed < TimeSpan.FromSeconds(2));
			throw new TimeoutException("The real clock did not advance within the bounded test setup interval.");
		}

		private static string Token(string text) { return text == null ? "null" : "\"" + Escape(text) + "\""; }

		private static string Escape(string text)
		{
			var result = new StringBuilder();
			foreach (var unit in text)
			{
				if (unit == '\\') result.Append("\\\\");
				else if (unit == '"') result.Append("\\\"");
				else if (unit == '\r') result.Append("\\r");
				else if (unit == '\n') result.Append("\\n");
				else if (unit == '\t') result.Append("\\t");
				else if (char.IsControl(unit) || unit == '\u2028' || unit == '\u2029') result.Append("\\u" + ((int)unit).ToString("X4", CultureInfo.InvariantCulture));
				else result.Append(unit);
			}
			return result.ToString();
		}

		private static string DefaultValue(object value) { return new ScalarProbe().Format(value); }
		private static LogAnnotations Labels(params string[] identifiers) { return new LogAnnotations(identifiers.Select(identifier => new SensitivityLabel(identifier))); }
		private static KeyValuePair<string, object> Pair(string key, object value) { return new KeyValuePair<string, object>(key, value); }

		private static Exception NestedException()
		{
			try
			{
				try { throw new InvalidOperationException("inner\"\n\\n\u0085\u2028"); }
				catch (Exception inner) { throw new Exception("outer\r\t\u2029", inner); }
			}
			catch (Exception exception) { return exception; }
		}

		private static KeyValuePair<object, string> Scalar(object value, string expected)
		{
			return new KeyValuePair<object, string>(value, expected);
		}

		[Flags]
		private enum SampleFlags { First = 1, Second = 2 }
		private enum UnsignedSample : ulong { Zero = 0 }

		private sealed class ScalarProbe : LoggingDestinationCore
		{
			public ScalarProbe() : base(LogLevels.Trace) { }
			public string Format(object value) { return FormatValue(value); }
		}

		private sealed class Observation
		{
			public readonly List<LogContext> Contexts = new List<LogContext>();
			public readonly List<string> Messages = new List<string>();
			public readonly List<Exception> Exceptions = new List<Exception>();
			public readonly List<object> Values = new List<object>();
			public readonly List<string> Printed = new List<string>();
			public int MassageCalls;
			public int PrintAttempts;
			public string Failure;
			public Func<object, string> Formatter;
			public Action<LogContext, string> BeforeRender;
			public Func<Observation> SelectObservation;
			public void Enter(LogContext context, string message, Exception exception)
			{
				if (SelectObservation != null) { SelectObservation().Enter(context, message, exception); return; }
				Contexts.Add(context); Messages.Add(message); Exceptions.Add(exception);
				if (BeforeRender != null) BeforeRender(context, message);
			}
			public string Massage(Func<string> original)
			{
				if (SelectObservation != null) return SelectObservation().Massage(original);
				MassageCalls++;
				if (Failure == "massage") throw new Exception("cause-private");
				return original();
			}
			public string Format(object value, Func<object, string> original)
			{
				if (SelectObservation != null) return SelectObservation().Format(value, original);
				Values.Add(value);
				if (Failure == "formatter") throw new Exception("cause-private", new Exception("value-private"));
				return Formatter == null ? original(value) : Formatter(value);
			}
			public void Print(string message)
			{
				if (SelectObservation != null) { SelectObservation().Print(message); return; }
				PrintAttempts++; Printed.Add(message);
				if (Failure == "print") throw new Exception("cause-private");
			}
		}

		private sealed class TextProbe : TextBasedDestination
		{
			public readonly Observation Data;
			public TextProbe(Observation data = null, LogLevels mask = LogLevels.Trace) : base(mask) { Data = data ?? new Observation(); }
			protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex)
			{
				Data.Enter(context, message, ex); base.LogCore(context, level, message, ex);
			}
			protected override string MassageLogStatement(LogLevels level, string message = null, Exception ex = null) { return Data.Massage(() => base.MassageLogStatement(level, message, ex)); }
			protected override string FormatValue(object value) { return Data.Format(value, base.FormatValue); }
			protected override void PrintLogEntry(string message) { Data.Print(message); }
		}

		private sealed class TypedText<T> : GenericTextBasedDestination<T>
		{
			public readonly Observation Data;
			public TypedText(Observation data, LogLevels mask = LogLevels.Trace) : base(mask) { Data = data; }
			public TypedText(int mask) : base(mask) { Data = new Observation(); }
			public TypedText(string mask) : base(mask) { Data = new Observation(); }
			protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception ex)
			{
				Data.Enter(context, message, ex); base.LogCore(context, level, metadata, message, ex);
			}
			protected override string MassageLogStatement(LogLevels level, string message = null, Exception ex = null) { return Data.Massage(() => base.MassageLogStatement(level, message, ex)); }
			protected override string FormatValue(object value) { return Data.Format(value, base.FormatValue); }
			protected override void PrintLogEntry(string message) { Data.Print(message); }
		}

		private sealed class EligibilityProbe : ILoggingDestination, IContextLoggingDestination,
			Utilities.Generics.ILoggingDestination<object>, Utilities.Generics.IContextLoggingDestination<object>
		{
			public readonly List<LogContext> Contexts = new List<LogContext>();
			public DateTimeOffset EligibilityFinished;
			public int LegacyCalls;
			public bool ValidateMessageLevel(LogLevels messageLevel)
			{
				EligibilityFinished = AdvanceClock(DateTimeOffset.UtcNow);
				return true;
			}
			public void Log(LogLevels level, string message = null, Exception ex = null) { LegacyCalls++; }
			public void Log(LogLevels level, object metadata, string message = null, Exception ex = null) { LegacyCalls++; }
			public void LogWithContext(LogContext context, LogLevels level, string message = null, Exception ex = null) { Contexts.Add(context); }
			public void LogWithContext(LogContext context, LogLevels level, object metadata, string message = null, Exception ex = null) { Contexts.Add(context); }
		}

		private sealed class RawProbe : BaseLoggingDestination
		{
			public readonly List<LogContext> Contexts = new List<LogContext>();
			public readonly List<string> Messages = new List<string>();
			public readonly List<Exception> Exceptions = new List<Exception>();
			public Action<LogContext, string> OnEntry;
			public RawProbe() : base(LogLevels.Trace) { }
			protected override void LogCore(LogContext context, LogLevels level, string message, Exception ex)
			{
				Contexts.Add(context); Messages.Add(message); Exceptions.Add(ex);
				if (OnEntry != null) OnEntry(context, message);
			}
		}

		private sealed class TypedRawProbe<T> : Utilities.Generics.BaseLoggingDestination<T>
		{
			public readonly List<LogContext> Contexts = new List<LogContext>();
			public readonly List<T> Metadata = new List<T>();
			public readonly List<Exception> Exceptions = new List<Exception>();
			public TypedRawProbe() : base(LogLevels.Trace) { }
			protected override void LogCore(LogContext context, LogLevels level, T metadata, string message, Exception ex)
			{
				Contexts.Add(context); Metadata.Add(metadata); Exceptions.Add(ex);
			}
		}

		private sealed class Routes<T> : IDisposable
		{
			private readonly List<ILoggingDestination> _ordinary = new List<ILoggingDestination>();
			private readonly List<Utilities.Generics.ILoggingDestination<T>> _typed = new List<Utilities.Generics.ILoggingDestination<T>>();
			public Routes() { Add(new EventDestination((LogLevels)0)); AddTyped(new GenericEventDestination<T>((LogLevels)0)); }
			public void Add(ILoggingDestination destination, DestinationRegistrationSettings settings = null)
			{
				if (settings == null) Utilities.Logger.AddDestination(destination); else Utilities.Logger.AddDestination(destination, settings);
				_ordinary.Add(destination);
			}
			public void AddTyped(Utilities.Generics.ILoggingDestination<T> destination, DestinationRegistrationSettings settings = null)
			{
				if (settings == null) Utilities.Logger.AddDestination<T>(destination); else Utilities.Logger.AddDestination<T>(destination, settings);
				_typed.Add(destination);
			}
			public void Dispose()
			{
				foreach (var destination in _ordinary) Utilities.Logger.RemoveDestination(destination);
				foreach (var destination in _typed) Utilities.Logger.RemoveDestination<T>(destination);
			}
		}

		private sealed class MutableValue { public string Text; }

		private sealed class InspectionCanary : IEnumerable, IFormattable, IDisposable
		{
			public int Inspections;
			public int Disposals;
			public string Payload { get { Inspections++; throw new InvalidOperationException("Unexpected getter inspection."); } }
			public override string ToString() { Inspections++; throw new InvalidOperationException("Unexpected ToString."); }
			public string ToString(string format, IFormatProvider provider) { Inspections++; throw new InvalidOperationException("Unexpected IFormattable."); }
			public IEnumerator GetEnumerator() { Inspections++; throw new InvalidOperationException("Unexpected enumeration."); }
			public override bool Equals(object other) { Inspections++; throw new InvalidOperationException("Unexpected equality."); }
			public override int GetHashCode() { Inspections++; throw new InvalidOperationException("Unexpected hashing."); }
			public void Dispose() { Disposals++; }
		}

		private sealed class SingleReadPairs : IEnumerable<KeyValuePair<string, object>>
		{
			private readonly List<KeyValuePair<string, object>> _source;
			public int Reads;
			public SingleReadPairs(List<KeyValuePair<string, object>> source) { _source = source; }
			public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
			{
				Reads++;
				if (Reads != 1) throw new InvalidOperationException("Producer membership was enumerated again.");
				return _source.GetEnumerator();
			}
			IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
		}
	}
}