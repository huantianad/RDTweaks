# RDTweaks
*A [BepInEx](https://github.com/BepInEx/BepInEx) plugin containing small QoL changes for [Rhythm Doctor](https://rhythmdr.com/).*

Still a big work in progress!


## Features
- Skip to main menu, editor, or CLS on startup.
- Skip main menu logo.
- Skip to CLS library when entering.
- Scroll wheel in CLS!
- Hide mouse cursor while in game.
- Block mouse input while in game.
- Pixel font in CLS search and importer.
- Now works with [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager)!

## TODO
- Make startup tweaks not load warning scene for a frame.
- Animations for CLS actions (scrolling, randomizing).
- CLS folders?
- Maybe samurai
- Remove Herobrine
- A better readme.
- Better comments for config.

## Installation
1. Download the latest version of BepInEx 5 [here](https://github.com/BepInEx/BepInEx/releases). (Scroll down past the BepInEx 6 pre-release)
    - **Make sure you use the correct architecture for your system!**
      - If you are on a 32-bit version of Windows, select the x86 download.
      - If you are on a 64-bit version of Windows, select the x64 download. 
      - Otherwise, chose the unix download.
2. Unzip the file into your RD folder. You should have a `winhttp.dll`, `doorstop_config.ini`, and `BepInEx` folder next to Rhythm Doctor.exe.
3. Launch RD once to generate BepInEx files.
4. Download the latest version of the mod from [here](https://github.com/huantianad/RDTweaks/releases). It should be named `RDTweaks vx.x.x.zip`.
5. Unzip the file you downloaded into your Rhythm Doctor installation folder. You should now have a file at `BepInEx/Plugins/RDTweaks/RDTweaks.dll`.
6. Launch the game, and configure the mod in `BepInEx/Config`.
7. *Optional*: Install the [BepInEx Configuration Manager](https://github.com/BepInEx/BepInEx.ConfigurationManager) to configure the mod with a GUI.

For more information, check out the [BepInEx installation guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html).

## Development
In order to build the plugin, you need some external dlls to compile it, not included for copyright purposes.\
Get the `Rhythm Doctor/Rhythm Doctor_Data/Managed/Assembly-CSharp.dll` file from the game files,
then create a new folder at the project root named `Libs`, and put this file inside it.

You also will need the Unity and BepInEx assemblies, you can get these from their NuGet feed.
Add https://nuget.bepinex.dev/v3/index.json as a source and install the required packages there.

Alternatively, you can get the Unity and BepInEx assemblies from the game and BepInEx files.


For more information, check out the [BepInEx plugin creation guide](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html).
