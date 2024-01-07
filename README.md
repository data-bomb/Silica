<p align="center">
    <img src="https://silicagame.com/_next/static/media/silica_logo.37ea77ee.svg" width="200" style="float:left" />&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp
    <img src="https://cdn.pixabay.com/photo/2012/04/10/23/39/sign-27080_1280.png" width="42" class="center" />  &nbsp&nbsp&nbsp&nbsp&nbsp&nbsp&nbsp
    <img src="https://raw.githubusercontent.com/LavaGang/MelonLoader.Installer/master/Resources/ML_Text.png" width="300" style="float:right" />  
</p>

# Running a Silica Server
[Silica](https://silicagame.com/news/welcome) was released in May 2023 as an Early Access game with only Listen Server capability. Dedicated Server was released in December 2023. Modding during the Listen Server period was done to quickly add much-needed quality-of-life improvements. Some of these features were now added into the base game when the Dedicated Server capability was released; however, others are still absent and some are expanding on the config options available to hosts.

**Note:** *These mods are only for hosts running servers. Do not attempt to use any of these as a client. If you are a host of a Listen server and want to switch to a client then remove all of your MelonLoader Mods before connecting as a client.*

## Silica Listen Server Requirements
- 50Mbps upload bandwidth available (Individual clients can use about ~50kbps download bandwidth and up to ~1,500kbps upload bandwidth)
- 32GB RAM
- Decent CPU
- Top-of-the-line GPU if you want to host and play

## Silica Dedicated Server Requirements
- 50Mbps upload bandwidth available (Individual clients can use about ~50kbps download bandwidth and up to ~1,500kbps upload bandwidth)
- 16GB RAM
- Decent CPU
- 6GB Disk Space

## Listen Server Setup Instructions
1. Install Silica from Steam
2. Install [.NET 6.0 Runtime x64](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)
3. Install [MelonLoader using the Manual Installation](https://melonwiki.xyz/#/README?id=manual-installation) method for 64-bit games
4. Place `dobby.dll` in your `Silica` directory
6. Place the Admin Mod in your `Silica\Mods` directory
7. Place any desired mods in your `Silica\Mods` directory
8. Launch Silica and then exit the game to populate configuration parameters
9. Review and modify the configuration parameters in the `Silica\UserData\MelonPreferences.cfg` file

## Dedicated Server Setup Instructions
1. Install Silica Dedicated Server Tool from Steam
2. Install the [MelonLoader.Windows.x64.CI.Release on the alpha-development branch](https://github.com/LavaGang/MelonLoader/actions/runs/7359733121) of MelonLoader using the [Manual Installation](https://melonwiki.xyz/#/README?id=manual-installation) method for 64-bit games
4. Place `dobby.dll` in your `Silica Dedicated Server` directory
5. Place the Admin Mod in your `Silica Dedicated Server\Mods` directory
6. Place any other desired mods in your `Silica Dedicated Server\Mods` directory
7. Update the command line options for Silica Dedicated Server to include `--melonloader.hideconsole --melonloader.disablestartscreen` (e.g., in Steam, right-click the Silica Dedicated Server Tool from the Steam Library and put these in the Launch Options)
8. Launch Silica Dedicated Server

For Dedicated Server hosts who wish to have an official server license, note that mods are not currently approved/supported as mods can negatively impact performance. The only exception to this is the GamePriority mod.

Kind suggestion to ensure that the server name reflects that there are mods so players can choose between mods and a vanilla game experience

## [Mod Descriptions](https://github.com/data-bomb/Silica/wiki/Mod-Descriptions)

## <a href="https://discord.gg/5SHQxFaess">Modding Discord</a>

## How to Show Love
<a href="https://www.buymeacoffee.com/databomb" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174"></a>

## Credits
Special thanks to Silentstorm, GrahamKracker, AuriRex, nighthalk and others in the MelonLoader community for being welcoming and supportive to an unusual use case of MelonLoader.
