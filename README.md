C# / Unity Wii Remote API
=========================

This is an easy to use interface between Unity3D (or C# in general with minimal changes) and a Wii Remote controller.
The library uses a slightly modified version of [Signal11's HIDAPI](https://github.com/signal11/hidapi) to handle
low-level bluetooth communications.  In essence, the API itself is an implementation of the excellent
reverse-engineering effort done at [Wiibrew](http://wiibrew.org/wiki/Wiimote).  Here are some notable features of the
API:

- *Cross Platform*: The API is compatible with Windows (on the Microsoft and BlueSoleil bluetooth stacks), Mac, and
  Linux (only tested on Windows and Mac).
- *Fully Featured*: The API is capable of communicating and interpreting almost all useful data from the Wii Remote,
  including:
    - Basic Button Data (A, B, +, -, 1, 2, D-Pad, Home buttons)
    - 3-Axis Accelerometer reporting
    - IR Camera Data (including pointing position)
    - Extension Controller Support:
        - **Nunchuck**: Joystick data, C and Z buttons, Accelerometer data
        - **Wii Motion Plus**: Change in Pitch / Yaw / Roll.
        - More extension controllers coming soon!  Raw data also available for custom extension controllers.
    - Controlling the remote's 4 LEDs
    - Status reporting (battery level, player LED state, etc.)
    - More features coming soon!
- *Fully Documented*: The API comes with an example scene in Unity3D that makes use of all of the API's functions.  The
  API itself is well commented and comes with [Doxygen](http://www.stack.nl/~dimitri/doxygen/) documentation.
- *Open and Growing*: The API is licensed under the generous MIT license (see LICENSE.txt) so you can easily use it
  in your projects.  Source code access lets you debug easer.  Of course, it's also free!

Getting Started
---------------

The API itself is located in /Assets/Wiimote.  There, you can find all of the Native plugins
[Signal11's HIDAPI](https://github.com/signal11/hidapi) as well as the scripts that make up the API itself.  Simply drag
the Wiimote folder into your Unity project and you are good to go!

###HIDAPI Modifications

This API makes use of a slightly modified version of [Signal11's HIDAPI](https://github.com/signal11/hidapi).  The Windows
of HIDAPI was changed to handle a regression on the Microsoft bluetooth stack.  You can find the fixed version of HIDAPI
in the /hidapi-modified directory.  To read up on the specifics check out
[This Issue](https://github.com/signal11/hidapi/issues/218) on Github.

Future Changes
--------------

While the API is very powerful already, I would still like to make changes to it to improve it even more.  Namely I would
like to:

- Add support for all common extension controllers (Classic Controller / Pro, extend finish Wii Motion Plus support)
    - Add support for Nunchuck passthrough / Classic Controller passthrough mode on the Wii Motion Plus
- Add speaker support (no small feat!)

If you would like to help implement any of these changes, feel free to submit a pull request!