# RDTweaks
*A [BepInEx](https://github.com/BepInEx/BepInEx) plugin which contains a collection of tweaks for RD.*

Still a big work in progress!


## Features
- Skip to main menu, editor, or CLS on startup.
- Skip to main menu, past the logo.
- CLS level randomizer (default key: R)
- Skip to CLS library when entering.
- Scroll wheen in CLS!
- Automatically swap P1 and P2 so P1 is on the left like a sane person.
- Geeze writing this is kinda hard I'll polish this later
- I'll probably add more features soontm but uhh 
  - coding hard

## TODO
- Make startup tweaks not load warning scene for a frame.
- Animations for CLS actions (scrolling, randomizing).
- CLS folders?
- Maybe samurai
- Remove Herobrine
- A better readme.
- Better comments for config.

## Installation
1. Download the latest version of BepInEx 5 **x86** [here](https://github.com/BepInEx/BepInEx/releases).\
**Make sure you use the x86 version!** RD is x86 so the x64 version of BepInEx will not work.
2. Unzip the file into your RD folder. You should have a `winhttp.dll`, `doorstop_config.ini`, and `BepInEx` folder next to Rhythm Doctor.exe.
3. Launch RD once to generate BepInEx files.
4. Download the latest version of the mod from [here](https://github.com/huantianad/RDTweaks/releases). It should be named `RDTweaks vx.x.x.zip`.
5. Unzip the file you downloaded into your Rhythm Doctor installation folder. You should now have a file at `BepInEx/Plugins/RDTweaks/RDTweaks.dll`.
6. Launch the game, and configure the mod in `BepInEx/Config`.

For more information, check out the [BepInEx installation guide](https://docs.bepinex.dev/articles/user_guide/installation/index.html).

## Development
In order to build the game dll, you need some external dlls to compile it, not included for copyright purposes.\
Get these files from the Rhythm Doctor and BepInEx installation, and put it in a Libs folder, then add all the dlls as references.\
- `BepInEx/core`
  - `0Harmony.dll`
  - `BepInEx.dll`
  - `BepInEx.Harmony.dll`
- `Rhythm Doctor_Data/Managed/`
  - `Assembly-CSharp.dll`
  - `UnityEngine.AudioModule.dll`
  - `UnityEngine.CoreModule.dll`
  - `UnityEngine.dll`
  - `UnityEngine.InputLegacyModule.dll`
  - `UnityEngine.UI.dll`


For more information, check out the [BepInEx plugin creation guide](https://docs.bepinex.dev/articles/dev_guide/plugin_tutorial/index.html).
