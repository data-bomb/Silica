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
using Il2Cpp;
using Il2CppSystem.IO;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Si_BasicBanlist;
using AdminExtension;

[assembly: MelonInfo(typeof(BasicBanlist), "[Si] Basic Banlist", "1.1.0", "databomb")]
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
            public String OffenderName
            {
                get;
                set;
            }
            public int UnixBanTime
            {
                get;
                set;
            }
            public String Comments
            {
                get;
                set;
            }
        }

        static List<BanEntry> MasterBanList;
        static String banListFile = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "banned_users.json");
        static bool AdminModAvailable = false;

        public static void UpdateBanFile()
        {
            // convert back to json string
            String JsonRaw = JsonConvert.SerializeObject(BasicBanlist.MasterBanList, Formatting.Indented);
            System.IO.File.WriteAllText(BasicBanlist.banListFile, JsonRaw);
        }

        public override void OnInitializeMelon()
        {
            try
            {
                if (System.IO.File.Exists(BasicBanlist.banListFile))
                {
                    // Open the stream and read it back.
                    using (System.IO.StreamReader banFileStream = System.IO.File.OpenText(BasicBanlist.banListFile))
                    {
                        String JsonRaw = banFileStream.ReadToEnd();
                        MasterBanList = JsonConvert.DeserializeObject<List<BanEntry>>(JsonRaw);
                        MelonLogger.Msg("Loaded Silica banlist with " + MasterBanList.Count + " entries.");
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
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public void Command_Ban(Il2Cpp.Player callerPlayer, String args)
        {
            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
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
                if (BasicBanlist.MasterBanList.Find(i => i.OffenderSteamId == thisBan.OffenderSteamId) != null)
                {
                    MelonLogger.Msg("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on banlist.");
                }
                else
                {
                    MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the banlist.");
                    BasicBanlist.MasterBanList.Add(thisBan);
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

        public static BanEntry GenerateBanEntry(Il2Cpp.Player player, Il2Cpp.Player admin)
        {
            BanEntry thisBan = new BanEntry();
            thisBan.OffenderSteamId = long.Parse(player.ToString().Split('_')[1]);
            thisBan.OffenderName = player.PlayerName;
            thisBan.UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            thisBan.Comments = "banned by " + admin.PlayerName;
            return thisBan;
        }

        [HarmonyPatch(typeof(Il2Cpp.NetworkGameServer), nameof(Il2Cpp.NetworkGameServer.KickPlayer))]
        private static class ApplyPatchKickPlayer
        {
            public static bool Prefix(Il2Cpp.Player __0, bool __1)
            {
                try
                {
                    BanEntry thisBan = GenerateBanEntry(__0, Il2Cpp.NetworkGameServer.GetServerPlayer());

                    // are we already banned?
                    if (BasicBanlist.MasterBanList.Find(i => i.OffenderSteamId == thisBan.OffenderSteamId) != null)
                    {
                        MelonLogger.Msg("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on banlist.");
                    }
                    else
                    {
                        MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the banlist.");
                        BasicBanlist.MasterBanList.Add(thisBan);
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
                    if (__0 != null)
                    {
                        // check if player was previously banned
                        long JoiningPlayerSteamId = long.Parse(__0.ToString().Split('_')[1]);
                        if (BasicBanlist.MasterBanList.Find(i => i.OffenderSteamId == JoiningPlayerSteamId) != null)
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