# v1.1.0
### Now with Generics!
Added the ability to log a custom object to a destination.  Currently only ```GenericEventDestination<T>``` was
created for testing purposes, however the point of a generic Logger is so you can put your custom metadata into 
your logs however/where you choose.  Immediate examples are having a custom database Destination, and you would want
to pass context of where/who/what is calling the log statement.  All of that context/metadata can be put into
a custom object class and call ```Logger.Info("information message", customMetadataObj);```, and then your custom
destination will parse out all the properties of your object, and log them accordingly.

There is no need to include `Message` or `Exception` in your custom object, as they are still required
parameters for using Logger and will be passed via the BaseLoggingDestination.


# v1.0.0
### Initial proper release.  
Contains functionality to log messages and exceptions.  See the README.MD for general information.
