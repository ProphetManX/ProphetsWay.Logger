using FluentAssertions;
using Xunit;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class EventDestinationTests
	{
		[Theory]
		[InlineData(false)]
		[InlineData(true)]
		public void ShouldDeliverM3EventContextWithOriginalRawValues(bool typed)
		{
			var ordinary = new EventDestination(LogLevels.Trace);
			var generic = new GenericEventDestination<object>(LogLevels.Trace);
			EventDestination.LoggerEventArgs ordinaryArgs = null;
			GenericEventDestination<object>.LoggerEventArgs typedArgs = null;
			object senderSeen = null;
			var calls = 0;
			ordinary.LoggingEvent += (sender, args) => { senderSeen = sender; ordinaryArgs = args; calls++; };
			generic.LoggingEvent += (sender, args) => { senderSeen = sender; typedArgs = args; calls++; };
			Utilities.Logger.AddDestination(ordinary);
			Utilities.Logger.AddDestination<object>(generic);
			try
			{
				var propertyValue = new M3OpaqueProperty();
				var metadata = new object();
				var exception = new Exception("event detail");
				var annotation = new LogAnnotations(new[] { new SensitivityLabel("entry") });
				using (M3EventScope(new[] { new KeyValuePair<string, object>("payload", propertyValue) }))
				{
					var parameterTypes = typed
						? new[] { typeof(LogAnnotations), typeof(LogLevels), typeof(object), typeof(string), typeof(Exception) }
						: new[] { typeof(LogAnnotations), typeof(LogLevels), typeof(string), typeof(Exception) };
					var method = typeof(Utilities.Logger).GetMethods().Where(candidate => candidate.Name == "LogAnnotated" && candidate.IsGenericMethodDefinition == typed)
						.Select(candidate => typed ? candidate.MakeGenericMethod(typeof(object)) : candidate)
						.SingleOrDefault(candidate => candidate.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(parameterTypes));
					M3EventInvoke(method, typed ? new object[] { annotation, (LogLevels)9, metadata, "raw", exception }
						: new object[] { annotation, (LogLevels)9, "raw", exception });
					calls.ShouldBe(1);
					var context = M3EventContext(typed ? (object)typedArgs : ordinaryArgs);
					context.ShouldNotBeNull();
					context.Scopes.ShouldHaveSingleItem().Properties.ShouldHaveSingleItem().Value.ShouldBeSameAs(propertyValue);
					context.Labels.Origins.Select(origin => origin.Label.Identifier).ShouldBe(new[] { "scope", "entry" });
					if (typed)
					{
						senderSeen.ShouldBeSameAs(generic);
						typedArgs.Metadata.ShouldBeSameAs(metadata);
						typedArgs.Exception.ShouldBeSameAs(exception);
						typedArgs.RawMessage.ShouldBe("raw");
						typedArgs.Message.ShouldContain("event detail");
						typedArgs.LogLevel.ShouldBe((LogLevels)9);
					}
					else
					{
						senderSeen.ShouldBeSameAs(ordinary);
						ordinaryArgs.Exception.ShouldBeSameAs(exception);
						ordinaryArgs.RawMessage.ShouldBe("raw");
						ordinaryArgs.Message.ShouldContain("event detail");
						ordinaryArgs.LogLevel.ShouldBe((LogLevels)9);
					}
					if (typed)
						((Utilities.Generics.IContextLoggingDestination<object>)generic).LogWithContext(context, (LogLevels)9, metadata, "repeat", exception);
					else
						((IContextLoggingDestination)ordinary).LogWithContext(context, (LogLevels)9, "repeat", exception);
					calls.ShouldBe(2);
					M3EventContext(typed ? (object)typedArgs : ordinaryArgs).Labels.EntryAnnotations.LabelOccurrences
						.Select(label => label.Identifier).ShouldBe(new[] { "entry" });
					propertyValue.InspectionCalls.ShouldBe(0);
				}
				M3EventContext(typed ? (object)typedArgs : ordinaryArgs).Scopes.Count.ShouldBe(1);
			}
			finally
			{
				Utilities.Logger.RemoveDestination(ordinary);
				Utilities.Logger.RemoveDestination<object>(generic);
			}
		}

		[Fact]
		public void ShouldLeaveM3ManuallyConstructedEventContextAbsentInsideAScope()
		{
			using (M3EventScope(new KeyValuePair<string, object>[0]))
			{
				var ordinary = new EventDestination.LoggerEventArgs("massaged", (LogLevels)0, null, null);
				var typed = new GenericEventDestination<object>.LoggerEventArgs(null, (LogLevels)0, null, null, "massaged");
				M3EventContext(ordinary).ShouldBeNull();
				M3EventContext(typed).ShouldBeNull();
				ordinary.Message.ShouldBe("massaged");
				typed.Message.ShouldBe("massaged");
				ordinary.RawMessage.ShouldBeNull();
				typed.Metadata.ShouldBeNull();
			}
		}

		[Fact]
		public void ShouldKeepM3EventMulticastFailureAtOneCapturedOutputBoundary()
		{
			var failing = new EventDestination(LogLevels.Trace);
			var successful = new EventDestination(LogLevels.Trace);
			var firstHandler = 0;
			var laterHandler = 0;
			var successfulCalls = 0;
			var reports = new List<LogFailureReport>();
			LogContext delivered = null;
			failing.LoggingEvent += (sender, args) => { firstHandler++; delivered = M3EventContext(args); throw new Exception("event-private"); };
			failing.LoggingEvent += (sender, args) => laterHandler++;
			successful.LoggingEvent += (sender, args) => successfulCalls++;
			Action<LogFailureReport> observer = report => reports.Add(report);
			Utilities.Logger.AddDestination(failing);
			Utilities.Logger.AddDestination(successful);
			Utilities.Logger.DispatchFailed += observer;
			var oldError = Console.Error;
			using (var error = new System.IO.StringWriter())
			{
				Console.SetError(error);
				try
				{
					var failure = Should.Throw<LogDispatchException>(() => Utilities.Logger.Info("captured event"));
					firstHandler.ShouldBe(1);
					laterHandler.ShouldBe(0);
					successfulCalls.ShouldBe(1);
					delivered.ShouldNotBeNull();
					delivered.Scopes.ShouldBeEmpty();
					var report = reports.ShouldHaveSingleItem();
					report.CoreCaptureFailureCount.ShouldBe(0);
					report.Failures.ShouldHaveSingleItem().RegistrationId.ShouldBe(1);
					report.Failures[0].Stage.ShouldBe(LogFailureStage.Output);
					failure.Report.CorrelationId.ShouldBe(report.CorrelationId);
					error.ToString().ShouldNotContain("event-private");
				}
				finally
				{
					Console.SetError(oldError);
					Utilities.Logger.DispatchFailed -= observer;
					Utilities.Logger.RemoveDestination(failing);
					Utilities.Logger.RemoveDestination(successful);
				}
			}
		}

		private sealed class M3OpaqueProperty
		{
			public int InspectionCalls;
			public override bool Equals(object other) { InspectionCalls++; throw new Exception("Property equality must not run."); }
			public override int GetHashCode() { InspectionCalls++; throw new Exception("Property hashing must not run."); }
			public override string ToString() { InspectionCalls++; throw new Exception("Property formatting must not run."); }
		}

		private static LogScopeHandle M3EventScope(IEnumerable<KeyValuePair<string, object>> properties)
		{
			var method = typeof(Utilities.Logger).GetMethod("BeginScope", new[] { typeof(LogAnnotations), typeof(IEnumerable<KeyValuePair<string, object>>) });
			return (LogScopeHandle)M3EventInvoke(method, new object[] { new LogAnnotations(new[] { new SensitivityLabel("scope") }), properties });
		}

		private static LogContext M3EventContext(object arguments)
		{
			arguments.ShouldNotBeNull();
			var property = arguments.GetType().GetProperty("Context");
			property.ShouldNotBeNull("M3 requires the reviewed event Context property.");
			property.PropertyType.ShouldBe(typeof(LogContext));
			property.GetSetMethod(true).ShouldBeNull();
			return (LogContext)property.GetValue(arguments);
		}

		private static object M3EventInvoke(System.Reflection.MethodInfo method, object[] arguments)
		{
			method.ShouldNotBeNull("M3 requires the exact reviewed public scope or annotated-log signature.");
			try { return method.Invoke(null, arguments); }
			catch (System.Reflection.TargetInvocationException failure)
			{
				System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw();
				throw;
			}
		}

		[Fact]
		public  void ShouldTriggerEventWhenLoggingMessage()
		{
			//setup
			var triggered = false;
			var dest = new EventDestination(LogLevels.Debug);
			dest.LoggingEvent += (sender, args) => { triggered = true; };
			Utilities.Logger.AddDestination(dest);

			//act
			Utilities.Logger.Debug("Hello World!");

			//assert
			triggered.Should().BeTrue();

			//cleanup
			Utilities.Logger.RemoveDestination(dest);

		}

	}
}
