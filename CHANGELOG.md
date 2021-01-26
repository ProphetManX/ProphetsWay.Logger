# v3.0.0
### Converted build/release pipelines into YML source files
Project's build and release pipleines are now written in YML files that are checked into the source code.
Removed build targets for unsupported versions of .Net frameworks (removed netcoreapp1.0, netcoreapp1.1, netcoreapp2.0, netcoreapp2.2, netcoreapp3.0.)
Removed UTF7 as a target filetype encoding for FileDestinations as this is obsolete, should be using UTF8.

# v2.2.0
### Build target for Net 5.0
Library now targets .Net 5.0

# v2.1.0
### Configurable LogLevels
You can now instanciate the Logger Destinations using the string or integer value of a given LogLevel.

Library now targets .NetFramework 4.8, .NetStandard 2.1 .NetCore 3.1


# v2.0.0
### LogLevel Overhaul and Specific LogLevel Targeting for Destinations
Modified the actual values of the LogLevels enum values, setting them in Binary so it's easier to understand how 
the logic works in the comparison functionality.  With these changes I have also created a subset of "xxxOnly" levels.
These new levels can be used to set your destination to only register log messages of that specific log level.
Now if you want to have all your ```Security``` logs written to a destination that your security officer will see, 
without cluttering the log with any ```Error``` or ```Warning``` messages that might have also occurred.  

Unfortunately the changes coming with this version will break any custom Destinations and code that ever used 
the ```Log``` or ```Log<T>``` methods.  My assumption is that this project has low adoption at this point
and any use of the pre-defined destinations aren't affected by these changes.  

Now all Log messages must be triggered via the "Shortcut" methods, and instead of triggering the original LogLevels
they now trigger the "Only" level.  This is the big change required to get Specific Level Targeting to work properly.


# v1.3.0
### Metadata Extensions
With the addition of Generics, I wanted to make an easier way for a user to log statements around their context/metadata
object.  Instead of requiring ```Logger.Info("information message", customMetadataObj);```, now you can modify your custom
object to implement the interface ```ILoggerMetadata```.  With this small change, logging becomes as easy as 
```customMetadataObj.Info("information message");```.

Cleaned up architecture of base logging destination classes.  I didn't like how the generic base inherited the regular base, and thus 
the generic base had both log functions implemented (```Log``` and ```Log<T>```, even tho only one was accessible). 
Created new base class for Destinations where the log level check logic, and the message massager can be shared across 
both base destinations.  


# v1.2.0
### Now with Generics!
Added the ability to log a custom object to a destination.  Currently only ```GenericEventDestination<T>``` was
created for testing purposes, however the point of a generic Logger is so you can put your custom metadata into 
your logs however/where you choose.  Immediate examples are having a custom database Destination, and you would want
to pass context of where/who/what is calling the log statement.  All of that context/metadata can be put into
a custom object class and call ```Logger.Info("information message", customMetadataObj);```, and then your custom
destination will parse out all the properties of your object, and log them accordingly.

There is no need to include `Message` or `Exception` in your custom object, as they are still required
parameters for using Logger and will be passed via the BaseLoggingDestination.


# v1.1.0
### Project Refactor
Changed a lot of properties on the .csproj file and updated the build/release pipelines for a more seamless setup.


# v1.0.0
### Initial proper release.  
Contains functionality to log messages and exceptions.  See the README.MD for general information.
