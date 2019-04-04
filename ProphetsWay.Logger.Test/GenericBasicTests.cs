using System;
using FluentAssertions;
using ProphetsWay.Utilities;
using ProphetsWay.Utilities.LoggerDestinations;
using Xunit;

namespace ProphetsWay.Logger.Test
{
	[Collection("Logger is a Singleton")]
	public class GenericBasicTests
	{
        public class TestObject
        {
            public TestObject(string name, int val)
            {
                Name = name;
                Value = val;
            }

            public string Name { get; }
            public int Value { get; }
        }

		[Fact]
		public void ShouldContainMessage()
		{
			//setup
			const string msg = "Hello World!";
			var evtMessage = string.Empty;
			var d = new GenericEventDestination<TestObject>(LogLevels.Debug);
			d.LoggingEvent += (sender, args) => evtMessage = args.Message;
			Utilities.Logger.AddDestination(d);
            var obj = new TestObject("blarg", 5);

			//act
			Utilities.Logger.Log(LogLevels.Debug, obj, msg, new Exception("Custom Exception"));
			

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
			var d = new GenericEventDestination<TestObject>(LogLevels.Error);
			d.LoggingEvent += (sender, args) => evtMessage = args.Exception.Message;
			Utilities.Logger.AddDestination(d);
            var obj = new TestObject("what?", 67);

            //act
            Utilities.Logger.Log(LogLevels.Error, obj, "Goodbye Everyone", e);

			//assert
			evtMessage.Should().Contain(e.Message);

			//cleanup
			Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldContainMetadata()
        {
            //setup
            const string msg = "Goodbye Everyone";
            var e = new Exception("Hello World!");
            TestObject evtObject = null;
            var d = new GenericEventDestination<TestObject>(LogLevels.Error);
            d.LoggingEvent += (sender, args) => evtObject = args.Metadata;
            Utilities.Logger.AddDestination(d);
            var obj = new TestObject("ok", 2);

            //act
            Utilities.Logger.Log(LogLevels.Error, obj, msg, e);

            //assert
            evtObject.Should().NotBeNull();
            evtObject.Name.Should().Be(obj.Name);
            evtObject.Value.Should().Be(obj.Value);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }

        [Fact]
        public void ShouldContainMessageExceptionAndMetadata()
        {
            //setup
            const string msg = "Goodbye Everyone";
            var e = new Exception("Hello World!");
            var evtMessage = string.Empty;
            var exMessage = string.Empty;
            TestObject evtObject = null;
            var d = new GenericEventDestination<TestObject>(LogLevels.Error);
            d.LoggingEvent += (sender, args) =>
            {
                evtMessage = args.Message;
                evtObject = args.Metadata;
                exMessage = args.Exception.Message;
            };

            Utilities.Logger.AddDestination(d);
            var obj = new TestObject("ok", 2);

            //act
            Utilities.Logger.Log(LogLevels.Error, obj, msg, e);

            //assert
            evtMessage.Should().Contain(msg);
            exMessage.Should().Contain(e.Message);
            evtObject.Should().NotBeNull();
            evtObject.Name.Should().Be(obj.Name);
            evtObject.Value.Should().Be(obj.Value);

            //cleanup
            Utilities.Logger.RemoveDestination(d);
        }
    }
}
