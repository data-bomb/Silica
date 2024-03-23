/*
 Silica Basic Banlist Mod
 Copyright (C) 2023-2024 by databomb
 
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

#if NET6_0
using Il2Cpp;
#endif

using HarmonyLib;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using Si_BasicBanlist;
using SilicaAdminMod;
using System;
using System.Collections.Generic;
using System.Linq;

[assembly: MelonInfo(typeof(BasicBanlist), "Basic Banlist", "1.4.0", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

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

        static MelonPreferences_Category _modCategory = null!;
        static MelonPreferences_Entry<bool> _Pref_Ban_KickButton_PermaBan = null!;

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
                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                _Pref_Ban_KickButton_PermaBan ??= _modCategory.CreateEntry<bool>("Ban_HostKickButton_Permabans", false);

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
            HelperMethods.CommandCallback banCallback = Command_Ban;
            HelperMethods.RegisterAdminCommand("ban", banCallback, Power.Ban, "Bans target player. Usage: !ban <player>");
            HelperMethods.RegisterAdminCommand("kickban", banCallback, Power.Ban, "Bans target player. Usage: !kickban <player>");

            HelperMethods.CommandCallback offlineBanCallback = Command_OfflineBan;
            HelperMethods.RegisterAdminCommand("banid", offlineBanCallback, Power.Rcon, "Bans target player by SteamID. Usage !ban <Steam64ID> <player name>");

            HelperMethods.CommandCallback unbanCallback = Command_Unban;
            HelperMethods.RegisterAdminCommand("unban", unbanCallback, Power.Unban, "Unbans target player. Usage: !unban <playername | Steam64ID>");

            #if NET6_0
            bool QListLoaded = RegisteredMelons.Any(m => m.Info.Name == "QList");
            if (!QListLoaded)
            {
                return;
            }

            QList.Options.RegisterMod(this);

            QList.OptionTypes.BoolOption kickEqualsPermaBan = new(_Pref_Ban_KickButton_PermaBan, _Pref_Ban_KickButton_PermaBan.Value);

            QList.Options.AddOption(kickEqualsPermaBan);
            #endif
        }

        public static void Command_OfflineBan(Player? callerPlayer, String args)
        {
            // validate banlist is available
            if (MasterBanList == null)
            {
                MelonLogger.Msg("Ban list unavailable. Check json syntax.");
                return;
            }

            string commandName = args.Split(' ')[0];

            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount < 2)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            string targetID = args.Split(' ')[1];
            string targetName = args.Split(' ', 2)[2];
            bool isNumber = long.TryParse(targetID, out long steamid);
            if (!isNumber)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Not a valid SteamID64");
                return;
            }

            BanEntry? matchingBan = MasterBanList.Find(i => i.OffenderSteamId == steamid);

            // did we find someone we could ban?
            if (matchingBan != null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": SteamID64 already found in the banlist");
                return;
            }

            BanEntry thisBan = new BanEntry()
            {
                OffenderSteamId = steamid,
                OffenderName = targetName,
                UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
            };

            if (callerPlayer == null)
            {
                thisBan.Comments = "banned by SERVER CONSOLE";
            }
            else
            {
                thisBan.Comments = "banned by " + callerPlayer.PlayerName;
            }

            MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the banlist.");
            MasterBanList.Add(thisBan);
            UpdateBanFile();

            HelperMethods.AlertAdminAction(callerPlayer, "banned " + thisBan.OffenderName);
        }

        public static void Command_Ban(Player? callerPlayer, String args)
        {
            // validate banlist is available
            if (MasterBanList == null)
            {
                MelonLogger.Msg("Ban list unavailable. Check json syntax.");
                return;
            }
            
            string commandName = args.Split(' ')[0];
            
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Player? playerToBan = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToBan == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer != null && !callerPlayer.CanAdminTarget(playerToBan))
            {
                HelperMethods.ReplyToCommand_Player(playerToBan, "is immune due to level");
                return;
            }

            BanPlayer(playerToBan, callerPlayer);
        }

        public static void Command_Unban(Player? callerPlayer, String args)
        {
            // validate banlist is available
            if (MasterBanList == null)
            {
                MelonLogger.Msg("Ban list unavailable. Check json syntax.");
                return;
            }
            
            string commandName = args.Split(' ')[0];
            
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
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

        public static void BanPlayer(Player playerToBan, Player? adminPlayer)
        {
            if (MasterBanList == null)
            {
                return;
            }

            BanEntry thisBan = GenerateBanEntry(playerToBan, adminPlayer);

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

            NetworkGameServer.KickPlayer(playerToBan);
            HelperMethods.AlertAdminActivity(adminPlayer, playerToBan, "banned");
        }

        public static BanEntry GenerateBanEntry(Player player, Player? admin)
        {
            BanEntry thisBan = new BanEntry()
            {
                OffenderSteamId = long.Parse(player.ToString().Split('_')[1]),
                OffenderName = player.PlayerName,
                UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds
            };
			
			if (admin == null)
			{
				thisBan.Comments = "banned by SERVER CONSOLE";
			}
			else
			{
				thisBan.Comments = "banned by " + admin.PlayerName;
			}

            return thisBan;
        }

        [HarmonyPatch(typeof(NetworkGameServer), nameof(NetworkGameServer.KickPlayer))]
        private static class ApplyPatchKickPlayer
        {
            public static void Prefix(Player __0)
            {
                try
                {
                    if (MasterBanList == null)
                    {
                        return;
                    }

                    // if we just intended to kick, we can skip the rest
                    if (!_Pref_Ban_KickButton_PermaBan.Value)
                    {
                        MelonLogger.Msg("Kicked player name (" + __0.PlayerName + ") SteamID (" + __0.ToString() + ")");
                        return;
                    }

                    BanEntry thisBan = GenerateBanEntry(__0, NetworkGameServer.GetServerPlayer());

                    // are we already banned?
                    if (MasterBanList.Find(i => i.OffenderSteamId == thisBan.OffenderSteamId) != null)
                    {
                        MelonLogger.Msg("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on banlist.");
                        return;
                    }
                    
                    MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the banlist.");
                    MasterBanList.Add(thisBan);
                    UpdateBanFile();
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run NetworkGameServer::KickPlayer");
                }

                return;
            }
        }

        [HarmonyPatch(typeof(GameMode), nameof(GameMode.OnPlayerJoinedBase))]
        private static class ApplyPatchOnPlayerJoinedBase
        {
            public static void Postfix(GameMode __instance, Player __0)
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
                            NetworkGameServer.KickPlayer(__0);
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