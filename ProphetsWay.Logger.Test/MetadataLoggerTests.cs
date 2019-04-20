using System;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.Generics;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
    [Collection("Logger is a Singleton")]
    public class MetadataLoggerTests
    {
        public class myMetadata : ILoggerMetadata
        {
            public bool State = true;
        }

        [Fact]
        public void ShouldTriggerDebugOnDebug()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Debug);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            //act
            ctx.Debug("Hello World!");

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerInfoOnInfo()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Information);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            //act
            ctx.Info("Hello World!");

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerSecurityOnSecurity()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Security);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            //act
            ctx.Security("Hello World!");

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerWarnOnWarn()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Warning);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            //act
            ctx.Warn("Hello World!");

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }

        [Fact]
        public void ShouldTriggerErrorOnError()
        {
            var triggered = false;
            var dest = new GenericEventDestination<myMetadata>(LogLevels.Error);
            dest.LoggingEvent += (sender, args) => triggered = args.Metadata.State;
            Utilities.Logger.AddDestination(dest);
            var ctx = new myMetadata();

            //act
            ctx.Error(new Exception("Hello World!"));

            //assert
            triggered.Should().BeTrue();

            //cleanup
            Utilities.Logger.RemoveDestination(dest);
        }
    }
}
