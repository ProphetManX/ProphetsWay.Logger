using System;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
    [Collection("Logger is a Singleton")]
    public class BasicTests
    {
        [Fact]
        public void ShouldContainMessage()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Debug);
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);

            //act
            Utilities.Logger.Debug(msg);


            //assert 
            evtMessage.Should().Contain(msg);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldContainException()
        {
            //setup
            var e = new Exception("Hello World!");
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Error);
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);

            //act
            Utilities.Logger.Error(e, "Goodbye Everyone");

            //assert
            evtMessage.Should().Contain(e.Message);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldContainMessageAndException()
        {
            //setup
            const string msg = "Goodbye Everyone";
            var e = new Exception("Hello World!");
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Error);
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);

            //act
            Utilities.Logger.Error(e, msg);

            //assert
            evtMessage.Should().Contain(msg).And.Contain(e.Message);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldContainMessageLevelNamed()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination("Debug");
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);

            //act
            Utilities.Logger.Debug(msg);


            //assert 
            evtMessage.Should().Contain(msg);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldNotContainMessageLevelNamed()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination("NOT_VALID");
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);

            //act
            Utilities.Logger.Debug(msg);


            //assert 
            evtMessage.Should().NotContain(msg);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldContainMessageLevelInt()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination(LogLevels.Debug.GetHashCode());
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);

            //act
            Utilities.Logger.Debug(msg);


            //assert 
            evtMessage.Should().Contain(msg);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldNotContainMessageLevelInt()
        {
            //setup
            const string msg = "Hello World!";
            var evtMessage = string.Empty;
            var d = new EventDestination(-1);
            d.LoggingEvent += (sender, args) => evtMessage = args.Message;
            Utilities.Logger.AddDestination(d);

            //act
            Utilities.Logger.Debug(msg);


            //assert 
            evtMessage.Should().NotContain(msg);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }
    }
}