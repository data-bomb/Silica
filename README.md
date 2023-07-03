# Running a Silica Listen Server
The Silica game [https://silicagame.com/news/welcome] was released in May 2023 with only Listen Server capability. Dedicated Server support is planned but finding a consistent and enjoyable experience on Listen Servers can be challenging. Some functionality is still missing from the base game that makes playing the game less enjoyable and prone to balance and abuse issues, and if you would like to host a Listen Server 24/7 then some additional automation is neccessary. The intention here is to go quick and provide functionality during the interim development period, so the quality of code is not a priority at the moment. Once dedicated servers are released then the approach to server-side modding may need to be re-visited.

**Note:** *These mods are for hosts running Listen Servers only. Do not attempt to use any of these as a client. If you are a host and want to switch to a client then remove all of your MelonLoader Mods before connecting as a client!*

## Mod Summary
| Mod Name | Version   | Link |
|---------:|-----------|------|
| Admin Mod | 1.0.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AdminMod/bin/Si_AdminMod.dll) |
| Auto Teams Mode Select | 1.0.4 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AutoTeamsSelect/bin/Si_AutoTeamsSelect.dll) |
| Mapcycle | 1.0.1 | Not Ready - Game Bug(s) |
| SiRAC (Anti-Cheat) | 0.7.6 | Restricted Access - By Request Only |
| Surrender Command | 1.1.7 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_SurrenderCommand/bin/Si_SurrenderCommand.dll) |
| Anti-Grief | 1.0.5 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AutoKickNegativeKills/bin/Si_AutoKickNegativeKills.dll) |
| Headquarterless Humans Lose | 1.0.6 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_HQlessHumansLose/bin/Si_HQlessHumansLose.dll) |
| Basic Team Balance | 1.0.7 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_BasicTeamBalance/bin/Si_BasicTeamBalance.dll) |
| Basic Banlist | 1.0.1 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_BasicBanlist/bin/Si_BasicBanlist.dll) |
| Commander Management | 1.0.2 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_CommManagement/bin/Si_CommManagement.dll) |
| AFK Manager | 0.8.1 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AFKManager/bin/Si_AFKManager.dll) |
| Logging | 0.8.8 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_Logging/bin/Si_Logging.dll) |
| GamePriority | 2.0.1 | [Download](https://github.com/MintLily/GamePriority/releases/download/2.0.1/GamePriority.dll) |
| Friendly Fire Limits | 1.1.4 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_FriendlyFireLimits/bin/Si_FriendlyFireLimits.dll) |

## Extension Summary
| Mod Name | Version   | Link |
|---------:|-----------|------|
| Admin Extension | 1.0.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AdminExtension/bin/Si_AdminExtension.dll) |

## Silica Listen Server Requirements
- 50Mbps upload bandwidth available [https://speedtest.org] (Individual clients can use about ~50kbps download bandwidth and up to ~1,500kbps upload bandwidth)
- 32GB+ RAM
- Decent CPU
- Top-of-the-line GPU if you want to host and play

## Server Setup Instructions
1. Install Silica
2. Install .NET 6.0 Runtime x64 [https://dotnet.microsoft.com/en-us/download/dotnet/6.0]
3. Install MelonLoader using the Manual Installation method for 64-bit games [https://melonwiki.xyz/#/README?id=manual-installation]
4. Place the Admin Extension in your `Silica\Mods` directory
5. Install any desired mods in your `Silica\Mods` directory
6. Make sure that the server name reflects that there are mods so players can choose between mods and a vanilla game experience

Note: The default location for `Silica.exe` will be in the `C:\Program Files (x86)\Steam\steamapps\common\Silica\` directory

## MelonLoader Mods for Silica Listen Server
### Admin Mod (Si_AdminMod)
Allows host to use `!addadmin` command and extends admin methods to the game's Player class for use with other mods
- Prerequisites: Ensure the Admin Extension is installed
- Install: Copy the `Si_AdminMod.dll` into your `Silica\Mods` directory
- Admins are stored in the `Silica\UserData\admins.json` file
- Testing Status: Mostly tested

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

### Anti-Grief (Si_AutoKickNegativeKills)
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
Only allows players to cause so much team imbalance; otherwise, this mod will deny the player's request to switch teams if it's too disruptive to balance. The maximum permissible imbalance (measured in player count difference between teams) is determined by a formula: `Ceiling((Current Server Player Count / Balance Divisor) + Balance Addend)`. There are separate team balance divisor and addends for two and three team versus variants in the config file.
- Install: Copy the `Si_BasicTeamBalance.dll` into your `Silica\Mods` directory
- Generates config entries in your `Silica\UserData\MelonPreferences.cfg` file for `TeamBalance_TwoTeam_Divisor` (default: 8.0), `TeamBalance_TwoTeam_Addend` (default: 1.0), `TeamBalance_ThreeTeam_Divisor` (default: 10.0), and `TeamBalance_ThreeTeam_Addend` (default: 0.0). 
- Valid divisor configuration options are any positive number (cannot be zero). Valid addend configuration options are any positive number or also zero.
- Testing Status: Mostly tested. Configuration options are currently untested.

### Basic Banlist (Si_BasicBanlist)
Retains memory of kicked players across server instances in a Json file.
- Install: Copy the `Si_BasicBanlist.dll` into your `Silica\Mods` directory
- Bans are stored in the `Silica\UserData\banned_users.json` file
- Testing Status: Mostly tested. Kick GUI functionality has not been encountered/tested at this time. Everything else tested.

### Commander Management (Si_CommManagement)
Randomly selects a team's commander from the pool of qualified applicants when the round starts. Allows `!demote <team>` admin command as well as `!cmdrban <player>` which retains memory of players not allowed to play commander across server instances in a Json file. Prevents the same commander from being randomly chosen two consecutive rounds.
- Install: Copy the `Si_CommManagement.dll` into your `Silica\Mods` directory
- Bans are stored in the `Silica\UserData\commander_bans.json` file
- Testing Status: Commanders need to push T to re-select Commamder but their spot is being reserved by the server. Additional troubleshooting needed to make it a seemless promotion to commander.

### AFK Manager (Si_AFKManager)
Allows `!afk <player>` and `!kick <player>` admin commands to soft-disconnect clients who are then free to rejoin within the server session.
- Install: Copy the `Si_AFKManager.dll` into your `Silica\Mods` directory
- Future Plans: Add timers with AFK counters to track players who are not on a team
- Testing Status: Limited testing

### Logging (Si_Logging)
Generates a log file in the Half-Life Log Standard format and replicates the log details to the server console.
- Install: Copy the `Si_Logging.dll` into your `Silica\Mods` directory
- Logs are stored in the `Silica\UserData\logs\` directory with filenames in the format `LyyyyMMdd.log`
- Testing Status: Several bugs need to be addressed that prevent some current log entries from succeeding.

### GamePriority (https://github.com/MintLily/GamePriority/releases)
Can automatically change the priority of the game executable upon launch
- Install: Copy the `GamePriority.dll` into your `Silica\Mods` directory
- Generates a config entry in your `Silica\UserData\MelonPreferences.cfg` file for `SetGamePriorityToHigh`
- Valid configuration options are `true` or `false`
- Testing Status: Confirmed working

### Friendly Fire Limits (Si_FriendlyFireLimits)
Configurable options for hosts to adjust percentages of friendly fire. The Unit to Unit multiplier controls how much damage a unit (e.g., creature, soldier, vehicle) does to a friendly unit. The Structure Non-Explosion multiplier controls how much damage is done by any damage type other than explosion to friendly structures. The Structure Explosion multiplier controls how much damage is done by just explosions to friendly structures.
- Install: Copy the `Si_FriendlyFireLimits.dll` into your `Silica\Mods` directory
- Generates config entries in your `Silica\UserData\MelonPreferences.cfg` file for `FriendlyFire_UnitAttacked_DamageMultiplier` (default: 0.05), `FriendlyFire_StructureAttacked_DamageMultiplier_Exp` (default: 0.65), `TeamBalance_ThreeTeam_Divisor` (default: 10.0), and `FriendlyFire_StructureAttacked_DamageMultiplier_NonExp` (default: 0.15)
- Valid configuration options are decimal values between 0.0 and 1.0
- Testing Status: Not fully tested

### Credits
Special thanks to Silentstorm, GrahamKracker, Auri, nighthalk and others in the MelonLoader community for being welcoming and supportive to an unusual use case of MelonLoader.
