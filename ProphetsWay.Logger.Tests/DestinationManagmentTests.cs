using Shouldly;
using Xunit;

namespace ProphetsWay.Logger.Tests
{
	[Collection("Logger is a Singleton")]
	public class DestinationManagementTests
	{
		[Fact]
		public void ShouldAddDestination()
		{
			using (var fixture = new DestinationManagementFixture())
			{
				//setup
				var triggered = false;
				var dest = fixture.CreateDestination();
				fixture.ObserveDestination(dest, (sender, args) => triggered = true);

				//act
				fixture.AddDestination(dest);
				fixture.Debug("Hello World!");

				//assert
				triggered.ShouldBeTrue();

				//cleanup
				fixture.RemoveDestination(dest);
			}
		}

		[Fact]
		public void ShouldRemoveDestination()
		{
			using (var fixture = new DestinationManagementFixture())
			{
				//setup
				var triggered = false;
				var dest = fixture.CreateDestination();
				fixture.ObserveDestination(dest, (sender, args) => triggered = true);

				//act
				fixture.AddDestination(dest);
				fixture.RemoveDestination(dest);
				fixture.Debug("Hello World!");

				//assert
				triggered.ShouldBeFalse();
			}
		}

		[Fact]
		public void ShouldClearAllDestinations()
		{
			using (var fixture = new DestinationManagementFixture())
			{
				//setup
				var triggered = false;
				var dest1 = fixture.CreateDestination();
				fixture.ObserveDestination(dest1, (sender, args) => triggered = true);
				var dest2 = fixture.CreateDestination();
				fixture.ObserveDestination(dest2, (sender, args) => triggered = true);
				var dest3 = fixture.CreateDestination();
				fixture.ObserveDestination(dest3, (sender, args) => triggered = true);
				fixture.ClearDestinations();

				//act
				fixture.AddDestination(dest1);
				fixture.AddDestination(dest2);
				fixture.AddDestination(dest3);
				fixture.ClearDestinations();
				fixture.Debug("Hello World!");

				//assert
				triggered.ShouldBeFalse();
			}
		}

		[Fact]
		public void ShouldRemoveDestinationEvenIfNotInList()
		{
			using (var fixture = new DestinationManagementFixture())
			{
				//setup
				var dest = fixture.CreateDestination();

				//act
				fixture.RemoveDestination(dest);

				//assert that no exception was thrown
			}
		}
	}
}
