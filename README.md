<p align="center">
    <img src="https://silicagame.com/_next/static/media/silica_logo.37ea77ee.svg" width="200" style="float:left" />&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp
    <img src="https://cdn.pixabay.com/photo/2012/04/10/23/39/sign-27080_1280.png" width="42" class="center" />  &nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp
    <img src="https://raw.githubusercontent.com/LavaGang/MelonLoader.Installer/master/Resources/ML_Text.png" width="300" style="float:right" />  
</p>

# Running a Silica Listen Server
[Silica](https://silicagame.com/news/welcome) was released in May 2023 as an Early Access game with only Listen Server capability. Dedicated Server support is planned but finding a consistent and enjoyable experience on Listen Servers can be challenging. Some functionality is still missing from the base game that makes playing the game less enjoyable and prone to balance and abuse issues, and if you would like to host a Listen Server 24/7 then some additional automation is neccessary. The intention here is to go quick and provide functionality during the interim development period, so the quality of code is not a priority at the moment. Once dedicated servers are released then the approach to server-side modding may need to be re-visited.

**Note:** *These mods are for hosts running Listen Servers only. Do not attempt to use any of these as a client. If you are a host and want to switch to a client then remove all of your MelonLoader Mods before connecting as a client!*

## Mod Summary
| Mod Name | Version   | Link |
|---------:|-----------|------|
| Admin Mod | 1.1.2 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AdminMod/bin/Si_AdminMod.dll) |
| Auto Teams Mode Select | 1.1.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AutoTeamsSelect/bin/Si_AutoTeamsSelect.dll) |
| Mapcycle | 1.0.1 | Not Ready - Game Bug(s) |
| SiRAC (Anti-Cheat) | 0.7.6 | Restricted Access - By Request Only |
| Surrender Command | 1.1.7 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_SurrenderCommand/bin/Si_SurrenderCommand.dll) |
| Anti-Grief | 1.0.6 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AutoKickNegativeKills/bin/Si_AutoKickNegativeKills.dll) |
| Headquarterless Humans Lose | 1.2.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_HQlessHumansLose/bin/Si_HQlessHumansLose.dll) |
| Basic Team Balance | 1.0.8 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_BasicTeamBalance/bin/Si_BasicTeamBalance.dll) |
| Basic Banlist | 1.0.1 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_BasicBanlist/bin/Si_BasicBanlist.dll) |
| Chat Silence | 1.0.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_ChatSilence/bin/Si_ChatSilence.dll) |
| Commander Management | 1.1.4 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_CommManagement/bin/Si_CommManagement.dll) |
| AFK Manager | 1.1.6 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AFKManager/bin/Si_AFKManager.dll) |
| Logging | 0.8.8 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_Logging/bin/Si_Logging.dll) |
| GamePriority | 2.0.1 | [Download](https://github.com/MintLily/GamePriority/releases/download/2.0.1/GamePriority.dll) |
| End Round | 1.0.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_EndRound/bin/Si_EndRound.dll) |
| Friendly Fire Limits | 1.1.5 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_FriendlyFireLimits/bin/Si_FriendlyFireLimits.dll) |
| Announcements | 1.0.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_Announcements/bin/Si_Announcements.dll) |
| Spawn Configs | 0.8.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_SpawnConfigs/bin/Si_SpawnConfigs.dll) |
| Resource Management | 1.0.0 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_Resources/bin/Si_Resources.dll) |

## Extension Summary
| Mod Name | Version   | Link |
|---------:|-----------|------|
| Admin Extension | 1.1.4 | [Download](https://raw.githubusercontent.com/data-bomb/Silica_ListenServer/main/Si_AdminExtension/bin/Si_AdminExtension.dll) |

## Silica Listen Server Requirements
- 50Mbps upload bandwidth available (Individual clients can use about ~50kbps download bandwidth and up to ~1,500kbps upload bandwidth)
- 32GB+ RAM
- Decent CPU
- Top-of-the-line GPU if you want to host and play

## Server Setup Instructions
1. Install Silica
2. Install [.NET 6.0 Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
3. Install [MelonLoader using the Manual Installation](https://melonwiki.xyz/#/README?id=manual-installation) method for 64-bit games
4. Place the Admin Extension (`Si_AdminExtension.dll`) in your `Silica\MelonLoader\net6` directory
5. Place any desired mods in your `Silica\Mods` directory
6. Launch the game and then exit the game to populate configuration parameters
7. Review and modify the Silica configuration parameters in your `Silica\UserData\MelonPreferences.cfg` file

Kind suggestion to ensure that the server name reflects that there are mods so players can choose between mods and a vanilla game experience

## [Mod Descriptions](https://github.com/data-bomb/Silica_ListenServer/wiki/Mod-Descriptions)

## How to Show Love
<a href="https://www.buymeacoffee.com/databomb" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174"></a>

## Credits
Special thanks to Silentstorm, GrahamKracker, AuriRex, nighthalk and others in the MelonLoader community for being welcoming and supportive to an unusual use case of MelonLoader.
