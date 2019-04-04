using System;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
    [Collection("Logger is a Singleton")]
    public class GenericLoggerTests
    {
        [Fact]
        public void ShouldTriggerDebugOnDebug()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Debug);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            //act
            Utilities.Logger.Debug("Hello World!", true);

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerInfoOnInfo()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Information);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            //act
            Utilities.Logger.Info("Hello World!", true);

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerSecurityOnSecurity()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Security);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            //act
            Utilities.Logger.Security("Hello World!", true);

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerWarnOnWarn()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Warning);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            //act
            Utilities.Logger.Warn("Hello World!", true);

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerErrorOnError()
        {
            var triggered = false;
            var dest = new GenericEventDestination<bool>(LogLevels.Error);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata;
            Utilities.Logger.AddDestination(dest);

            //act
            Utilities.Logger.Error(new Exception("Hello World!"), true);

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }
    }
}
