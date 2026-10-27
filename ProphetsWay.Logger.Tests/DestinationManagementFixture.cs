using System;

namespace ProphetsWay.Logger.Tests
{
	internal sealed class DestinationManagementFixture : IDisposable
	{
		private readonly AutomaticFileIntegrationFixture _fixture;
		private readonly Type _destinationType;
		private readonly Type _destinationInterface;
		private readonly object _debugLevel;

		public DestinationManagementFixture()
		{
			_fixture = new AutomaticFileIntegrationFixture();
			try
			{
				_fixture.BindUnusedOwner(
					() => _fixture.Files.PrimaryDirectory,
					() => _fixture.Files.LocalRoot,
					() => new DateTime(2031, 2, 3, 4, 5, 6, DateTimeKind.Utc),
					() => new Guid("01234567-89ab-cdef-0123-456789abcdef"),
					_fixture.Files.CreateDirectory,
					(path, mode, access, share) => _fixture.Files.Open(path, mode, access, share));
				_destinationType = _fixture.ProductAssembly.GetType("ProphetsWay.Utilities.LoggerDestinations.EventDestination", true);
				_destinationInterface = _fixture.ProductAssembly.GetType("ProphetsWay.Utilities.ILoggingDestination", true);
				_debugLevel = Enum.Parse(_fixture.ProductAssembly.GetType("ProphetsWay.Utilities.LogLevels", true), "Debug");
			}
			catch
			{
				_fixture.Dispose();
				throw;
			}
		}

		public object CreateDestination()
		{
			return Activator.CreateInstance(_destinationType, new[] { _debugLevel });
		}

		public void ObserveDestination(object destination, Action<object, object> observer)
		{
			_fixture.ObserveDestination(destination, observer);
		}

		public void AddDestination(object destination)
		{
			_fixture.InvokePublic("AddDestination", Type.EmptyTypes, new[] { _destinationInterface }, destination);
		}

		public void RemoveDestination(object destination)
		{
			_fixture.InvokePublic("RemoveDestination", Type.EmptyTypes, new[] { _destinationInterface }, destination);
		}

		public void ClearDestinations()
		{
			_fixture.InvokePublic("ClearDestinations", Type.EmptyTypes, Type.EmptyTypes);
		}

		public void Debug(string message)
		{
			_fixture.InvokePublic("Debug", Type.EmptyTypes, new[] { typeof(string) }, message);
		}

		public void Dispose()
		{
			_fixture.Dispose();
		}
	}
}
