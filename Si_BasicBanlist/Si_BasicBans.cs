/*
 Silica Basic Banlist Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, retains history of kicked players across
 server reboots.

 * License *
 This program is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.
 
 This program is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 GNU General Public License for more details.
 
 You should have received a copy of the GNU General Public License
 along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Si_BasicBanlist;
using AdminExtension;

[assembly: MelonInfo(typeof(BasicBanlist), "[Si] Basic Banlist", "1.2.1", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_BasicBanlist
{
    public class BasicBanlist : MelonMod
    {
        public class BanEntry
        {
            public long OffenderSteamId
            {
                get;
                set;
            }
            public String? OffenderName
            {
                get;
                set;
            }
            public int UnixBanTime
            {
                get;
                set;
            }
            public String? Comments
            {
                get;
                set;
            }
        }

        static List<BanEntry>? MasterBanList;
        static readonly String banListFile = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "banned_users.json");
        static bool AdminModAvailable = false;

        public static void UpdateBanFile()
        {
            // convert back to json string
            String JsonRaw = JsonConvert.SerializeObject(MasterBanList, Formatting.Indented);
            System.IO.File.WriteAllText(banListFile, JsonRaw);
        }

        public override void OnInitializeMelon()
        {
            try
            {
                if (System.IO.File.Exists(banListFile))
                {
                    // Open the stream and read it back.
                    System.IO.StreamReader banFileStream = System.IO.File.OpenText(banListFile);
                    using (banFileStream)
                    {
                        String JsonRaw = banFileStream.ReadToEnd();
                        MasterBanList = JsonConvert.DeserializeObject<List<BanEntry>>(JsonRaw);
                        if (MasterBanList == null)
                        {
                            MelonLogger.Msg("Unable to process banned_users.json file. Check syntax.");
                        }
                        else
                        {
                            MelonLogger.Msg("Loaded Silica banlist with " + MasterBanList.Count + " entries.");
                        }
                    }
                }
                else
                {
                    MelonLogger.Msg("Did not find banned_users.json file. No banlist entries loaded.");
                    MasterBanList = new List<BanEntry>();
                }
            }
            catch (Exception error)
            {
                HelperMethods.PrintError(error, "Failed to load Silica banlist (OnInitializeMelon)");
            }
        }

        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (AdminModAvailable)
            {
                HelperMethods.CommandCallback banCallback = Command_Ban;
                HelperMethods.RegisterAdminCommand("!ban", banCallback, Power.Ban);
                HelperMethods.RegisterAdminCommand("!kickban", banCallback, Power.Ban);

                HelperMethods.CommandCallback unbanCallback = Command_Unban;
                HelperMethods.RegisterAdminCommand("!unban", unbanCallback, Power.Unban);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public static void Command_Ban(Il2Cpp.Player callerPlayer, String args)
        {
            // validate banlist is available
            if (MasterBanList == null)
            {
                MelonLogger.Msg("Ban list unavailable. Check json syntax.");
                return;
            }

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Il2Cpp.Player? playerToBan = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToBan == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer.CanAdminTarget(playerToBan))
            {
                BanEntry thisBan = GenerateBanEntry(playerToBan, callerPlayer);

                // are we already banned?
                if (MasterBanList.Find(i => i.OffenderSteamId == thisBan.OffenderSteamId) != null)
                {
                    MelonLogger.Msg("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on banlist.");
                }
                else
                {
                    MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the banlist.");
                    MasterBanList.Add(thisBan);
                    UpdateBanFile();
                }

                Il2Cpp.NetworkGameServer.KickPlayer(playerToBan);
                HelperMethods.AlertAdminActivity(callerPlayer, playerToBan, "banned");
            }
            else
            {
                HelperMethods.ReplyToCommand_Player(playerToBan, "is immune due to level");
            }
        }

        public static void Command_Unban(Il2Cpp.Player callerPlayer, String args)
        {
            // validate banlist is available
            if (MasterBanList == null)
            {
                MelonLogger.Msg("Ban list unavailable. Check json syntax.");
                return;
            }

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too few arguments");
                return;
            }

            // grab everything after !unban for the target string
            String sTarget = args.Split(' ', 2)[1];

            // accept target of either steamid64 or name
            bool isNumber = long.TryParse(sTarget, out long steamid);

            BanEntry? matchingBan;
            // assume it's a steamid64
            if (isNumber)
            {
                matchingBan = MasterBanList.Find(i => i.OffenderSteamId == steamid);
            }
            // assume it's a name
            else
            {
                matchingBan = MasterBanList.Find(i => i.OffenderName == sTarget);
            }

            // did we find someone we could unban?
            if (matchingBan == null)
            {
                String targetType = isNumber ? "steamid" : "name";
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Unable to find " + targetType + " on banlist");
                return;
            }

            MelonLogger.Msg("Removed player name (" + matchingBan.OffenderName + ") SteamID (" + matchingBan.OffenderSteamId.ToString() + ") from the banlist.");
            MasterBanList.Remove(matchingBan);
            UpdateBanFile();

            HelperMethods.AlertAdminAction(callerPlayer, "unbanned " + matchingBan.OffenderName);
        }

            public static BanEntry GenerateBanEntry(Il2Cpp.Player player, Il2Cpp.Player admin)
        {
            BanEntry thisBan = new()
            {
                OffenderSteamId = long.Parse(player.ToString().Split('_')[1]),
                OffenderName = player.PlayerName,
                UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                Comments = "banned by " + admin.PlayerName
            };
            return thisBan;
        }

        [HarmonyPatch(typeof(Il2Cpp.NetworkGameServer), nameof(Il2Cpp.NetworkGameServer.KickPlayer))]
        private static class ApplyPatchKickPlayer
        {
            public static bool Prefix(Il2Cpp.Player __0, bool __1)
            {
                try
                {
                    if (MasterBanList == null)
                    {
                        return true;
                    }

                    BanEntry thisBan = GenerateBanEntry(__0, Il2Cpp.NetworkGameServer.GetServerPlayer());

                    // are we already banned?
                    if (MasterBanList.Find(i => i.OffenderSteamId == thisBan.OffenderSteamId) != null)
                    {
                        MelonLogger.Msg("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on banlist.");
                    }
                    else
                    {
                        MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the banlist.");
                        MasterBanList.Add(thisBan);
                        UpdateBanFile();
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run NetworkGameServer::KickPlayer");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.GameMode), nameof(Il2Cpp.GameMode.OnPlayerJoinedBase))]
        private static class ApplyPatchOnPlayerJoinedBase
        {
            public static void Postfix(Il2Cpp.GameMode __instance, Il2Cpp.Player __0)
            {
                try
                {
                    if (MasterBanList == null)
                    {
                        return;
                    }

                    if (__0 != null)
                    {
                        // check if player was previously banned
                        long JoiningPlayerSteamId = long.Parse(__0.ToString().Split('_')[1]);
                        if (MasterBanList.Find(i => i.OffenderSteamId == JoiningPlayerSteamId) != null)
                        {
                            MelonLogger.Msg("Kicking " + __0.ToString() + " for matching an entry in the banlist.");
                            Il2Cpp.NetworkGameServer.KickPlayer(__0);
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run GameMode::OnPlayerJoinedBase");
                }
            }
        }
    }
}