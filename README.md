A simple C# wrapper for the Elgato Stream Deck

This project is a work-in-progress. This project is not affiliated with Elgato in any way.

## Usage
Modify the StreamApp project.

## Limitations
This is a very early proof-of-concept. Right now, solid colors can be sent to the Stream Deck. A short list of pending work items are:
* Allow images to be sent (both per-button and per-device)
* Build an interface or configuration file that defines the device behavior
* Support animation

## Credits
Some code and protocol information was used from https://github.com/Lange/node-elgato-stream-deck.

This also uses a modified version of this USB library: https://github.com/mikeobrien/HidLibrary. The code has been modified to listen for Windows events for USB changes instead of polling for them twice a second. This has great results for memory and CPU usage.
