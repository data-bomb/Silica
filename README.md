# Running a Silica Listen Server
The Silica game [https://silicagame.com/news/welcome] was released in May 2023 with only Listen Server capability. Dedicated Server support is planned but finding a consistent experience on Listen Servers can be challenging. If you would like to host a Listen Server 24/7 then some additional automation is neccessary.

## Silica Listen Server Requirements
- 50Mbps upload bandwidth available [https://speedtest.org] (Individual clients use about ~50kbps download bandwidth and ~1,300kbps upload bandwidth)
- 32GB RAM
- Decent CPU
- Top-of-the-line GPU if you want to host and play

## Server Setup Instructions
1. Install MelonLoader using the Manual Installation method for 64-bit games [https://melonwiki.xyz/#/README?id=manual-installation]
2. Install any other mods desired in your `Silica\Mods` directory

## MelonLoader Mods for Silica Listen Server
### Auto Teams Mode Select (Si_AutoTeamsSelect)
Automatically selects the mode (e.g., Humans vs. Aliens) of your choice each time the listen server restarts (allows near-headless operations)
- Install: Copy the `Si_AutoTeamsSelect.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `VersusAutoSelectMode`
- Valid configuration options are `"HUMANS_VS_HUMANS"`, `"HUMANS_VS_ALIENS"`, or `"HUMANS_VS_HUMANS_VS_ALIENS"`

### Mapcycle (Si_Mapcycle)
Automatically changes the map 20 seconds after the round ends to the next map in the cycle
- Install: copy the `Si_Mapcycle.dll` into your `Silica\Mods` directory
- Launch the game then close it. This generates a `mapcycle.txt` file in your `Silica\UserData` directory
- Modify the mapcycle as desired. Repeating maps within the mapcycle is acceptable to make some maps more common than others.
- Note: Changing the mapcycle file currently requires a game restart for the change to be recognized.

### Auto Kick on Negative Kills (Si_AutoKickNegativeKills)
Automatically detects players who go below the threshold, sends a chat message, then kicks the player
- Install: Copy the `Si_AutoKickNegativeKills.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `AutoKickNegativeKillsThreshold`
- Valid configuration options are any negative number representing targeted negative kill threshold (default `-100`)

### GamePriority (https://github.com/MintLily/GamePriority/releases)
Can automatically change the priority of the game executable upon launch
- Install: Copy the `GamePriority.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `SetGamePriorityToHigh`
- Valid configuration options are `true` or `false`

### Friendly Fire Limits (Si_FriendlyFireLimits) - Currently Not Working - Under Investigation
~~- Install: Copy the `Si_FriendlyFireLimits.dll` into your `Silica\Mods` directory~~
~~- Directly hurting another player with bullets is blocked~~
~~- AoE for explosions still applies friendly fire~~
~~- Hurting friendly structures is limited (explosions still cause significant damage but bullets/chomps are more limited)~~

### Thank You
Special thanks to Silentstorm, GrahamKracker, nighthalk and others in the MelonLoader community for being very supportive.
