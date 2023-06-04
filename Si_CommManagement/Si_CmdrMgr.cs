/*
 Silica Commander Management Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, allows commanders to be demoted and blocked
 from being a commander.

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
using Si_CommanderManagement;
using System.Xml;
using UnityEngine;
using static MelonLoader.MelonLogger;
using static Si_CommanderManagement.CommanderManager;

[assembly: MelonInfo(typeof(CommanderManager), "[Si] Commander Management", "0.9.4", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_CommanderManagement
{
    public class CommanderManager : MelonMod
    {
        const string ChatPrefix = "[BOT] ";
        const int MaxTeams = 3;
        const int AlienTeam = 0;
        const int CentauriTeam = 1;
        const int SolTeam = 2;

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

        public static void PrintError(Exception exception, string message = null)
        {
            if (message != null)
            {
                MelonLogger.Msg(message);
            }
            string error = exception.Message;
            error += "\n" + exception.TargetSite;
            error += "\n" + exception.StackTrace;
            MelonLogger.Error(error);
        }

        static List<Player>[] commanderApplicants;

        static List<BanEntry> MasterBanList;
        static String banListFile = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, "commander_bans.json");

        public override void OnInitializeMelon()
        {
            try
            {
                if (System.IO.File.Exists(CommanderManager.banListFile))
                {
                    // Open the stream and read it back.
                    using (System.IO.StreamReader banFileStream = System.IO.File.OpenText(CommanderManager.banListFile))
                    {
                        String JsonRaw = banFileStream.ReadToEnd();
                        MasterBanList = JsonConvert.DeserializeObject<List<BanEntry>>(JsonRaw);
                        MelonLogger.Msg("Loaded Silica commander banlist with " + MasterBanList.Count + " entries.");
                    }
                }
                else
                {
                    MelonLogger.Msg("Did not find commander_bans.json file. No commander ban entries loaded.");
                    MasterBanList = new List<BanEntry>();
                }

                CommanderManager.commanderApplicants = new List<Player>[MaxTeams];
                CommanderManager.commanderApplicants[AlienTeam] = new List<Player>();
                CommanderManager.commanderApplicants[CentauriTeam] = new List<Player>();
                CommanderManager.commanderApplicants[SolTeam] = new List<Player>();
            }
            catch (Exception error)
            {
                CommanderManager.PrintError(error, "Failed to load Silica commander balist");
            }
        }

        public static void SendToInfantry(Il2Cpp.Player FormerCommander)
        {
            Il2Cpp.GameByteStreamWriter theRoleStream;
            theRoleStream = Il2Cpp.GameMode.CurrentGameMode.CreateRPCPacket(2);
            if (theRoleStream == null)
            {
                return;
            }

            theRoleStream.WriteUInt64(FormerCommander.PlayerID.m_SteamID);
            theRoleStream.WriteByte((byte)FormerCommander.PlayerChannel);
            theRoleStream.WriteByte((byte)Il2Cpp.MP_Strategy.ETeamRole.INFANTRY);
            Il2Cpp.GameMode.CurrentGameMode.SendRPCPacket(theRoleStream);
        }

        public static Il2Cpp.Player? FindTargetPlayer(String sTarget)
        {
            Il2Cpp.Player targetPlayer = null;
            int iTargetCount = 0;

            // loop through all players
            Il2CppSystem.Collections.Generic.List<Il2Cpp.Player> players = Il2Cpp.Player.Players;
            int iPlayerCount = players.Count;

            for (int i = 0; i < iPlayerCount; i++)
            {
                if (players[i].PlayerName.Contains(sTarget))
                {
                    iTargetCount++;
                    targetPlayer = players[i];
                }
            }

            if (iTargetCount != 1)
            {
                targetPlayer = null;
            }

            return targetPlayer;
        }

        // may need to re-think this approach on preventing commander promotion
        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.GetStrategyCommanderTeamSetup))]
        private static class ApplyPatchCommanderTeamSetup
        {
            public static bool Prefix(Il2Cpp.MP_Strategy __instance, Il2Cpp.StrategyTeamSetup __result, Il2Cpp.Player __0)
            {
                try
                {
                    if (__0 != null)
                    {
                        // check if player is allowed to be commander
                        long JoiningPlayerSteamId = long.Parse(__0.ToString().Split('_')[1]);
                        CommanderManager.BanEntry banEntry = CommanderManager.MasterBanList.Find(i => i.OffenderSteamId == JoiningPlayerSteamId);
                        if (banEntry != null)
                        {
                            MelonLogger.Msg("Preventing " + banEntry.OffenderName + " from selecting commander.");

                            __0 = null;
                            __result = null;
                            return false;
                        }
                    }
                }
                catch (Exception error)
                {
                    CommanderManager.PrintError(error, "Failed to run GetStrategyCommanderTeamSetup");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameInit))]
        private static class ApplyPatchOnGameInit
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0)
            {
                try
                {
                    MelonLogger.Msg("OnGameInit Fired.");

                    if (CommanderManager.commanderApplicants[AlienTeam].Count > 0)
                    {
                        MelonLogger.Msg("Clearing Alien applicants.");
                        CommanderManager.commanderApplicants[AlienTeam].Clear();
                    }
                    if (CommanderManager.commanderApplicants[CentauriTeam].Count > 0)
                    {
                        MelonLogger.Msg("Clearing Centauri applicants.");
                        CommanderManager.commanderApplicants[CentauriTeam].Clear();
                    }
                    if (CommanderManager.commanderApplicants[SolTeam].Count > 0)
                    {
                        MelonLogger.Msg("Clearing Sol applicants.");
                        CommanderManager.commanderApplicants[SolTeam].Clear();
                    }
                }
                catch (Exception error)
                {
                    CommanderManager.PrintError(error, "Failed to run OnGameInit");
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatchOnGameStarted
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0)
            {
                try
                {
                    MelonLogger.Msg("OnGameStarted Fired. Finding commanders.");

                    Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                    // *** TODO: need to account for if a player leaves the game within the 30 second window
                    System.Random randomIndex = new System.Random();
                    if (CommanderManager.commanderApplicants[AlienTeam].Count > 0)
                    {
                        int iAlienCommanderIndex = randomIndex.Next(0, CommanderManager.commanderApplicants[AlienTeam].Count - 1);
                        Il2Cpp.Player AlienCommander = CommanderManager.commanderApplicants[AlienTeam][iAlienCommanderIndex];

                        if (AlienCommander != null && AlienCommander.Team.Index == AlienTeam)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + "Promoted " + AlienCommander.PlayerName + " to Alien commander", false);
                            PromoteToCommander(AlienCommander);
                        }
                    }
                    if (CommanderManager.commanderApplicants[CentauriTeam].Count > 0)
                    {
                        int iCentauriCommanderIndex = randomIndex.Next(0, CommanderManager.commanderApplicants[CentauriTeam].Count - 1);
                        Il2Cpp.Player CentauriCommander = CommanderManager.commanderApplicants[CentauriTeam][iCentauriCommanderIndex];

                        if (CentauriCommander != null && CentauriCommander.Team.Index == CentauriTeam)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + "Promoted " + CentauriCommander.PlayerName + " to Centauri commander", false);
                            PromoteToCommander(CentauriCommander);
                        }
                    }
                    if (CommanderManager.commanderApplicants[SolTeam].Count > 0)
                    {
                        int iSolCommanderIndex = randomIndex.Next(0, CommanderManager.commanderApplicants[SolTeam].Count - 1);
                        Il2Cpp.Player SolCommander = CommanderManager.commanderApplicants[SolTeam][iSolCommanderIndex];

                        if (SolCommander != null && SolCommander.Team.Index == SolTeam)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + "Promoted " + SolCommander.PlayerName + " to Sol commander", false);
                            PromoteToCommander(SolCommander);
                        }
                    }
                }
                catch (Exception error)
                {
                    CommanderManager.PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.SetCommander))]
        private static class ApplyPatchSetCommander
        {
            public static bool Prefix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Team __0, Il2Cpp.Player __1)
            {
                try
                {
                    if (__instance != null && __0 != null && __1 != null)
                    {
                        // check if player is allowed to be commander
                        long JoiningPlayerSteamId = long.Parse(__1.ToString().Split('_')[1]);
                        CommanderManager.BanEntry banEntry = CommanderManager.MasterBanList.Find(i => i.OffenderSteamId == JoiningPlayerSteamId);
                        if (banEntry != null)
                        {
                            MelonLogger.Msg("Preventing " + banEntry.OffenderName + " from playing as commander.");

                            // need to get the player back to Infantry and not stuck in no-clip
                            SendToInfantry(__1);
                            // respawn
                            GameMode.CurrentGameMode.SpawnUnitForPlayer(__1, __0);

                            __1 = null;
                            return false;
                        }

                        // check if they're trying to join before the 30 second countdown expires and the game begins
                        if (Il2Cpp.GameMode.CurrentGameMode.Started && !Il2Cpp.GameMode.CurrentGameMode.GameBegun)
                        {
                            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __1.PlayerName + " has applied for commander.", false);

                            // need to get the player back to Infantry and not stuck in no-clip
                            SendToInfantry(__1);
                            // respawn
                            GameMode.CurrentGameMode.SpawnUnitForPlayer(__1, __0);

                            CommanderManager.commanderApplicants[__1.Team.Index].Add(__1);

                            __1 = null;
                            return false;
                        }
                    }
                }
                catch (Exception error)
                {
                    CommanderManager.PrintError(error, "Failed to run SetCommander");
                }

                return true;
            }
        }

        public static void AddCommanderBan(Il2Cpp.Player PlayerToCmdrBan)
        {
            // gather information to log in the banlist
            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
            CommanderManager.BanEntry thisBan = new BanEntry();
            thisBan.OffenderSteamId = long.Parse(PlayerToCmdrBan.ToString().Split('_')[1]);
            thisBan.OffenderName = PlayerToCmdrBan.PlayerName;
            thisBan.UnixBanTime = (int)DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            thisBan.Comments = "banned from playing commander by " + serverPlayer.PlayerName;

            // are we currently a commander?
            if (PlayerToCmdrBan.IsCommander)
            {
                Il2Cpp.Team playerTeam = PlayerToCmdrBan.Team;
                Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();

                DemoteTeamsCommander(strategyInstance, playerTeam);
                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + thisBan.OffenderName + " was demoted", false);
            }

            // are we already banned?
            if (CommanderManager.MasterBanList.Find(i => i.OffenderSteamId == thisBan.OffenderSteamId) != null)
            {
                MelonLogger.Msg("Player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") already on commander banlist.");
            }
            else
            {
                MelonLogger.Msg("Added player name (" + thisBan.OffenderName + ") SteamID (" + thisBan.OffenderSteamId.ToString() + ") to the commander banlist.");
                CommanderManager.MasterBanList.Add(thisBan);

                // convert back to json string
                String JsonRaw = JsonConvert.SerializeObject(CommanderManager.MasterBanList, Newtonsoft.Json.Formatting.Indented);

                System.IO.File.WriteAllText(CommanderManager.banListFile, JsonRaw);

                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + thisBan.OffenderName + " was temporarily banned from being a commander", false);
            }
        }

        public static void PromoteToCommander(Il2Cpp.Player CommanderPlayer)
        {
            Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
            strategyInstance.SetCommander(CommanderPlayer.Team, CommanderPlayer);
            strategyInstance.RPC_SynchCommander(CommanderPlayer.Team);
        }

        public static void DemoteTeamsCommander(Il2Cpp.MP_Strategy strategyInstance, Il2Cpp.Team TargetTeam)
        {
            Il2Cpp.Player DemotedCommander = strategyInstance.GetCommanderForTeam(TargetTeam);
            strategyInstance.SetCommander(TargetTeam, null);
            strategyInstance.RPC_SynchCommander(TargetTeam);
            // need to get the player back to Infantry and not stuck in no-clip
            SendToInfantry(DemotedCommander);
            // respawn
            GameMode.CurrentGameMode.SpawnUnitForPlayer(DemotedCommander, TargetTeam);
        }

        [HarmonyPatch(typeof(Il2Cpp.Player), nameof(Il2Cpp.Player.SendChatMessage))]
        private static class ApplySendChatDemoteCommandPatch
        {
            public static bool Prefix(Il2Cpp.Player __instance, bool __result, string __0, bool __1)
            {
                try
                {
                    bool bIsDemoteCommand = String.Equals(__0.Split(' ')[0], "!demote", StringComparison.OrdinalIgnoreCase);
                    if (bIsDemoteCommand)
                    {
                        __result = false;

                        // check for authorized. for now, only server operator is considered authorized
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

                        if (__instance != serverPlayer)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + " is not authorized to use !demote", false);
                            return false;
                        }

                        // count number of arguments
                        int iArguments = __0.Split(' ').Count();
                        if (iArguments > 2)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + ": Too many arguments specified", false);
                            return false;
                        }

                        int iTargetTeamIndex = -1;
                        Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();

                        // if no team was specified then use current team
                        if (iArguments == 1)
                        {
                            iTargetTeamIndex = serverPlayer.Team.Index;
                        }
                        else
                        {
                            // second argument targets team where current commander needs to get demoted
                            String sTarget = __0.Split(' ')[1];

                            if (String.Equals(sTarget, "Human", StringComparison.OrdinalIgnoreCase))
                            {
                                // check gamemode - if Humans vs Aliens or the other ones
                                if (strategyInstance.TeamsVersus == Il2Cpp.MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS)
                                {
                                    // if it's human vs aliens then human translates to the Human (Sol) team index
                                    iTargetTeamIndex = 2;
                                }
                                // otherwise, it's ambigious and we can't make a decision
                            }
                            else if (String.Equals(sTarget, "Alien", StringComparison.OrdinalIgnoreCase))
                            {
                                iTargetTeamIndex = 0;
                            }
                            else if (sTarget.Contains("Cent", StringComparison.OrdinalIgnoreCase))
                            {
                                iTargetTeamIndex = 1;
                            }
                            else if (String.Equals(sTarget, "Sol", StringComparison.OrdinalIgnoreCase))
                            {
                                iTargetTeamIndex = 2;
                            }

                            // check if we still don't have a valid target
                            if (iTargetTeamIndex < 0)
                            {
                                // notify player on invalid usage
                                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + ": Valid targets are Alien, Centauri, or Sol", false);
                                return false;
                            }
                        }

                        Il2Cpp.Team TargetTeam = Il2Cpp.Team.GetTeamByIndex(iTargetTeamIndex);
                        if (TargetTeam != null)
                        {
                            // check if they have a commander to demote
                            bool bHasCommander = (strategyInstance.GetCommanderForTeam(TargetTeam) != null);

                            if (bHasCommander)
                            {
                                DemoteTeamsCommander(strategyInstance, TargetTeam);
                                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + TargetTeam.TeamName + "'s commander was demoted", false);
                                return false;
                            }
                            else
                            {
                                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + ": No commander found on specified team", false);
                            }
                        }
                        else
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + ": No valid team found", false);
                        }

                        return false;
                    }

                    bool bIsCommanderBanCommand = (String.Equals(__0.Split(' ')[0], "!commanderban", StringComparison.OrdinalIgnoreCase) ||
                                String.Equals(__0.Split(' ')[0], "!cmdrban", StringComparison.OrdinalIgnoreCase));
                    if (bIsCommanderBanCommand)
                    {
                        // check for authorized. for now, only server operator is considered authorized
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

                        if (__instance != serverPlayer)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + " is not authorized to use !cmdrban", false);
                            return false;
                        }

                        // count number of arguments
                        int iArguments = __0.Split(' ').Count();
                        if (iArguments > 2)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + ": Too many arguments specified", false);
                            return false;
                        }

                        String sTarget = __0.Split(' ')[1];
                        Il2Cpp.Player PlayerToCmdrBan = FindTargetPlayer(sTarget);

                        if (PlayerToCmdrBan == null)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __instance.PlayerName + ": Ambiguous or invalid target", false);
                            return false;
                        }
                        
                        AddCommanderBan(PlayerToCmdrBan);
                        return false;
                    }
                }
                catch (Exception error)
                {
                    CommanderManager.PrintError(error, "Failed to run SendChatMessage");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class ApplyReceiveChatDemoteCommandPatch
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    bool bIsDemoteCommand = String.Equals(__1.Split(' ')[0], "!demote", StringComparison.OrdinalIgnoreCase);
                    if (bIsDemoteCommand)
                    {
                        // check for authorized. for now, only server operator is considered authorized
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

                        if (__0 != serverPlayer)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + " is not authorized to use !demote", false);
                            return;
                        }

                        // count number of arguments
                        int iArguments = __1.Split(' ').Count();
                        if (iArguments > 2)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + ": Too many arguments specified", false);
                            return;
                        }

                        int iTargetTeamIndex = -1;
                        Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();

                        // if no team was specified then use current team
                        if (iArguments == 1)
                        {
                            iTargetTeamIndex = serverPlayer.Team.Index;
                        }
                        else
                        {
                            // second argument targets team where current commander needs to get demoted
                            String sTarget = __1.Split(' ')[1];

                            if (String.Equals(sTarget, "Human", StringComparison.OrdinalIgnoreCase))
                            {
                                // check gamemode - if Humans vs Aliens or the other ones
                                if (strategyInstance.TeamsVersus == Il2Cpp.MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS)
                                {
                                    // if it's human vs aliens then human translates to the Human (Sol) team index
                                    iTargetTeamIndex = 2;
                                }
                                // otherwise, it's ambigious and we can't make a decision
                            }
                            else if (String.Equals(sTarget, "Alien", StringComparison.OrdinalIgnoreCase))
                            {
                                iTargetTeamIndex = 0;
                            }
                            else if (sTarget.Contains("Cent", StringComparison.OrdinalIgnoreCase))
                            {
                                iTargetTeamIndex = 1;
                            }
                            else if (String.Equals(sTarget, "Sol", StringComparison.OrdinalIgnoreCase))
                            {
                                iTargetTeamIndex = 2;
                            }

                            // check if we still don't have a valid target
                            if (iTargetTeamIndex < 0)
                            {
                                // notify player on invalid usage
                                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + ": Valid targets are Alien, Centauri, or Sol", false);
                                return;
                            }
                        }

                        Il2Cpp.Team TargetTeam = Il2Cpp.Team.GetTeamByIndex(iTargetTeamIndex);
                        if (TargetTeam != null)
                        {
                            // check if they have a commander to demote
                            bool bHasCommander = (strategyInstance.GetCommanderForTeam(TargetTeam) != null);

                            if (bHasCommander)
                            {
                                // demote
                                DemoteTeamsCommander(strategyInstance, TargetTeam);
                                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + TargetTeam.TeamName + "'s commander was demoted", false);
                                return;
                            }
                            else
                            {
                                Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + ": No commander found on specified team", false);
                                return;
                            }
                        }
                        else
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + ": No valid team found", false);
                            return;
                        } 
                    }

                    bool bIsCommanderBanCommand = (String.Equals(__1.Split(' ')[0], "!commanderban", StringComparison.OrdinalIgnoreCase) || 
                                                    String.Equals(__1.Split(' ')[0], "!cmdrban", StringComparison.OrdinalIgnoreCase));
                    if (bIsCommanderBanCommand)
                    {
                        // check for authorized. for now, only server operator is considered authorized
                        Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();

                        if (__0 != serverPlayer)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + " is not authorized to use !cmdrban", false);
                            return;
                        }

                        // count number of arguments
                        int iArguments = __1.Split(' ').Count();
                        if (iArguments > 2)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + ": Too many arguments specified", false);
                            return;
                        }

                        String sTarget = __1.Split(' ')[1];
                        Il2Cpp.Player PlayerToCmdrBan = FindTargetPlayer(sTarget);

                        if (PlayerToCmdrBan == null)
                        {
                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, ChatPrefix + __0.PlayerName + ": Ambiguous or invalid target", false);
                            return;
                        }

                        AddCommanderBan(PlayerToCmdrBan);
                        return;
                    }
                }
                catch (Exception error)
                {
                    CommanderManager.PrintError(error, "Failed to run MessageReceived");
                }
            }
        }
    }
}