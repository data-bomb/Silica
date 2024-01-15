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
3. Download and run the [MelonLoader.Installer.exe](https://github.com/LavaGang/MelonLoader/releases/tag/v0.6.2)
4. Using the Automated tab, hit the <b>SELECT</b> button for Unity Game and specify `C:\Program Files (x86)\Steam\steamapps\common\Silica\Silica.exe` or your custom path to the Silica.exe in your game installation directory
5. Ensure that the <b>Version</b> is set to v0.6.2 or later
6. Ensure that the <b>Game Arch</b> is set to x64
7. Select <b>Install</b>
8. Download the latest [Listen Server release](https://github.com/data-bomb/Silica/releases) and unzip all files to your `Silica` directory
9. Review the `Silica\Mods` directory and place any undesired mods in the `disabled` directory
10. Launch Silica and then exit the game to populate configuration parameters
11. Review and modify the configuration parameters in the `Silica\UserData\MelonPreferences.cfg` file
12. (Optional) Install the [QList Mod](https://github.com/dodad-2/QList) to adjust configuration parameters on-the-fly

## Dedicated Server Setup Instructions
1. Install Silica Dedicated Server Tool from Steam
2. Download and run the [MelonLoader.Installer.exe](https://github.com/LavaGang/MelonLoader/releases/tag/v0.6.2)
3. Using the Automated tab, hit the <b>SELECT</b> button for Unity Game and specify `C:\Program Files (x86)\Steam\steamapps\common\Silica Dedicated Server\Silica.exe` or your custom path to the Silica.exe in your Dedicated Server installation directory
4. Ensure that the <b>Version</b> is set to v0.6.2 or later
5. Ensure that the <b>Game Arch</b> is set to x64
6. Select <b>Install</b>
7. Download the latest [Dedicated Server release](https://github.com/data-bomb/Silica/releases) and unzip all files to your `Silica Dedicated Server` directory
8. Review the `Silica Dedicated Server\Mods` directory and place any undesired mods in the `disabled` directory
9. Update the command line options for Silica Dedicated Server to include <br>`--melonloader.hideconsole --melonloader.disablestartscreen`<br> (e.g., in Steam, right-click the Silica Dedicated Server Tool from the Steam Library and put these in the Launch Options)
10. Launch Silica Dedicated Server

## Modding and License Considerations
Note that this is not legal advice and databomb is not a lawyer. If you do have concerns then you should consult the appropriate person before running server mods.
| Server Type | Considerations |
| ----------- | ----------------------------------------------------------------- |
| Listen Server | Although no game files are modified, running mods on a listen server might be construed as modifying the game, which is not expressly permitted by the game's EULA. Depending on relevant local laws, this may be a concern. |
| Dedicated Server | There is no EULA on the server tool, so installing and running mods of the dedicated server tool is acceptable. |
| Licensed Dedicated Server | See A2.3 General Policy guidelines in the Silica Hosting Discord: <br> `A2.3 – Game Mods: Licensed servers are permitted to run modded game modes; however, it is important for license holders and their server admin teams to ensure that the server's performance meets the standards set for a server license. If the modded game mode negatively impacts the server's performance, it may be necessary to remove it. Failure to maintain the required performance standards may result in the revocation of the server license.` |

Kind suggestion to ensure that the server name reflects that there are mods so players can choose between mods and a vanilla game experience.

## [Mod Descriptions](https://github.com/data-bomb/Silica/wiki/Mod-Descriptions)

## <a href="https://discord.gg/5SHQxFaess">Modding Discord</a>

## How to Show Love
<a href="https://www.buymeacoffee.com/databomb" target="_blank"><img src="https://cdn.buymeacoffee.com/buttons/default-orange.png" alt="Buy Me A Coffee" height="41" width="174"></a>

## Credits
Special thanks to Silentstorm, GrahamKracker, AuriRex, nighthalk and others in the MelonLoader community for being welcoming and supportive to an unusual use case of MelonLoader.
