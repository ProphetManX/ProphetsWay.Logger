using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Shouldly;
using Xunit;
using NativeLogger = ProphetsWay.Utilities.Logger;

namespace ProphetsWay.Logger.Tests
{
	[CollectionDefinition(Name, DisableParallelization = true)]
	public sealed class ControlledReturnTestCollection
	{
		public const string Name = "Controlled return in-memory regressions";
	}

	[Collection(ControlledReturnTestCollection.Name)]
	public sealed class MicrosoftBridgeControlledReturnTests
	{
		// R-06/AC-06.1, R-18, I27 and O15-O20: entry-only labels must survive
		// every unchanged receipt of one export, not just its first provider receipt.
		[Fact]
		public void TwoProviderFanoutWithholdsEntryLabeledPayloadFromDeniedRawRecipient()
		{
			using (var factory = new FanoutFactory())
			{
				var label = new SensitivityLabel("controlled-return-private");
				var denied = new RawRecipient();
				var allowed = new CaptureRecipient();
				var exception = new InvalidOperationException("synthetic raw exception canary");
				using (Ordinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory)))
				using (Exact(denied, Settings(LogLevels.Trace,
					new DestinationLabelPolicy(LabelFilterMode.Exclude, new[] { label }))))
				using (Exact(allowed))
					NativeLogger.LogAnnotated(new LogAnnotations(new[] { label }),
						LogLevels.InformationOnly, "synthetic raw payload canary", exception);

				denied.Records.ShouldBeEmpty("a denied raw callback must not receive even the transport containing the labeled payload");
				allowed.Records.Count.ShouldBe(1);
				allowed.Records[0].Entry.ShouldBeSameAs(factory.Entries.Single());
				allowed.Records[0].Exception.ShouldBeSameAs(exception);
				allowed.Records[0].Entry.NativeContext.Labels.EntryAnnotations.LabelOccurrences.ShouldContain(label);
				factory.ReceiptAttempts.ShouldBe(2);
			}
		}

		// I27/I28, O28/O29 and AC-19.1: a route is visited once per controlled
		// entry, while other original recipients still see the identical carrier.
		[Fact]
		public void TwoProviderFanoutDeliversOriginalCarrierOnceWithoutAbandoningOtherRecipients()
		{
			using (var factory = new FanoutFactory())
			{
				var first = new CaptureRecipient();
				var second = new CaptureRecipient();
				using (Ordinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory)))
				using (Exact(first))
				using (Exact(second))
					NativeLogger.LogAnnotated(null, LogLevels.WarningOnly, message: "same captured text");

				first.Records.Count.ShouldBe(1);
				second.Records.Count.ShouldBe(1);
				first.Records[0].Entry.ShouldBeSameAs(factory.Entries.Single());
				second.Records[0].Entry.ShouldBeSameAs(first.Records[0].Entry);
				first.Records[0].Entry.StateType.ShouldBe(typeof(string));
				first.Records[0].Entry.OriginalState.ShouldBe("same captured text");
				factory.ReceiptAttempts.ShouldBe(2);
			}
		}

		[Fact]
		public void TwoProviderFanoutSuppressesVisitedRouteCycleBeforeAnotherExport()
		{
			using (var factory = new FanoutFactory())
			{
				var before = new CaptureRecipient();
				var after = new CaptureRecipient();
				Exception failure = null;
				using (Ordinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory)))
				using (Exact(before))
				using (Exact(new GenericMicrosoftLoggerDestination<LogBridgeEntry>(LogLevels.Trace, factory)))
				using (Exact(after))
				{
					try { NativeLogger.LogAnnotated(null, LogLevels.WarningOnly, message: "bounded synthetic cycle"); }
					catch (Exception ex) { failure = ex; }
				}
				factory.GuardTripped.ShouldBeFalse("synthetic callback exceeded eight exports: controlled cycle suppression failed; recursion was stopped by the fixture");
				failure.ShouldBeNull("a correctly suppressed controlled return is not an output failure");
				factory.Entries.Count.ShouldBe(2, "one ordinary export and one newly entered exact-route export");
				factory.ReceiptAttempts.ShouldBe(4);
				before.Records.Count.ShouldBe(1);
				after.Records.Count.ShouldBe(1);
				factory.Entries.All(entry => ReferenceEquals(entry, before.Records[0].Entry)).ShouldBeTrue();
				after.Records[0].Entry.ShouldBeSameAs(before.Records[0].Entry);
			}
		}

		// R-07/AC-07.2 and I27: checking OriginalLevel alone is insufficient.
		// Check registration, supplied-base intrinsic and custom severity boundaries,
		// plus the actual native level delivered to an allowed recipient.
		[Fact]
		public void CompositeControlledReturnUsesFullMaskForRegistrationSelection()
		{
			using (var factory = new FanoutFactory())
			{
				var denied = new RawRecipient();
				var allowed = new CaptureRecipient();
				using (Ordinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory)))
				using (Exact(denied, Settings(LogLevels.Critical)))
				using (Exact(allowed, Settings(LogLevels.Information)))
					NativeLogger.LogAnnotated(null, (LogLevels)9, message: "composite registration canary");
				denied.Records.ShouldBeEmpty("mask 1 cannot include every requested bit of mask 9");
				AssertCompositeDelivery(factory, allowed);
			}
		}

		[Fact]
		public void CompositeControlledReturnUsesFullMaskForIntrinsicSelection()
		{
			using (var factory = new FanoutFactory())
			{
				var denied = new CaptureRecipient(LogLevels.Critical);
				var allowed = new CaptureRecipient(LogLevels.Information);
				using (Ordinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory)))
				using (Exact(denied))
				using (Exact(allowed))
					NativeLogger.LogAnnotated(null, (LogLevels)9, message: "composite intrinsic canary");
				denied.Records.ShouldBeEmpty("the intrinsic mask must also include all original bits");
				AssertCompositeDelivery(factory, allowed);
			}
		}

		[Fact]
		public void CompositeControlledReturnPassesFullMaskToCustomCheckAndNativeHandoff()
		{
			using (var factory = new FanoutFactory())
			{
				var custom = new RawRecipient { Permission = level => level == (LogLevels)9 };
				var allowed = new CaptureRecipient();
				using (Ordinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory)))
				using (Exact(custom))
				using (Exact(allowed))
					NativeLogger.LogAnnotated(null, (LogLevels)9, message: "composite custom canary");
				custom.CheckedLevels.ShouldBe(new[] { (LogLevels)9 });
				custom.Records.Count.ShouldBe(1);
				custom.Records[0].Level.ShouldBe((LogLevels)9);
				custom.Records[0].Entry.ShouldBeSameAs(factory.Entries.Single());
				AssertCompositeDelivery(factory, allowed);
			}
		}

		private static void AssertCompositeDelivery(FanoutFactory factory, CaptureRecipient recipient)
		{
			factory.Levels.ShouldBe(new[] { LogLevel.Critical });
			recipient.Records.Count.ShouldBe(1);
			recipient.Records[0].Level.ShouldBe((LogLevels)9, "native delivery retains the full mask, not the outward Microsoft projection");
			recipient.Records[0].Entry.ShouldBeSameAs(factory.Entries.Single());
			recipient.Records[0].Entry.OriginalLevel.ShouldBe((LogLevels)9);
		}

		// O28/O29: a broad ambient exporting flag, or state reference alone, must
		// not claim unrelated calls. Each altered argument is tested before the real
		// receipts, so it cannot consume the original transfer's private binding.
		[Theory]
		[InlineData("unrelated")]
		[InlineData("state")]
		[InlineData("formatter")]
		[InlineData("exception")]
		[InlineData("event")]
		[InlineData("level")]
		public void IndependentCallsDuringExportDoNotConsumeOrInheritControlledTransfer(string change)
		{
			using (var factory = new FanoutFactory { IndependentCall = change })
			{
				var observer = new CaptureRecipient();
				using (Ordinary(new MicrosoftLoggerDestination(LogLevels.Trace, factory)))
				using (Exact(observer))
				{
					NativeLogger.LogAnnotated(null, LogLevels.WarningOnly, message: "identical text");
					observer.Records.Count.ShouldBe(2, "one independent ingress plus one controlled route visit");
					var fresh = observer.Records[0].Entry;
					fresh.ShouldNotBeSameAs(factory.Entries.Single());
					fresh.OriginalLevel.ShouldBe(change == "level" ? LogLevels.DebugOnly : LogLevels.WarningOnly);
					observer.Records[1].Entry.ShouldBeSameAs(factory.Entries.Single());
					// Same carrier outside the bound invocation is ordinary external state.
					var producerCalls = 0;
					factory.First.CreateLogger("fresh after export").Log(LogLevel.Warning, default(EventId),
						factory.Entries.Single(), null, (state, ex) => { producerCalls++; return "identical text"; });
					producerCalls.ShouldBe(1);
					observer.Records.Count.ShouldBe(3);
					observer.Records[2].Entry.ShouldNotBeSameAs(factory.Entries.Single());
					observer.Records[2].Entry.OriginalState.ShouldBeSameAs(factory.Entries.Single());
				}
			}
		}

		private static DestinationRegistrationSettings Settings(LogLevels level = LogLevels.Trace, DestinationLabelPolicy policy = null)
		{
			return new DestinationRegistrationSettings(true, level,
				policy ?? new DestinationLabelPolicy(LabelFilterMode.NoFilter, new SensitivityLabel[0]));
		}

		// Own only these registrations; never clear or replace another test's globals.
		private static IDisposable Ordinary(ILoggingDestination destination)
		{
			NativeLogger.AddDestination(destination, Settings());
			return new Cleanup(() => NativeLogger.RemoveDestination(destination));
		}

		private static IDisposable Exact(ProphetsWay.Utilities.Generics.ILoggingDestination<LogBridgeEntry> destination,
			DestinationRegistrationSettings settings = null)
		{
			NativeLogger.AddDestination<LogBridgeEntry>(destination, settings ?? Settings());
			return new Cleanup(() => NativeLogger.RemoveDestination<LogBridgeEntry>(destination));
		}

		private sealed class Delivery
		{
			internal LogLevels Level;
			internal LogBridgeEntry Entry;
			internal Exception Exception;
		}

		private sealed class CaptureRecipient : ProphetsWay.Utilities.Generics.BaseLoggingDestination<LogBridgeEntry>
		{
			internal readonly List<Delivery> Records = new List<Delivery>();
			internal CaptureRecipient(LogLevels level = LogLevels.Trace) : base(level) { }
			protected override void LogCore(LogContext context, LogLevels level, LogBridgeEntry metadata, string message, Exception ex)
			{
				Records.Add(new Delivery { Level = level, Entry = metadata, Exception = ex });
			}
		}

		private sealed class RawRecipient : ProphetsWay.Utilities.Generics.ILoggingDestination<LogBridgeEntry>
		{
			internal readonly List<Delivery> Records = new List<Delivery>();
			internal readonly List<LogLevels> CheckedLevels = new List<LogLevels>();
			internal Func<LogLevels, bool> Permission = level => true;
			public bool ValidateMessageLevel(LogLevels level) { CheckedLevels.Add(level); return Permission(level); }
			public void Log(LogLevels level, LogBridgeEntry metadata, string message = null, Exception ex = null)
			{
				Records.Add(new Delivery { Level = level, Entry = metadata, Exception = ex });
			}
		}

		private sealed class Cleanup : IDisposable
		{
			private Action _action;
			internal Cleanup(Action action) { _action = action; }
			public void Dispose() { var action = _action; _action = null; if (action != null) action(); }
		}

		// In-memory host fanout: forwards the original generic arguments unchanged to
		// two separately registered production providers. No envelope reconstruction,
		// private bridge hooks, default-output configuration or filesystem fixture.
		private sealed class FanoutFactory : ILoggerFactory
		{
			private readonly List<ILoggerProvider> _providers = new List<ILoggerProvider>();
			internal readonly List<LogBridgeEntry> Entries = new List<LogBridgeEntry>();
			internal readonly List<LogLevel> Levels = new List<LogLevel>();
			internal readonly MicrosoftLoggerProvider First;
			internal string IndependentCall;
			internal int ReceiptAttempts;
			internal bool GuardTripped;
			internal FanoutFactory()
			{
				First = this.AddProphetsWay();
				this.AddProphetsWay();
			}
			public void AddProvider(ILoggerProvider provider) { _providers.Add(provider); }
			public ILogger CreateLogger(string categoryName)
			{
				return new FanoutLogger(this, _providers.Select(provider => provider.CreateLogger(categoryName)).ToArray());
			}
			public void Dispose() { foreach (var provider in _providers) provider.Dispose(); _providers.Clear(); }

			private sealed class FanoutLogger : ILogger
			{
				private readonly FanoutFactory _owner;
				private readonly ILogger[] _loggers;
				internal FanoutLogger(FanoutFactory owner, ILogger[] loggers) { _owner = owner; _loggers = loggers; }
				public bool IsEnabled(LogLevel level) { return _loggers.Any(logger => logger.IsEnabled(level)); }
				public IDisposable BeginScope<TState>(TState state)
				{
					var handles = new List<IDisposable>();
					try { foreach (var logger in _loggers) handles.Add(logger.BeginScope(state)); }
					catch { for (var i = handles.Count - 1; i >= 0; i--) handles[i]?.Dispose(); throw; }
					return new Cleanup(() => { for (var i = handles.Count - 1; i >= 0; i--) handles[i]?.Dispose(); });
				}
				public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception exception,
					Func<TState, Exception, string> formatter)
				{
					// Test-only failure bound, not a product counter policy. It throws
					// before another callback can recurse and is asserted independently.
					if (_owner.Entries.Count >= 8)
					{
						_owner.GuardTripped = true;
						throw new InvalidOperationException("Synthetic fanout recursion guard: more than eight exports.");
					}
					var envelope = (IEnumerable<KeyValuePair<string, object>>)(object)state;
					_owner.Entries.Add((LogBridgeEntry)envelope.Single(pair => pair.Key == "ProphetsWay.Entry").Value);
					_owner.Levels.Add(level);
					var change = _owner.IndependentCall;
					_owner.IndependentCall = null;
					if (change == "unrelated")
						_loggers[0].Log(level, eventId, "identical text", exception, (text, ex) => text);
					else if (change != null)
					{
						Func<TState, Exception, string> otherFormatter = (value, ex) => formatter(value, ex);
						_loggers[0].Log(change == "level" ? LogLevel.Debug : level,
							change == "event" ? new EventId(71, "independent event") : eventId,
							change == "state" ? default(TState) : state,
							change == "exception" ? new Exception("independent synthetic exception") : exception,
							change == "formatter" ? otherFormatter : formatter);
					}
					foreach (var logger in _loggers)
					{
						_owner.ReceiptAttempts++;
						logger.Log(level, eventId, state, exception, formatter);
					}
				}
			}
		}
	}
}