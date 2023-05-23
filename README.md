# Running a Silica Listen Server
The Silica game [https://silicagame.com/news/welcome] currently only has P2P listen servers available. If you would like to host a P2P server while the host is afk then some automation is needed to select the gamemode at the beginning of each round.

## Silica Listen Server Requirements
- 50Mbps upload bandwidth available [speedtest.org] (Server only uses ~5Mbps download bandwidth)
- 32GB RAM
- Decent CPU

## Server Setup Instructions
1. Install MelonLoader using the Manual Installation method for 64-bit games [https://melonwiki.xyz/#/README?id=manual-installation]
2. Install the GamePriority Mod into your `Silica\Mods` directory
3. Set `SetGamePriorityToHigh = true` in your `Silica\UserData\MelonPreferences.cfg` file
4. Install AutoHotKey v2 [https://www.autohotkey.com/v2/]
5. Place the `AutoGamemodeSelect.ahk` script in your `Silica` directory
6. Optional: Configure the Logging option in the script. Default is to log who wins each round.
7. Right-click the `AutoGamemodeSelect.ahk` script and select `Run script`
8. Perform the script validation activity

## Script Validation Activity Instructions
1. Launch a Humans vs. Aliens mode Silica server
2. Activate the console (~)
3. Type `cheats` in the console
4. Type `delete structure` in the console. You should fall to the ground.
5. Type `destroy` in the console. You should see the red scratches across both teams now.
6. Use the hot-key Ctrl+Alt+Z. If everything goes well you should see a `Setup Validated` message.
7. If not then the `GameMidAdjustX` and `GameMidAdjustY` may need adjustment in the `GrabSilicaColors` function
