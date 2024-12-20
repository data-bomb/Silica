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

[assembly: MelonInfo(typeof(BasicBanlist), "Basic Banlist", "1.5.3", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_BasicBanlist
{
    public class BasicBanlist : MelonMod
    {
        public class BanEntry
        {
            public ulong OffenderSteamId
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
                    MelonLogger.Msg("Did not find banned_users.json file. Will use ServerSettings.xml file.");
                    MasterBanList = null;
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
            HelperMethods.RegisterAdminCommand("banid", offlineBanCallback, Power.Rcon, "Bans target player by SteamID. Usage !banid <Steam64ID> <player name>");

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

        public static bool IsPlayerBanned(string name)
        {
            // using banned_users.json
            if (MasterBanList != null)
            {
                BanEntry? matchingBan = MasterBanList.Find(i => i.OffenderName == name);
                if (matchingBan != null)
                {
                    return true;
                }
            }
            // using game XML
            else
            {
                return NetworkServerSettings.PlayerIsBanned(name);
            }

            return false;
        }

        public static bool IsPlayerBanned(ulong steamID)
        {
            // using banned_users.json
            if (MasterBanList != null)
            {
                BanEntry? matchingBan = MasterBanList.Find(i => i.OffenderSteamId == steamID);
                if (matchingBan != null)
                {
                    return true;
                }
            }
            // using game XML
            else
            {
                return NetworkServerSettings.PlayerIsBanned(steamID);
            }

            return false;
        }

        public static void Command_OfflineBan(Player? callerPlayer, String args)
        {
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
            int spaceCharacter = args.IndexOf(' ', commandName.Length+1);
            string targetName = args.Substring(spaceCharacter+1);
            bool isNumber = ulong.TryParse(targetID, out ulong steamid);
            if (!isNumber)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Not a valid SteamID64");
                return;
            }

            // did we find someone we could ban?
            if (IsPlayerBanned(steamid))
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": SteamID64 already found in the banlist");
                return;
            }

            if (MasterBanList == null)
            {
                NetworkServerSettings.PlayerAddBan(steamid, targetName, 0, callerPlayer == null ? "offline banned by SERVER CONSOLE" : "offline banned by " + callerPlayer.PlayerName);
            }
            else
            {
                BanEntry thisBan = new BanEntry()
                {
                    OffenderSteamId = steamid,
                    OffenderName = targetName,
                    UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                    Comments = (callerPlayer == null) ? "offline banned by SERVER CONSOLE" : "offline banned by " + callerPlayer.PlayerName
                };

                MasterBanList.Add(thisBan);
                UpdateBanFile();
            }

            MelonLogger.Msg("Added player name (" + targetName + ") SteamID (" + steamid + ") to the banlist (offline).");
            HelperMethods.AlertAdminAction(callerPlayer, "offline banned " + targetName);
        }

        public static void Command_Ban(Player? callerPlayer, String args)
        {
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
            string commandName = args.Split(' ')[0];
            
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // grab everything after !unban for the target string
            String unbanTarget = args.Split(' ', 2)[1];

            if (!UnbanPlayer(unbanTarget, callerPlayer))
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Unable to find " + unbanTarget + " on banlist");
                return;
            }
        }

        public static bool UnbanPlayer(String target, Player? adminPlayer)
        {
            // accept target of either steamid64 or name
            bool isNumber = ulong.TryParse(target, out ulong steamid);

            if (MasterBanList == null)
            {
                NetworkBannedPlayer? bannedPlayer = isNumber ? NetworkServerSettings.GetPlayerBan(steamid) : NetworkServerSettings.GetPlayerBan(target);
                if (bannedPlayer == null)
                {
                    return false;
                }

                NetworkServerSettings.PlayerRemoveBan(bannedPlayer.m_ID);
                MelonLogger.Msg("Removed player name (" + bannedPlayer.m_Name + ") SteamID (" + bannedPlayer.m_ID.ToString() + ") from the banlist.");
                HelperMethods.AlertAdminAction(adminPlayer, "unbanned " + bannedPlayer.m_Name);
                return true;
            }
            else
            {
                BanEntry? bannedPlayer = isNumber ? MasterBanList.Find(i => i.OffenderSteamId == steamid) : MasterBanList.Find(i => i.OffenderName == target);
                if (bannedPlayer == null)
                {
                    return false;
                }

                MasterBanList.Remove(bannedPlayer);
                UpdateBanFile();
                MelonLogger.Msg("Removed player name (" + bannedPlayer.OffenderName + ") SteamID (" + bannedPlayer.OffenderSteamId.ToString() + ") from the banlist.");
                HelperMethods.AlertAdminAction(adminPlayer, "unbanned " + bannedPlayer.OffenderName);
                return true;
            }
        }

        public static void BanPlayer(Player playerToBan, Player? adminPlayer)
        {
            // are we already banned?
            if (IsPlayerBanned(playerToBan.PlayerID.SteamID.m_SteamID))
            {
                MelonLogger.Warning("Player name (" + playerToBan.PlayerName + ") SteamID (" + playerToBan.PlayerID.SteamID.m_SteamID.ToString() + ") already on banlist.");
                return;
            }

            if (MasterBanList == null)
            {
                NetworkServerSettings.PlayerAddBan(playerToBan.PlayerID.SteamID.m_SteamID, playerToBan.PlayerName, 0, adminPlayer == null ? "banned by SERVER CONSOLE" : "banned by " + adminPlayer.PlayerName);
            }
            else
            {
                BanEntry thisBan = GenerateBanEntry(playerToBan, adminPlayer);
                MasterBanList.Add(thisBan);
                UpdateBanFile();
            }

            MelonLogger.Msg("Added player name (" + playerToBan.PlayerName + ") SteamID (" + playerToBan.PlayerID.SteamID.m_SteamID.ToString() + ") to the banlist.");
            NetworkGameServer.KickPlayer(playerToBan);
            HelperMethods.AlertAdminActivity(adminPlayer, playerToBan, "banned");
        }

        public static BanEntry GenerateBanEntry(Player player, Player? admin)
        {
            BanEntry thisBan = new BanEntry()
            {
                OffenderSteamId = ulong.Parse(player.ToString().Split('_')[1]),
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
                        ulong JoiningPlayerSteamId = ulong.Parse(__0.ToString().Split('_')[1]);
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