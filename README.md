# Witcher Direct Combat

An experimental controller and combat mod for **The Witcher: Enhanced Edition** on Windows. This beta widens the game's sword combo timing window and maps an Xbox controller to movement, camera, sword styles, dodging, and Signs. You can press an attack button repeatedly without watching for the flaming sword cursor. The installer offers a bundled unsigned controller executable or the option to compile it from source on your PC.

This is an early community beta, not a replacement for The Witcher 1 combat engine. The game still chooses targets under the cursor, resolves hits and parries, and plays its original attack animations. There is no lock-on targeting. It has been tested with the Steam build whose game window reports `1.4.5.1304`, in F3 over-the-shoulder view.

## Download and install

1. Download the repository as a ZIP from GitHub and extract it.
2. Close Witcher completely.
3. Double-click `Install.cmd`. The first prompt defaults to **P** so you can paste your game folder path; choose **S** to search common Steam library locations. Spaces and surrounding quotes are accepted. The installer verifies `System\witcher.exe` and asks you to confirm the exact folder.
4. Choose **U** to install the bundled unsigned controller executable (default), or **C** to compile the included C# source yourself.
5. Double-click `DirectCombat\Play.cmd` inside the game folder to start the controller bridge and Witcher.

The compile option builds [`DirectCombatController.cs`](Payload/DirectCombatController.cs) with the Windows .NET Framework C# compiler. The bundled executable is built from that same source and is not digitally signed. Both options install our `testtools.lua` and launcher. The installer refuses to overwrite an existing `System\Scripts\testtools.lua` that differs from ours. It does not change saves or the game executable. It saves the verified game path as `location.txt` beside `Install.cmd` and `Uninstall.cmd` in the extracted installer folder. Keep that folder until you uninstall. To remove the beta, run `Uninstall.cmd`; it reads `location.txt`, waits for Witcher to close if needed, stops the installed controller bridge, and removes only files that still match the install manifest.

## Controller layout

| Input | Action |
| --- | --- |
| Left stick | Move Geralt (F3 view) |
| Right stick | Turn the camera / cursor in F3 view |
| A | Fast style and attack |
| X | Strong style and attack |
| Y | Group style and attack |
| B + left stick direction | Dodge in that direction |
| Menu (hamburger) button | Pause or resume combat (Space) |
| LT | Alternate steel sword (`Q`) and extra weapon 1 (`R`) |
| D-pad Up | Select and cast Aard |
| D-pad Right / Down / Left | Select Igni / Yrden / Quen |
| LB | Select Axii |
| RB or RT | Hold, then let go to cast the selected Sign |

Use Fast against agile opponents, Strong against tough slow opponents, and Group against crowds. The game may parry attacks made with a poor style match. Sword attacks still require an enemy under the center cursor. The first LT press selects the steel sword; the next selects extra weapon 1. This assumes the extra weapon is in the first extra slot. The Q/R toggle and corrected Sign selection keys still need broader in-game confirmation.

## How it works

The controller bridge reads XInput and sends the game's existing keyboard and mouse commands while a window titled `The Witcher (...)` is active. The Lua startup hook sets `g_cAuroraSettings.m_bUseOldAttackMechanics = true` and `g_cAuroraSettings.m_fCombatTimeAdvance = 10.0`. The wider timing window is what lets repeated presses continue the flaming combo without watching its cue. Game behavior and balance outside the tested fights may vary.

The controller source is [`Payload/DirectCombatController.cs`](Payload/DirectCombatController.cs). The Lua source is [`Payload/testtools.lua`](Payload/testtools.lua). The package contains a prebuilt unsigned controller executable and no original game files. Source code is MIT licensed; The Witcher and its assets belong to their respective owners.

## Known limitations

- Requires 64-bit Windows and an XInput controller. Compiling from source also requires the .NET Framework 4 C# compiler. The bridge requests F3 over-the-shoulder view once when Witcher first becomes active; the player confirmed the right stick turns the camera normally in F3. If you change camera views later, press F3 on the keyboard to return.
- The game can still parry or miss; Fast style landed more hits than Strong against one agile test enemy.
- The dodge needs a sword drawn and may require holding B while moving the stick.
- LT toggles Q/R based on the bridge's own presses. Keyboard or UI weapon changes can put its next choice out of sync.
- D-pad Up casting Aard and repeated X/Y flaming combos were confirmed in play; the corrected Sign selection keys and Q/R weapon toggle need further playtesting.
- The bridge uses controller slot 0. It waits up to 30 seconds for `witcher.exe` to start. During play it reuses the controller input it already reads: after 30 seconds without controller input it checks whether the remembered Witcher process still exists, repeating only every 30 seconds while idle. A player can put the controller down and pick it up again while the game remains open. The bridge has a 24-hour maximum session length.
- The launcher and controller program prevent a second bridge instance; an earlier beta launcher could start duplicates and make right-stick camera movement erratic.
- The player confirmed the Menu button toggles combat pause on and off.

Bug reports and improvements are welcome. Please include the game version, controller type, camera view, and exact buttons pressed.
