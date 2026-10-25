using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using ProphetsWay.Utilities;
using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Tests
{
	[Collection(MicrosoftInboundTestCollection.Name)]
	public class MicrosoftLoggerFactoryExtensionsTests
	{
		private readonly InboundTestEnvironment _environment;

		public MicrosoftLoggerFactoryExtensionsTests(InboundTestEnvironment environment)
		{
			_environment = environment;
		}

		[Fact]
		public void ShouldRegisterAndReturnTheSameExplicitInboundProvider()
		{
			using (var routes = new InboundRoutes(_environment, typeof(string), null, LogLevels.Trace, LogLevels.Trace))
			{
				var factory = new SyntheticLoggerFactory();
				object provider = null;
				try
				{
					var extension = RequiredExtensionMethod();
					provider = InboundTestSupport.Invoke(extension, null, new object[] { factory, null });
					provider.ShouldNotBeNull();
					factory.Providers.ShouldHaveSingleItem().ShouldBeSameAs(provider);
					factory.DisposeCalls.ShouldBe(0);

					var logger = InboundTestSupport.CreateLogger(provider, "factory category");
					logger.Log(LogLevel.Information, default(EventId), "factory state", null,
						(state, exception) => "factory formatted");
					routes.Recipient.Records.ShouldHaveSingleItem();
					routes.Recipient.Records[0].Message.ShouldBe("factory formatted");
					InboundTestSupport.Read<string>(routes.Recipient.Records[0].Metadata, "CategoryName")
						.ShouldBe("factory category");
					routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
					routes.OrdinaryRecipient.Calls.ShouldBe(0);
				}
				finally
				{
					if (provider != null) InboundTestSupport.DisposeProvider(provider);
				}
			}
		}

        [Fact]
        public void ShouldRejectNullFactoryWithArgumentNullExceptionNamedFactory()
        {
            var extension = RequiredExtensionMethod();
            var thrown = Should.Throw<ArgumentNullException>(() =>
                InboundTestSupport.Invoke(extension, null, new object[] { null, null }));
            thrown.ParamName.ShouldBe("factory");
            _environment.AssertNoAutomaticOutput();
        }
		[Fact]
		public void ShouldRetireCreatedProviderAndPreserveFactoryErrorWhenRegistrationFails()
		{
			using (var routes = new InboundRoutes(_environment, typeof(string), null, LogLevels.Trace, LogLevels.Trace))
			{
				var failure = new InvalidOperationException("synthetic factory registration failure");
				var factory = new SyntheticLoggerFactory(failure);
				try
				{
					var extension = RequiredExtensionMethod();
					var thrown = Should.Throw<InvalidOperationException>(() =>
						InboundTestSupport.Invoke(extension, null, new object[] { factory, null }));
					thrown.ShouldBeSameAs(failure);
					factory.DisposeCalls.ShouldBe(0);
					factory.Providers.ShouldHaveSingleItem();
					var inertLogger = InboundTestSupport.CreateLogger(factory.Providers[0], "failed registration");
					inertLogger.IsEnabled(LogLevel.Error).ShouldBeFalse();
					var formatterCalls = 0;
					inertLogger.Log(LogLevel.Error, default(EventId), "state", null, (state, exception) =>
					{
						formatterCalls++;
						return "not delivered";
					});
					formatterCalls.ShouldBe(0);
					routes.Recipient.Records.ShouldBeEmpty();
					routes.UnexpectedRecipients.ShouldAllBe(recipient => recipient.Records.Count == 0);
					routes.OrdinaryRecipient.Calls.ShouldBe(0);
				}
				finally
				{
					foreach (var provider in factory.Providers)
						InboundTestSupport.DisposeProvider(provider);
				}
			}
		}

		private static MethodInfo RequiredExtensionMethod()
		{
			var extensionType = InboundTestSupport.RequiredType("MicrosoftLoggerFactoryExtensions");
			extensionType.IsPublic.ShouldBeTrue();
			extensionType.IsAbstract.ShouldBeTrue();
			extensionType.IsSealed.ShouldBeTrue();
			var publicExtensions = extensionType.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
			publicExtensions.Select(candidate => candidate.Name).ShouldBe(new[] { "AddProphetsWay" });
			var method = extensionType.GetMethod("AddProphetsWay", BindingFlags.Public | BindingFlags.Static);
			method.ShouldNotBeNull("The reviewed factory extension is AddProphetsWay.");
			method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false).ShouldBeTrue();
			method.IsGenericMethod.ShouldBeFalse();
			var parameters = method.GetParameters();
			parameters.Length.ShouldBe(2);
			parameters[0].ParameterType.ShouldBe(typeof(ILoggerFactory));
			parameters[0].Name.ShouldBe("factory");
			var mapperType = InboundTestSupport.RequiredType("IMicrosoftLogLabelMapper");
			parameters[1].ParameterType.ShouldBe(mapperType);
			parameters[1].Name.ShouldBe("labelMapper");
			parameters[1].IsOptional.ShouldBeTrue();
			parameters[1].HasDefaultValue.ShouldBeTrue();
			parameters[1].DefaultValue.ShouldBeNull();
			method.ReturnType.ShouldBe(InboundTestSupport.RequiredType("MicrosoftLoggerProvider"));
			return method;
		}

		private sealed class SyntheticLoggerFactory : ILoggerFactory
		{
			private readonly Exception _registrationFailure;

			public SyntheticLoggerFactory(Exception registrationFailure = null)
			{
				_registrationFailure = registrationFailure;
			}

			public List<ILoggerProvider> Providers { get; } = new List<ILoggerProvider>();
			public int DisposeCalls { get; private set; }

			public void AddProvider(ILoggerProvider provider)
			{
				Providers.Add(provider);
				if (_registrationFailure != null) throw _registrationFailure;
			}

			public ILogger CreateLogger(string categoryName)
			{
				throw new NotSupportedException("The bridge tests use the registered provider directly.");
			}

			public void Dispose()
			{
				DisposeCalls++;
			}
		}
	}
}
