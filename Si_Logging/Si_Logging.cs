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
using MelonLoader;
using MelonLoader.Utils;
using Si_Logging;
using UnityEngine;
using AdminExtension;
using static Il2Cpp.Interop;

[assembly: MelonInfo(typeof(HL_Logging), "Half-Life Logger", "0.9.7", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_Logging
{
    // https://developer.valvesoftware.com/wiki/HL_Log_Standard
    public class HL_Logging : MelonMod
    {
        const int MaxTeams = 3;
        static Player[]? lastCommander;

        static MelonPreferences_Category? _modCategory;
        static MelonPreferences_Entry<bool>? Pref_Log_Damage;
        static MelonPreferences_Entry<bool>? Pref_Log_Kills_Include_AI_vs_Player;

        public static void PrintLogLine(string LogMessage, bool suppressConsoleOutput = false)
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

                if (!suppressConsoleOutput)
                {
                    MelonLogger.Msg(LogLine);
                }

                System.IO.File.AppendAllText(CurrentLogFile, LogLine + Environment.NewLine);
            }
        }

        public static void AddFirstLogLine()
        {
            string FirstLine = "Log file started (file \"" + GetLogSubPath() + "\") (game \"" + MelonEnvironment.GameExecutablePath + "\") (version \"" + MelonLoader.InternalUtils.UnityInformationHandler.GameVersion + "\") (hostid \"" + NetworkGameServer.GetServerID().ToString() + "\")";
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

        public static string GetPlayerID(Player player)
        {
            return player.ToString().Split('_')[1];
        }

        public override void OnInitializeMelon()
        {
            try
            {
                _modCategory ??= MelonPreferences.CreateCategory("Silica");
                Pref_Log_Damage ??= _modCategory.CreateEntry<bool>("Logging_LogDamage", false);
                Pref_Log_Kills_Include_AI_vs_Player ??= _modCategory.CreateEntry<bool>("Logging_LogKills_IncludeAIvsPlayer", true);

                if (!System.IO.Directory.Exists(GetLogFileDirectory()))
                {
                    MelonLogger.Msg("Creating log file directory at: " + GetLogFileDirectory());
                    System.IO.Directory.CreateDirectory(GetLogFileDirectory());
                }

                if (!System.IO.File.Exists(CurrentLogFile))
                {
                    AddFirstLogLine();
                }

                lastCommander = new Player[MaxTeams];
                for (int i = 0; i < MaxTeams; i++)
                {
                    lastCommander[i] = null;
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
                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><>\" entered the game";
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
                    if (__0 != null)
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

                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + ">\" disconnected";
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
            public static void Postfix(Il2Cpp.Player __0)
            {
                try
                {
                    int userID = Math.Abs(__0.GetInstanceID());
                    string teamName;
                    if ( __0.m_Team == null)
                    {
                        teamName = "";
                    }
                    else
                    {
                        teamName = __0.m_Team.TeamName;
                    }
                    string LogLine = "Kick: \"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + "\" was kicked by \"Console\" (message \"\")";
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
        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.OnUnitDestroyed))]
        private static class ApplyPatchOnUnitDestroyed
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Unit __0, Il2Cpp.EDamageType __1, UnityEngine.GameObject __2)
            {
                try
                {
                    if (__0 == null || __2 == null)
                    {
                        return;
                    }

                    if (Pref_Log_Kills_Include_AI_vs_Player == null)
                    {
                        return;
                    }

                    // Attacker
                    BaseGameObject attackerBase = Il2Cpp.GameFuncs.GetBaseGameObject(__2);
                    if (attackerBase == null)
                    {
                        return;
                    }
                    NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                    Player attackerPlayer = attackerNetComp.OwnerPlayer;

                    // Victim
                    Player victimPlayer = __0.m_ControlledBy;

                    bool isVictimHuman = (victimPlayer != null);
                    bool isAttackerHuman = (attackerPlayer != null);
                    
                    if (isVictimHuman)
                    {
                        int victimUserID = Math.Abs(victimPlayer.GetInstanceID());

                        if (attackerNetComp == null)
                        {
                            return;
                        }

                        // Attacker and Victim are both humans
                        if (isAttackerHuman)
                        {
                            bool isSuicide = (attackerPlayer == victimPlayer);
                            if (isSuicide)
                            {

                                string LogLine = "\"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.m_Team.TeamName + ">\" committed suicide with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\")";
                                PrintLogLine(LogLine);
                            }
                            // human-controlled player killed another human-controlled player
                            else
                            {
                                int attackerUserID = Math.Abs(attackerPlayer.GetInstanceID());
                                string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.m_Team.TeamName + ">\" killed \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.m_Team.TeamName + ">\" with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                                PrintLogLine(LogLine);
                            }
                        }
                        else if (Pref_Log_Kills_Include_AI_vs_Player.Value)
                        // Attacker is an AI, Victim is a human
                        {
                            string LogLine = "\"" + __2.ToString().Split('(')[0] + "<><><" + attackerBase.m_Team.TeamName + ">\" killed \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.m_Team.TeamName + ">\" with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                            PrintLogLine(LogLine);
                        }
                    }
                    else if (isAttackerHuman && Pref_Log_Kills_Include_AI_vs_Player.Value)
                    // Attacker is a human, Victim is an AI
                    {
                        int attackerUserID = Math.Abs(attackerPlayer.GetInstanceID());
                        string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.m_Team.TeamName + ">\" killed \"" + __0.ToString().Split('(')[0] + "<><><" + __0.m_Team.TeamName + ">\" with \"" + __2.ToString().Split('(')[0] + "\" (dmgtype \"" + __1.ToString() + "\") (victim \"" + __0.ToString().Split('(')[0] + "\")";
                        PrintLogLine(LogLine);
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
                        string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + theOldTeamName + ">\" joined team \"" + theNewTeamName + "\"";
                        PrintLogLine(LogLine);
                    }
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run OnPlayerChangedTeam");
                }
            }
        }

        // 055. Role Selection - Commander Change
        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.SetCommander))]
        private static class ApplyPatch_MP_Strategy_SetCommander
        {
            public static void Postfix(Il2Cpp.MP_Strategy __instance, Il2Cpp.Team __0, Il2Cpp.Player __1)
            {
                try
                {
                    if (__0 == null || lastCommander == null)
                    {
                        return;
                    }

                    if (__1 != null)
                    {
                        // is it the same as what we already captured?
                        if (__1 == lastCommander[__0.Index])
                        {
                            return;
                        }

                        // a change occurred and this player was promoted
                        int commanderUserID = Math.Abs(__1.GetInstanceID());
                        string LogLine = "\"" + __1.PlayerName + "<" + commanderUserID + "><" + GetPlayerID(__1) + "><" + __0.TeamName + ">\" changed role to \"Commander\"";
                        PrintLogLine(LogLine);

                        // check if another player was demoted
                        if (lastCommander[__0.Index] != null)
                        {
                            // this player is no longer commander
                            int prevCommanderUserID = Math.Abs(lastCommander[__0.Index].GetInstanceID());
                            LogLine = "\"" + lastCommander[__0.Index].PlayerName + "<" + prevCommanderUserID + "><" + GetPlayerID(lastCommander[__0.Index]) + "><" + __0.TeamName + ">\" changed role to \"Infantry\"";
                            PrintLogLine(LogLine);
                        }

                        lastCommander[__0.Index] = __1;
                    }
                    else
                    {
                        // is it the same as what we already captured?
                        if (lastCommander[__0.Index] == null)
                        {
                            return;
                        }

                        // a change occurred and this player is no longer commander
                        int prevCommanderUserID = Math.Abs(lastCommander[__0.Index].GetInstanceID());
                        string LogLine = "\"" + lastCommander[__0.Index].PlayerName + "<" + prevCommanderUserID + "><" + GetPlayerID(lastCommander[__0.Index]) + "><" + __0.TeamName + ">\" changed role to \"Infantry\"";
                        PrintLogLine(LogLine);

                        lastCommander[__0.Index] = null;
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::SetCommander");
                }
            }
        }

        // 056. Change Name
        [HarmonyPatch(typeof(Il2Cpp.NetworkLayer), nameof(Il2Cpp.NetworkLayer.SendPlayerChangeName))]
        private static class ApplyPatchSendPlayerChangeName
        {
            public static void Postfix(Il2CppSteamworks.CSteamID __0, int __1, string __2)
            {
                try
                {
                    // Player player = GetPlayerFromSteamID(__0);
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

        // 058. Injuring
        [HarmonyPatch(typeof(Il2Cpp.DamageManager), nameof(Il2Cpp.DamageManager.OnDamageReceived))]
        private static class ApplyPatchOnDamageReceived
        {
            public static void Postfix(Il2Cpp.DamageManager __instance, UnityEngine.Collider __0, float __1, Il2Cpp.EDamageType __2, UnityEngine.GameObject __3, UnityEngine.Vector3 __4)
            {
                // should we log the damage?
                if (Pref_Log_Damage != null && !Pref_Log_Damage.Value)
                {
                    return;
                }

                // was it a non-human-controlled instigator?
                if (__3 == null)
                {
                    return;
                }

                BaseGameObject attackerBase = GameFuncs.GetBaseGameObject(__3);
                if (attackerBase == null)
                {
                    return;
                }

                NetworkComponent attackerNetComp = attackerBase.NetworkComponent;
                if (attackerNetComp == null)
                {
                    return;
                }

                Player attackerPlayer = attackerNetComp.OwnerPlayer;
                if (attackerPlayer == null)
                {
                    return;
                }

                // was it a non-human-controlled victim?
                BaseGameObject victimBase = __instance.Owner;
                if (victimBase == null)
                {
                    return;
                }

                NetworkComponent victimNetComp = victimBase.NetworkComponent;
                if (victimNetComp == null)
                {
                    return;
                }

                Player victimPlayer = victimNetComp.OwnerPlayer;
                if (victimPlayer == null)
                {
                    return;
                }

                // was damage less than (or equal to) 1?
                int damage = (int)Math.Ceiling(__1);
                if (damage <= 1)
                {
                    return;
                }

                int attackerUserID = Math.Abs(attackerPlayer.GetInstanceID());
                int victimUserID = Math.Abs(victimPlayer.GetInstanceID());
                string LogLine = "\"" + attackerPlayer.PlayerName + "<" + attackerUserID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayer.m_Team.TeamName + ">\" attacked \"" + victimPlayer.PlayerName + "<" + victimUserID + "><" + GetPlayerID(victimPlayer) + "><" + victimPlayer.m_Team.TeamName + ">\" with \"" + __3.ToString().Split('(')[0] + "\"" + " (damage \"" + damage.ToString() + "\")";
                PrintLogLine(LogLine, true);
            }
        }

        // 059. Player-Player Actions
        // None for now. Re-evaluate with updates

        // 060. Player Objectives/Actions - Structure Kill
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

                                    string attackerPlayerTeam;
                                    if (attackerPlayer.m_Team == null)
                                    {
                                        attackerPlayerTeam = "";
                                    }
                                    else
                                    {
                                        attackerPlayerTeam = attackerPlayer.m_Team.TeamName;
                                    }

                                    string structTeam;
                                    if (__0.m_Team == null)
                                    {
                                        structTeam = "";
                                    }
                                    else
                                    {
                                        structTeam = __0.m_Team.TeamName;
                                    }
  
                                    string LogLine = "\"" + attackerPlayer.PlayerName + "<" + userID + "><" + GetPlayerID(attackerPlayer) + "><" + attackerPlayerTeam + ">\" triggered \"structure_kill\" (structure \"" + structName + "\") (struct_team \"" + structTeam + "\")";
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

                                string playerTeam;
                                if (thisPlayer.m_Team == null)
                                {
                                    playerTeam = "";
                                }
                                else
                                {
                                    playerTeam = thisPlayer.m_Team.TeamName;
                                }

                                string PlayerLogLine = "Player \"" + thisPlayer.PlayerName + "<" + userID + "><" + GetPlayerID(thisPlayer) + "><" + playerTeam + ">\" scored \"" + thisPlayer.m_Score + "\" (kills \"" + thisPlayer.m_Kills + "\") (deaths \"" + thisPlayer.m_Deaths + "\")";
                                PrintLogLine(PlayerLogLine);
                            }
                        }

                        string RoundWinLogLine = "World triggered \"Round_Win\" (gametype \"" + versusMode.ToString() + "\")";
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
        /*[HarmonyPatch(typeof(Il2Cpp.Team), nameof(Il2Cpp.Team.UpdateTechnologyTier))]
        private static class ApplyPatchUpdateTechnologyTier
        {
            public static void Postfix(Il2Cpp.Team __instance)
            {
                try
                {
                    
                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run UpdateTechnologyTier");
                }
            }
        }*/



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
                    MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
                    MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

                    string RoundWinLogLine = "World triggered \"Round_Start\" (gametype \"" + versusMode.ToString() + "\")";
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
                    // each faction has its own chat manager but by looking at alien and only global messages this catches commands only once
                    if (__instance != null && __0 != null && __instance.ToString().Contains("alien"))
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
                            string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + ">\" say \"" + __1 + "\"";
                            PrintLogLine(LogLine);
                            
                        }
                        else
                        {
                            string LogLine = "\"" + __0.PlayerName + "<" + userID + "><" + GetPlayerID(__0) + "><" + teamName + ">\" say_team \"" + __1 + "\"";
                            PrintLogLine(LogLine);
                        }
                    }

                }
                catch (Exception error)
                {
                    PrintError(error, "Failed to run Chat::MessageReceived");
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