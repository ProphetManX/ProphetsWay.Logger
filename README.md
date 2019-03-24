# ProphetsWay.Logger    [![Build status](https://dev.azure.com/ProphetsWay/ProphetsWay%20GitHub%20Projects/_apis/build/status/Logger/Logger%20CI)](https://dev.azure.com/ProphetsWay/ProphetsWay%20GitHub%20Projects/_build/latest?definitionId=5)

Logger is a quick to setup logging utility.  It is designed so that you can establish the destinations for your log messages to one or many different outputs, each targeting different log level severities (so you can have a log of Errors and Warnings separate from a logger dumping Debug and Info statements).

## Getting Started

These instructions will get you a copy of the project up and running on your local machine for development and testing purposes. See deployment for notes on how to deploy the project on a live system.

### Prerequisites

You can pull a copy of the source code from GitHub, or you can reference the library from NuGet.org from within Visual Studio.

```
Install-Package ProphetsWay.Logger 
dotnet add package ProphetsWay.Logger 
```

### Referencing and Using

Because this is the first utility of a handful, they will all exist in the namespace of "ProphetsWay.Utilities".  Since the Logger is a static object, you can use it throughout your software to log messages without having to worry about any pre-configuration.  However the point is to be able to create LoggerDestinations where ever you want, with a minimum specified LogLevel severity.  You should establish your LoggerDestinations when your project initializes, so that the destinations are only created once.

```
using ProphetsWay.Utilities;
```

To create a simple Console Logging Destination, just instantiate a new "ConsoleDestination" class, and add it to the logger.

```
Logger.AddDestination(new ConsoleDestination());
```
or
```
var dest = new ConsoleDestination();
Logger.AddDestination(dest);
```

Destinations can be added and removed so long as you keep a reference to the object from when you initially added it.  There is also the option to "Clear" all current destinations and simply add new ones.

```
Logger.RemoveDestination(dest);
```
and
```
Logger.ClearDestinations();
```

The built in destinations consist of ConsoleDestination, FileDestination, and EventDestination.  There is also a base abstract class BaseLoggingDestination that anyone can use to inherit and implement their own custom destination.

ConsoleDestination will simply dump the log statement to your console, for both the console and file destinations, there is a text formatter that will append a timestamp and the log level to the message before it is rendered to the console.

```
Hello World!
```
is transformed into
```
3/15/2019 11:09:33 PM ::        Debug:  Hello World!
```
with the log level adding padding, so that scrolling thru the text will align timestamps, levels, and first characters of your message.  Exceptions are also dumped into your logs as well.
```
3/15/2019 11:25:41 PM ::        Error:  Another generic message about an error occuring. (friendly message to show a UI maybe?)
This exception has an inner exception. (likely details to hide from a UI)

Inner Exception Message:
This is a specific Exception Message and will contain a stack trace.
```

A FileDestination requires a target filename to dump the logs into.  If you want to clear the file on launch of your application (so that the file only contains logs from the 'last run') you can leave the reset flag as true, or if you want to have a running log, you can set it to false.
```
var fileDest = new FileDestination("Warnings.log", LogLevels.Warning, false);
```
A final additional parameter is available to specify the text Encoder to be used.  The following are supported
* ASCII
* BigEndianUnicode
* Unicode
* UTF7
* UTF8
* UTF32

UTF8 is set by default.

The last destination available by default is the EventDestination.  With this you will create the destination and then assign a delegate to the EventHandler.  As a valid statement is logged, the event will trigger, and you can handle the message however you please.  Generally this is the destination I use in my UI's.  I will use Dispatcher to invoke the UI to render the log messages into a UI control for the user to see.
```
var evtDest = new EventDestination(LogLevels.Debug);
evtDest.LoggingEvent += (sender, eventArgs) => { /* whatever you want to do with the message here */ };
```
For simple actions in specific situations, the EventDestination is likely your best option.  However if you have some specific functionality that you will use in a few different projects specific to your situation, you can also create your own personalized destinations.  An example of a custom  destination might be to create a specific database destination that is tightly coupled to a solution you are working in across multiple projects.

The BaseLoggingDestination requires an argument of LogLevel to be established, so any log statement with a severity of what was chosen or higher will be logged at that destination.  The log levels are as follows:

1. Debug
1. Information
1. Security
1. Warning
1. Error

If you set your destination to LogLevels.Debug, then all messages currently supported will be logged in your destination.  If you choose LogLevels.Warning, then only Warning and Error logs will be logged in your destination.

You can have as many destinations as you wish; if you want to create two log files, one for Warnings and Errors, and a second one for all your Debug/Info logs, you only have to create two separate FileDestinations and add them both to the Logger.
```
var debugDest = new FileDestination("Debug.log", LogLevels.Debug);
var warnDest = new FileDestination("Warnings.log", LogLevels.Warning);
Logger.AddDestination(debugDest);
Logger.AddDestination(warnDest);
```


## Running the tests

The library has 25 unit tests currently.  I tried to cover everything possible.  They are created with XUnit and utilize Moq for two tests.  The Test project is included in this repository, as well as an Example project.


## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/ProphetManX/ProphetsWay.Logger/tags). 

## Authors

* **G. Gordon Nasseri** - *Initial work* - [ProphetManX](https://github.com/ProphetManX)

See also the list of [contributors](https://github.com/ProphetManX/ProphetsWay.Logger/graphs/contributors) who participated in this project.

## License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details


