# TeamsHassBridge
Teams Log Watcher

This is a C# program that reads the Teams log file and parses it into a simple JSON object representing the current state of Teams.

It will PUT a JSON formatted version of TeamsStatus to an endpoint of your choice. You'll need to edit the URL in App.config.

On launch it will read through the entire log to try and work out the current state, and then it will listen to the log in real time from then on, PUTting to the endpoint on change

I use this to PUT my status to Home Assistant via Node Red (hense the name of this project)