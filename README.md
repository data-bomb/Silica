# Running a Silica Listen Server
The Silica game [https://silicagame.com/news/welcome] was released in May 2023 with only Listen Server capability. Dedicated Server support is planned but finding a consistent and enjoyable experience on Listen Servers can be challenging. Some functionality is still missing from the base game that makes playing the game more enjoyable, and if you would like to host a Listen Server 24/7 then some additional automation is neccessary. The intention here is to go quick and provide functionality during the interim development period, so the quality of code is not a priority at the moment. Once dedicated servers are released then the approach to server-side modding may need to be re-visited.

Note: These mods are for hosts running Listen Servers only. Do not attempt to use any of these as a client. If you are a host and want to switch to a client then remove all of your MelonLoader Mods before connecting as a client!

## Mod Summary
| Mod Name | Version   | Link |
|---------:|-----------|------|
| Auto Teams Mode Select | 1.0.2 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/Si_AutoTeamsSelect/bin/Si_AutoTeamsSelect.dll) |
| Mapcycle | 1.0.1 | Broken |
| Surrender Command | 1.1.5 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/blob/main/Si_SurrenderCommand/bin/Si_SurrenderCommand.dll) |
| Auto Kick on Negative Kills | 1.0.5 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/blob/main/Si_AutoKickNegativeKills/bin/Si_AutoKickNegativeKills.dll) |
| Headquarterless Humans Lose | 1.0.6 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/blob/main/Si_HQlessHumansLose/bin/Si_HQlessHumansLose.dll) |
| Basic Team Balance | 0.9.2 | Broken |
| Basic Banlist | 1.0.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/blob/main/Si_BasicBanlist/bin/Si_BasicBanlist.dll) |
| GamePriority | 2.0.1 | [Download](https://github.com/MintLily/GamePriority/releases/download/2.0.1/GamePriority.dll) |


## Silica Listen Server Requirements
- 50Mbps upload bandwidth available [https://speedtest.org] (Individual clients use about ~50kbps download bandwidth and ~1,300kbps upload bandwidth)
- 64GB RAM
- Decent CPU
- Top-of-the-line GPU if you want to host and play

## Server Setup Instructions
1. Install .NET 6.0 Runtime x64 [https://dotnet.microsoft.com/en-us/download/dotnet/6.0]
2. Install MelonLoader using the Manual Installation method for 64-bit games [https://melonwiki.xyz/#/README?id=manual-installation]
3. Install any desired mods in your `Silica\Mods` directory

Note: The default location for `Silica.exe` will be in the `C:\Program Files (x86)\Steam\steamapps\common\Silica\` directory

## MelonLoader Mods for Silica Listen Server
### Auto Teams Mode Select (Si_AutoTeamsSelect)
Automatically selects the mode (e.g., Humans vs. Aliens) of your choice each time the listen server restarts. This is a must have for unattended operation and much more reliable than the initial approach of an AutoHotKey script.
- Install: Copy the `Si_AutoTeamsSelect.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `VersusAutoSelectMode`
- Valid configuration options are `"HUMANS_VS_HUMANS"`, `"HUMANS_VS_ALIENS"`, or `"HUMANS_VS_HUMANS_VS_ALIENS"`
- Testing Status: Confirmed working

### Mapcycle (Si_Mapcycle)
Automatically changes the map 20 seconds after the round ends to the next map in the cycle
- Install: Copy the `Si_Mapcycle.dll` into your `Silica\Mods` directory
- Launch the game then close it. This generates a `mapcycle.txt` file in your `Silica\UserData` directory
- Modify the mapcycle as desired. Repeating maps within the mapcycle is acceptable to make some maps more common than others.
- Note: Changing the mapcycle file currently requires a game restart for the change to be recognized.
- Testing Status: Users report extremely dark screens after the map change. May not be suitable for servers at this time until the underyling cause is fixed.

### Surrender Command (Si_SurrenderCommand)
Allows each team's commander to use a `!surrender` command to give up early
- Install: Copy the `Si_SurrenderCommand.dll` into your `Silica\Mods` directory
- Testing Status: Confirmed working

### Auto Kick on Negative Kills (Si_AutoKickNegativeKills)
Notifies everyone of team-kills in public chat and automatically kicks players who go below a certain kill point threshold
- Install: Copy the `Si_AutoKickNegativeKills.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `AutoKickNegativeKillsThreshold`
- Valid configuration options are any negative number representing targeted negative kill threshold (default `-80`)
- Testing Status: Kick functionality has not been encountered/tested at this time. Everything else tested.

### Headquarterless Humans Lose (Si_HQlessHumansLose)
Just like the Alien team losing their last Nest, when a Human team loses their last HQ then they are eliminated from the round.
- Install: Copy the `Si_HQlessHumansLose.dll` into your `Silica\Mods` directory
- Testing Status: Confirmed working

### Basic Team Balance (Si_BasicTeamBalance)
Only allows players to cause so much team imbalance; otherwise, this mod will deny the player's request to switch teams if it's too disruptive to balance. Current imbalance threshold is 3+ player difference between teams.
- Install: Copy the `Si_BasicTeamBalance.dll` into your `Silica\Mods` directory
- Testing Status: Causes major issues each time the round restarts where clients can't pick a team and have to rejoin the server. Requires additional investigation

### Basic Banlist (Si_BasicBanlist)
Retains memory of kicked players across server instances in a Json file.
- Install: Copy the `Si_BasicBanlist.dll` into your `Silica\Mods` directory
- Bans are stored in the `Silica\UserData\banned_users.json` file
- Testing Status: Mostly tested. Kick GUI functionality has not been encountered/tested at this time. Everything else tested.

### GamePriority (https://github.com/MintLily/GamePriority/releases)
Can automatically change the priority of the game executable upon launch
- Install: Copy the `GamePriority.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `SetGamePriorityToHigh`
- Valid configuration options are `true` or `false`
- Testing Status: Confirmed working

### Friendly Fire Limits (Si_FriendlyFireLimits) - Currently Not Working - Under Investigation
~~- Install: Copy the `Si_FriendlyFireLimits.dll` into your `Silica\Mods` directory~~
~~- Directly hurting another player with bullets is blocked~~
~~- AoE for explosions still applies friendly fire~~
~~- Hurting friendly structures is limited (explosions still cause significant damage but bullets/chomps are more limited)~~

### Thank You
Special thanks to Silentstorm, GrahamKracker, nighthalk and others in the MelonLoader community for being welcoming and supportive to an unusual use case of MelonLoader.
