/*
Silica Commander Management Mod
Copyright (C) 2023-2024 by databomb

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

using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using SilicaAdminMod;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Si_CommanderManagement
{
    public class CommanderBans
    {
        public static readonly String banListFile = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "commander_bans.json");
        public static List<BanEntry>? BanList;

        public static void UpdateCommanderBanFile()
        {
            // convert back to json string
            String JsonRaw = JsonConvert.SerializeObject(BanList, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText(banListFile, JsonRaw);
        }

        public static void InitializeList()
        {
            if (System.IO.File.Exists(banListFile))
            {
                // Open the stream and read it back.
                System.IO.StreamReader banFileStream = System.IO.File.OpenText(banListFile);
                using (banFileStream)
                {
                    String JsonRaw = banFileStream.ReadToEnd();
                    if (JsonRaw == null)
                    {
                        MelonLogger.Warning("The commander_bans.json read as empty. No commander ban entries loaded.");
                        BanList = new List<BanEntry>();
                        return;
                    }

                    BanList = JsonConvert.DeserializeObject<List<BanEntry>>(JsonRaw);
                    if (BanList == null)
                    {
                        MelonLogger.Error("Encountered deserialization error in commander_bans.json file. Ensure file is in valid format (e.g. https://jsonlint.com/)");
                        BanList = new List<BanEntry>();
                        return;
                    }
                        
                    MelonLogger.Msg("Loaded Silica commander banlist with " + CommanderBans.BanList.Count + " entries.");
                }
            }
            else
            {
                MelonLogger.Warning("Did not find commander_bans.json file. No commander ban entries loaded.");
                BanList = new List<BanEntry>();
            }
        }

        public static bool IsBanned(Player player)
        {
            if (BanList == null)
            {
                return false;
            }

            // check if player is allowed to be commander
            long playerSteamId = long.Parse(player.ToString().Split('_')[1]);
            BanEntry? banEntry = BanList.Find(i => i.OffenderSteamId == playerSteamId);
            if (banEntry != null)
            {
                return true;
            }

            return false;
        }

        public static bool RemoveBan(Player playerToCmdrUnban)
        {
            if (CommanderBans.BanList == null)
            {
                return false;
            }

            BanEntry? matchingCmdrBan;
            matchingCmdrBan = CommanderBans.BanList.Find(i => i.OffenderSteamId == (long)playerToCmdrUnban.PlayerID.m_SteamID);

            if (matchingCmdrBan == null)
            {
                return false;
            }

            MelonLogger.Msg("Removed player name (" + matchingCmdrBan.OffenderName + ") SteamID (" + matchingCmdrBan.OffenderSteamId.ToString() + ") from the commander banlist.");
            CommanderBans.BanList.Remove(matchingCmdrBan);
            CommanderBans.UpdateCommanderBanFile();

            return true;
        }

        public static void AddBan(Player playerToCmdrBan)
        {
            if (CommanderBans.BanList == null)
            {
                return;
            }

            // gather information to log in the banlist
            Player serverPlayer = NetworkGameServer.GetServerPlayer();
            BanEntry thisBan = new BanEntry()
            {
                OffenderSteamId = long.Parse(playerToCmdrBan.ToString().Split('_')[1]),
                OffenderName = playerToCmdrBan.PlayerName,
                UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                Comments = "banned from playing commander by " + serverPlayer.PlayerName
            };

            // are we currently a commander?
            if (playerToCmdrBan.IsCommander)
            {
                Team playerTeam = playerToCmdrBan.Team;
                MP_Strategy strategyInstance = GameObject.FindObjectOfType<MP_Strategy>();

                CommanderPrimitives.DemoteTeamsCommander(strategyInstance, playerTeam);
                HelperMethods.ReplyToCommand_Player(playerToCmdrBan, "was demoted");
            }

            // are we already banned?
            if (CommanderBans.BanList.Find(i => i.OffenderSteamId == thisBan.OffenderSteamId) != null)
            {
                MelonLogger.Warning("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on commander banlist.");
            }
            else
            {
                MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the commander banlist.");
                CommanderBans.BanList.Add(thisBan);
                CommanderBans.UpdateCommanderBanFile();
            }
        }
    }
}