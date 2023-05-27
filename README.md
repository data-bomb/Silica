# Running a Silica Listen Server
The Silica game [https://silicagame.com/news/welcome] currently only has listen servers available. If you would like to host a server while the host is afk then some automation is needed to select the gamemode at the beginning of each round.

## Silica Listen Server Requirements
- 50Mbps upload bandwidth available [speedtest.org] (Individual clients use about ~50kbps download bandwidth and ~1,300kbps upload bandwidth)
- 32GB RAM
- Decent CPU
- Top-of-the-line GPU if you want to host and play

## Server Setup Instructions
1. Install MelonLoader using the Manual Installation method for 64-bit games [https://melonwiki.xyz/#/README?id=manual-installation]
2. Install any other mods desired in your `Silica\Mods` directory

## MelonLoader Mods for Silica Listen Server
### Auto Teams Mode Select (Si_AutoTeamsSelect)
Install: Copy the `Si_AutoTeamsSelect.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `VersusAutoSelectMode`
- Valid configuration options are `"HUMANS_VS_HUMANS"`, `"HUMANS_VS_ALIENS"`, or `"HUMANS_VS_HUMANS_VS_ALIENS"`
- Automatically selects the mode of your choice each time the listen server restarts (allows near-headless operations)

### Auto Kick on Negative Kills (Si_AutoKickNegativeKills)
Install: Copy the `Si_AutoKickNegativeKills.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `AutoKickNegativeKillsThreshold`
- Valid configuration options are any negative number representing targeted negative kill threshold (default `-100`)
- Automatically detects players who go below the threshold, sends a chat message, then kicks the player

### GamePriority (https://github.com/MintLily/GamePriority/releases)
Install: Copy the `GamePriority.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `SetGamePriorityToHigh`
- Valid configuration options are `true` or `false`

### Friendly Fire Limits (Si_FriendlyFireLimits) - Currently Not Working
Install: Copy the `Si_FriendlyFireLimits.dll` into your `Silica\Mods` directory
- Directly hurting another player with bullets is blocked
- AoE for explosions still applies friendly fire
- Hurting friendly structures is limited (explosions still cause significant damage but bullets/chomps are more limited)

### Legacy AutoHotKey Install and Validation
1. Install AutoHotKey v2 [https://www.autohotkey.com/v2/]
2. Place the `AutoGamemodeSelect.ahk` script in your `Silica` directory
3. Optional: Configure the Logging option in the script. Default is to log who wins each round.
4. Launch a Humans vs. Aliens mode Silica server
5. Right-click the `AutoGamemodeSelect.ahk` script and select `Run script`
6. Activate the console (~)
7. Type `cheats` in the console
8. Type `delete structure` in the console. You should fall to the ground.
9. Type `destroy` in the console. You should see the red scratches across both teams now.
10. Use the hot-key Ctrl+Alt+Z. If everything goes well you should see a `Setup Validated` message.
11. If not then the `GameMidAdjustX` and `GameMidAdjustY` may need adjustment in the `GrabSilicaColors` function
