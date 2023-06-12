/*
 Silica Logging Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, creates a log file with console replication
 in the Half-Life log standard format.

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
using Il2CppSteamworks;
using Il2CppInterop.Runtime.Runtime;
using MelonLoader;
using MelonLoader.Utils;
using Si_Logging;
using UnityEngine;
using System.Xml;
using System.Timers;

[assembly: MelonInfo(typeof(HL_Logging), "Half-Life Logger", "0.8.5", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_Logging
{
    // https://developer.valvesoftware.com/wiki/HL_Log_Standard
    public class HL_Logging : MelonMod
    {
        public static void PrintLogLine(string LogMessage)
        {
            if (LogMessage != null)
            {
                string TempLogFile = GetLogFilePath();
                if (TempLogFile != CurrentLogFile)
                {
                    System.IO.File.AppendAllText(CurrentLogFile, GetLogPrefix() + "Log file closed" + Environment.NewLine);
                    CurrentLogFile = TempLogFile;
                    AddFirstLogLine();
                }
                string LogLine = GetLogPrefix() + LogMessage;
                MelonLogger.Msg(LogLine);
                System.IO.File.AppendAllText(CurrentLogFile, LogLine + Environment.NewLine);
            }
        }

        public static void AddFirstLogLine()
        {
            string FirstLine = "Log file started (file \"" + GetLogSubPath() + "\") (game \"" + MelonEnvironment.GameExecutablePath + "\") (version \"" + MelonLoader.InternalUtils.UnityInformationHandler.GameVersion + "\")";
            System.IO.File.AppendAllText(CurrentLogFile, GetLogPrefix() + FirstLine + Environment.NewLine);
        }

        // Generate prefix in format "L mm/dd/yyyy - hh:mm:ss:"
        public static string GetLogPrefix()
        {
            DateTime currentDateTime = DateTime.Now;
            string LogPrefix = "L " + currentDateTime.ToString("MM/dd/yyyy - HH:mm:ss: ");
            return LogPrefix;
        }

        public static string GetLogFileDirectory()
        {
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, @"logs\");
        }

        public static string GetLogSubPath()
        {
            return @"logs\" + GetLogName();
        }

        public static string GetLogFilePath()
        {
            string LogSubPath = GetLogSubPath();
            return System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, LogSubPath);
        }

        public static string GetLogName()
        {
            DateTime currentDateTime = DateTime.Now;
            return "L" + currentDateTime.ToString("yyyyMMdd") + ".log";
        }

        static String CurrentLogFile = GetLogFilePath();

        public static void PrintError(Exception exception, string? message = null)
        {
            if (message != null)
            {
                MelonLogger.Msg(message);
            }
            MelonLogger.Error(exception.Message);
            MelonLogger.Error(exception.StackTrace);
            MelonLogger.Error(exception.TargetSite);
        }

        public override void OnInitializeMelon()
        {
            try
            {
                if (!System.IO.Directory.Exists(GetLogFileDirectory()))
                {
                    MelonLogger.Msg("Creating log file directory at: " + GetLogFileDirectory());
                    System.IO.Directory.CreateDirectory(GetLogFileDirectory());
                }

                if (!System.IO.File.Exists(CurrentLogFile))
                {
                    AddFirstLogLine();
                }
            }
            catch (Exception error)
            {
                PrintError(error, "Failed to initialize log directories or files");
            }
        }

        // 050. Connection
        [HarmonyPatch(typeof(Il2Cpp.NetworkGameServer), nameof(Il2Cpp.NetworkGameServer.OnP2PSessionRequest))]
        private static class ApplyPatchOnP2PSessionRequest
        {
            public static void Postfix(Il2Cpp.NetworkGameServer __instance, Il2CppSteamworks.P2PSessionRequest_t __0)
            {
                try
                {
                    // TODO: Find IP address and port of client
                    string LogLine = "\"...<><" + __0.m_steamIDRemote.ToString() + "><> connected, address \"127.0.0.1:27015\"";
                    PrintLogLine(LogLine);
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnP2PSessionRequest");
                }
            }
        }

        // TODO: 050b. Validation

        // 051. Enter Game
        [HarmonyPatch(typeof(Il2Cpp.GameMode), nameof(Il2Cpp.GameMode.OnPlayerJoinedBase))]
        private static class ApplyPatchOnPlayerJoinedBase
        {
            public static void Postfix(Il2Cpp.GameMode __instance, Il2Cpp.Player __0)
            {
                try
                {
                    if (__0 != null)
                    {
                        // TODO: Find current name
                        int userID = Math.Abs(__0.GetInstanceID());
                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + __0.ToString().Split('_')[1] + "><>\" entered the game";
                        PrintLogLine(LogLine);
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnPlayerJoinedBase");
                }
            }
        }

        // 052. Disconnection
        [HarmonyPatch(typeof(Il2Cpp.GameMode), nameof(Il2Cpp.GameMode.OnPlayerLeftBase))]
        private static class ApplyPatchOnPlayerLeftBase
        {
            public static void Prefix(Il2Cpp.GameMode __instance, Il2Cpp.Player __0)
            {
                try
                {
                    /* fix to grab from earlier source
                     *  [18:11:33.064] [UnityExplorer] [Unity] SERVER - InformDisconnect: [unknown]
                        [18:11:33.067] [UnityExplorer] [Unity] Player ID: 76561197981746273, channel: 0 left
                        [18:11:33.069] [UnityExplorer] [Unity] Deleted 0 network objects for player ID: '76561197981746273', channel: 0
                        [18:11:33.069] [Half-Life_Logger] Failed to run OnPlayerLeftBase
                     */
                    if (__0 != null)
                    {
                        int userID = Math.Abs(__0.GetInstanceID());
                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + __0.ToString().Split('_')[1] + "><" + __0.m_Team.TeamName + ">\" disconnected";
                        PrintLogLine(LogLine);
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnPlayerLeftBase");
                }
            }
        }

        // 052b. Kick
        [HarmonyPatch(typeof(Il2Cpp.NetworkGameServer), nameof(Il2Cpp.NetworkGameServer.KickPlayer))]
        private static class ApplyPatchKickPlayer
        {
            public static void Postfix(Il2Cpp.Player __0, bool __1)
            {
                try
                {
                    int userID = Math.Abs(__0.GetInstanceID());
                    string LogLine = "Kick: \"" + __0.PlayerName + "<" + userID + "><" + __0.ToString().Split('_')[1] + "><" + __0.m_Team.TeamName + "\" was kicked by \"Console\" (message \"\")";
                    PrintLogLine(LogLine);
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run KickPlayer");
                }
            }
        }

        // 053. Suicides
        // 057. Kills
        // TODO: Grab weapon names instead of damage type
        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnUnitDestroyed))]
        private static class ApplyPatchOnUnitDestroyed
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Unit __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    if (__0 != null && __2 != null)
                    {
                        // Victim
                        Il2Cpp.Player victimPlayer = __0.m_ControlledBy;

                        // Attacker
                        Il2Cpp.BaseGameObject attackerBase = Il2Cpp.GameFuncs.GetBaseGameObject(__2);
                        if (victimPlayer != null && attackerBase != null)
                        {
                            Il2Cpp.NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                            // was teamkiller a playable character?
                            if (attackerNetComp != null)
                            {
                                Il2Cpp.Player attackerPlayer = attackerNetComp.OwnerPlayer;

                                if (attackerPlayer != null)
                                {
                                    int victimUserID = Math.Abs(victimPlayer.GetInstanceID());

                                    // suicide?
                                    if (attackerPlayer == victimPlayer)
                                    {
                                        
                                        string LogLine = "\"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + victimPlayer.ToString().Split('_')[1] + "><" + victimPlayer.m_Team.TeamName + ">\" committed suicide with \"" + __1.ToString() + "\"";
                                        PrintLogLine(LogLine);
                                    }
                                    // human-controlled player killed another human-controlled player
                                    else
                                    {
                                        int attackerUserID = Math.Abs(attackerPlayer.GetInstanceID());
                                        string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + attackerPlayer.ToString().Split('_')[1] + "><" + attackerPlayer.m_Team.TeamName + ">\" killed \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + victimPlayer.ToString().Split('_')[1] + "><" + victimPlayer.m_Team.TeamName + ">\" with \"" + __1.ToString() + "\"";
                                        PrintLogLine(LogLine);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnUnitDestroyed");
                }
            }
        }

        // 054. Team Selection
        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnPlayerChangedTeam))]
        private static class ApplyPatchOnPlayerChangedTeam
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Player __0, Il2Cpp.Team __1, Il2Cpp.Team __2)
            {
                try
                {
                    string theOldTeamName;
                    if (__1 == null)
                    {
                        theOldTeamName = "";
                    }
                    else
                    {
                        theOldTeamName = __1.TeamName;
                    }

                    string theNewTeamName;
                    if (__2 == null)
                    {
                        // this happens at the begginning of each map and isn't useful logging information
                        return;
                    }
                    else
                    {
                        theNewTeamName = __2.TeamName;
                    }

                    if (__0 != null)
                    {
                        int userID = Math.Abs(__0.GetInstanceID());
                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + __0.ToString().Split('_')[1] + "><" + theOldTeamName + ">\" joined team \"" + theNewTeamName + "\"";
                        PrintLogLine(LogLine);
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnPlayerChangedTeam");
                }
            }
        }

        // TODO: 055. Role Selection

        // 056. Change Name
        [HarmonyPatch(typeof(Il2Cpp.NetworkLayer), nameof(Il2Cpp.NetworkLayer.SendPlayerChangeName))]
        private static class ApplyPatchSendPlayerChangeName
        {
            public static void Postfix(Il2CppSteamworks.CSteamID __0, int __1, string __2)
            {
                try
                {
                    // TODO: grab old name and current team
                    string LogLine = "\"...<><" + __0.ToString() + "><>\" changed name to \"" + __2 + "\"";
                    PrintLogLine(LogLine);
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run SendPlayerChangeName");
                }
            }
        }

        // TODO: 058. Injuring

        // 059. Player-Player Actions
        // None for now. Re-evaluate with updates

        // 060. Player Objectives/Actions - Structure Kill
        // TODO: Fix error- Object reference not set to an instance of an object.
        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnStructureDestroyed))]
        private static class ApplyPatchOnStructureDestroyed
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Structure __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    if (__0 != null && __2 != null)
                    {
                        // Attacker
                        Il2Cpp.BaseGameObject attackerBase = Il2Cpp.GameFuncs.GetBaseGameObject(__2);
                        if (attackerBase != null)
                        {
                            Il2Cpp.NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                            // was teamkiller a playable character?
                            if (attackerNetComp != null)
                            {
                                Il2Cpp.Player attackerPlayer = attackerNetComp.OwnerPlayer;

                                if (attackerPlayer != null)
                                {
                                    int userID = Math.Abs(attackerPlayer.GetInstanceID());
                                    string structName;
                                    if (__0.ToString().Contains('_'))
                                    {
                                        structName = __0.ToString().Split('_')[0];
                                    }
                                    else if (__0.ToString().Contains('('))
                                    {
                                        structName = __0.ToString().Split('(')[0];
                                    }
                                    else
                                    {
                                        structName = __0.ToString();
                                    }
  
                                    string LogLine = "\"" + attackerPlayer.PlayerName + "<" + userID + "><" + attackerPlayer.ToString().Split('_')[1] + "><" + attackerPlayer.m_Team.TeamName + ">\" triggered \"structure_kill\" (structure \"" + structName + "\") (struct_team \"" + __0.m_Team.TeamName + "\")";
                                    PrintLogLine(LogLine);
                                }

                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnStructureDestroyed");
                }
            }
        }

        // TODO: 060. Player Objectives/Actions
        // Ideas: Enter/exit vehicle. Take control of bug.

        // 061. Team Objectives/Actions - Victory
        // 062. World Objectives/Actions - Round_Win
        // 065. Round-End Team Score Report
        // 067. Round-End Player Score Report

        static bool firedRoundEndOnce;

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatchOnGameEnded
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0, Il2Cpp.Team __1)
            {
                try
                {
                    if (!firedRoundEndOnce)
                    {
                        firedRoundEndOnce = true;

                        string VictoryLogLine = "Team \"" + __1.TeamName + "\" triggered \"Victory\"";
                        PrintLogLine(VictoryLogLine);

                        Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
                        Il2Cpp.MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

                        for (int i = 0; i < Il2Cpp.Team.Teams.Count; i++)
                        {
                            Il2Cpp.Team? thisTeam = Il2Cpp.Team.Teams[i];
                            if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS && i == 0)
                            {
                                continue;
                            }
                            else if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS && i == 1)
                            {
                                continue;
                            }

                            // TODO: Investigate what else to use for team score. Add up all player scores? For now resources is used but it's not using Total acculumated resources so need to find something else.
                            string TeamLogLine = "Team \"" + thisTeam.TeamName + "\" scored \"" + thisTeam.TotalResources.ToString() + "\" with \"" + thisTeam.GetNumPlayers().ToString() + "\" players";
                            PrintLogLine(TeamLogLine);
                        }

                        for (int i = 0; i < Il2Cpp.Player.Players.Count; i++)
                        {
                            if (Il2Cpp.Player.Players[i] != null)
                            {
                                Il2Cpp.Player thisPlayer = Il2Cpp.Player.Players[i];
                                int userID = Math.Abs(thisPlayer.GetInstanceID());
                                string PlayerLogLine = "Player \"" + thisPlayer.PlayerName + "<" + userID + "><" + thisPlayer.ToString().Split('_')[1] + "><" + thisPlayer.m_Team.TeamName + ">\" scored \"" + thisPlayer.m_Score + "\" (kills \"" + thisPlayer.m_Kills + "\") (deaths \"" + thisPlayer.m_Deaths + "\")";
                                PrintLogLine(PlayerLogLine);
                            }
                        }

                        string RoundWinLogLine = "World triggered \"Round_Win\"";
                        PrintLogLine(RoundWinLogLine);
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnGameEnded");
                }
            }
        }

        // 061. Team Objectives/Actions - Research Tier
        /*[HarmonyPatch(typeof(Il2Cpp.Team))]
        [HarmonyPatch(nameof(Il2Cpp.Team.CurrentTechnologyTier), MethodType.Setter)]
        private static class ApplyPatchOnSetTechnologyTier
        {
            public static void Postfix(Il2Cpp.Team __instance, ref int __result)
            {
                string LogLine = "Team \"" + __instance.TeamName + "\" triggered \"technology_change\" (tier \"" + __result.ToString() + "\"";
                PrintLogLine(LogLine);
            }
        }*/

        // 062. World Objectives/Actions - Round_Start
        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatchOnGameStarted
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0)
            {
                try
                {
                    string RoundWinLogLine = "World triggered \"Round_Start\"";
                    PrintLogLine(RoundWinLogLine);

                    firedRoundEndOnce = false;
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }

        // 063. Chat
        [HarmonyPatch(typeof(Il2CppSilica.UI.Chat), nameof(Il2CppSilica.UI.Chat.MessageReceived))]
        private static class ApplyPatchMessageReceived
        {
            public static void Postfix(Il2CppSilica.UI.Chat __instance, Il2Cpp.Player __0, string __1, bool __2)
            {
                try
                {
                    if (__instance != null && __0 != null)
                    {
                        int userID = Math.Abs(__0.GetInstanceID());

                        string teamName;
                        if (__0.m_Team == null)
                        {
                            teamName = "";
                        }
                        else
                        {
                            teamName = __0.m_Team.TeamName;
                        }

                        // __2 true = team-only message
                        if (__2 == false)
                        {
                            string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + __0.ToString().Split('_')[1] + "><" + teamName + ">\" say \"" + __1 + "\"";
                            PrintLogLine(LogLine);
                            
                        }
                        else
                        {
                            string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + __0.ToString().Split('_')[1] + "><" + teamName + ">\" say_team \"" + __1 + "\"";
                            PrintLogLine(LogLine);
                        }
                    }

                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run MessageReceived");
                }
            }
        }

        // 064. Team Alliances
        // None for now. Re-evaluate with updates

        // 066. Private Chat
        // None for now. Re-evaluate with updates

        // 068. Weapon Selection
        // None for now. Re-evaluate with updates

        // 069. Weapon Pickup
        // None for now. Re-evaluate with updates
    }
}