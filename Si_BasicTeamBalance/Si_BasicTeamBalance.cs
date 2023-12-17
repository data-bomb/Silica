/*
 Silica Basic Team Balance Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica servers, allows server operators to configure the exact
 amount of team imbalance that is allowed. Additionally, it allows 
 a prevention from switching teams early in the match (to mitigate 
 a common cheating technique to find an enemy's position)

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
using MelonLoader;
using Si_BasicTeamBalance;
using UnityEngine;
using AdminExtension;
using System.Timers;

[assembly: MelonInfo(typeof(BasicTeamBalance), "[Si] Basic Team Balance", "1.1.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_BasicTeamBalance
{
    public class BasicTeamBalance : MelonMod
    {
        static MelonPreferences_Category _modCategory;
        static MelonPreferences_Entry<float> _TwoTeamBalanceDivisor;
        static MelonPreferences_Entry<float> _TwoTeamBalanceAddend;
        static MelonPreferences_Entry<float> _ThreeTeamBalanceDivisor;
        static MelonPreferences_Entry<float> _ThreeTeamBalanceAddend;
        static MelonPreferences_Entry<bool>  _PreventEarlyTeamSwitches;
        static MelonPreferences_Entry<int>   _AllowTeamSwitchAfterTime;

        static Player? LastPlayerChatMessage;
        static bool preventTeamSwitches;
        private static System.Timers.Timer? Timer_AllowTeamSwitches;

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            _modCategory ??= MelonPreferences.CreateCategory(ModCategory);
            _TwoTeamBalanceDivisor ??= _modCategory.CreateEntry<float>("TeamBalance_TwoTeam_Divisor", 8.0f);
            _TwoTeamBalanceAddend ??= _modCategory.CreateEntry<float>("TeamBalance_TwoTeam_Addend", 1.0f);
            _ThreeTeamBalanceDivisor ??= _modCategory.CreateEntry<float>("TeamBalance_ThreeTeam_Divisor", 10.0f);
            _ThreeTeamBalanceAddend ??= _modCategory.CreateEntry<float>("TeamBalance_ThreeTeam_Addend", 0.0f);
            _PreventEarlyTeamSwitches ??= _modCategory.CreateEntry<bool>("TeamBalance_Prevent_EarlySwitching", false);
            _AllowTeamSwitchAfterTime ??= _modCategory.CreateEntry<int>("TeamBalance_Prevent_EarlySwitching_For_Seconds", 360);

            preventTeamSwitches = false;
        }

        public static void SendClearRequest(ulong thisPlayerSteam64, int thisPlayerChannel)
        {
            // send RPC_ClearRequest (3)
            GameByteStreamWriter clearWriteInstance = GameMode.CurrentGameMode.CreateRPCPacket((byte)MP_Strategy.ERPCs.CLEAR_REQUEST);
            if (clearWriteInstance != null)
            {
                clearWriteInstance.WriteUInt64(thisPlayerSteam64);
                clearWriteInstance.WriteByte((byte)thisPlayerChannel);
                GameMode.CurrentGameMode.SendRPCPacket(clearWriteInstance);
            }
        }

        public static bool OneFactionEliminated()
        {
            int TeamsWithMajorStructures = 0;
            for (int i = 0; i < Il2Cpp.Team.Teams.Count; i++)
            {
                Il2Cpp.Team? thisTeam = Il2Cpp.Team.Teams[i];
                int thisTeamMajorStructures = thisTeam.NumMajorStructures;
                if (thisTeamMajorStructures > 0)
                {
                    TeamsWithMajorStructures++;
                }
            }

            if (TeamsWithMajorStructures < 3)
            {
                return true;
            }

            return false;
        }

        public static int GetNumberOfActiveTeams(Il2Cpp.MP_Strategy.ETeamsVersus versusMode)
        {
            int NumActiveTeams = 0;
            switch (versusMode)
            {
                case MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS:
                case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS:
                    {
                        NumActiveTeams = 2;
                        break;
                    }
                case MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS:
                    {
                        // need to determine if one faction has been eliminated
                        if (OneFactionEliminated())
                        {
                            NumActiveTeams = 2;
                        }
                        else
                        {
                            NumActiveTeams = 3;
                        }    
                        break;
                    }
            }

            return NumActiveTeams;
        }

        public static Team? FindLowestPopulationTeam(Il2Cpp.MP_Strategy.ETeamsVersus versusMode)
        {
            int LowestTeamNumPlayers = NetworkGameServer.GetPlayersMax() + 1;
            Il2Cpp.Team? LowestPopTeam = null;

            for (int i = 0; i < Il2Cpp.Team.Teams.Count; i++)
            {
                Il2Cpp.Team? thisTeam = Il2Cpp.Team.Teams[i];
                // skip Alien index on HvH
                if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS && i == 0)
                {
                    continue;
                }
                // skip Centauri index on HvA
                else if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_ALIENS && i == 1)
                {
                    continue;
                }
                // has the team been eliminated?
                else if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS && !thisTeam.GetHasAnyMajorStructures())
                {
                    continue;
                }

                int thisTeamNumPlayers = thisTeam.GetNumPlayers();
                if (thisTeamNumPlayers < LowestTeamNumPlayers)
                {
                    LowestTeamNumPlayers = thisTeamNumPlayers;
                    LowestPopTeam = thisTeam;
                }
            }

            return LowestPopTeam;
        }

        // Team Index 0 - Alien
        // Team Index 1 - Human (Centauri)
        // Team Index 2 - Human (Sol)
        public static bool JoinCausesImbalance(Il2Cpp.Team? TargetTeam)
        {
            if (TargetTeam == null)
            {
                return false;
            }

            Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
            Il2Cpp.MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;

            Il2Cpp.Team? LowestPopTeam = null;
            int NumActiveTeams = GetNumberOfActiveTeams(versusMode);
            LowestPopTeam = FindLowestPopulationTeam(versusMode);

            // are we already trying to join the team with lowest pop or did we have an error?
            if (LowestPopTeam == null || LowestPopTeam == TargetTeam)
            {
                return false;
            }

            // what's the player count difference?
            int TargetTeamPop = TargetTeam.GetNumPlayers();
            int PlayerDifference = TargetTeamPop - LowestPopTeam.GetNumPlayers();
            // as a positive number only
            if (PlayerDifference < 0)
            {
                PlayerDifference = -PlayerDifference;
            }

            // determine maximum allowed difference
            int TotalNumPlayers = Il2Cpp.Player.Players.Count;
            int MaxDifferenceAllowed;
            if (NumActiveTeams == 2)
            {
                MaxDifferenceAllowed = (int)Math.Ceiling((TotalNumPlayers / _TwoTeamBalanceDivisor.Value) + _TwoTeamBalanceAddend.Value);
            }
            // more strict enforcement for Humans vs Humans vs Aliens
            else
            {
                MaxDifferenceAllowed = (int)Math.Ceiling((TotalNumPlayers / _ThreeTeamBalanceDivisor.Value) + _ThreeTeamBalanceAddend.Value);
            }


            if (PlayerDifference > MaxDifferenceAllowed)
            {
                return true;
            }

            return false;
        }

        [HarmonyPatch(typeof(Il2Cpp.MP_Strategy), nameof(Il2Cpp.MP_Strategy.ProcessNetRPC))]
        private static class ApplyPatch_MPStrategy_JoinTeam
        {
            public static bool Prefix(Il2Cpp.MP_Strategy __instance, Il2Cpp.GameByteStreamReader __0, byte __1)
            {
                try
                {
                    if (__instance == null || __0 == null)
                    {
                        return true;
                    }

                    // only look at RPC_RequestJoinTeam bytes
                    if (__1 != (byte)MP_Strategy.ERPCs.REQUEST_JOIN_TEAM)
                    {
                        return true;
                    }

                    // after this point we will modify the read pointers so we have to return false
                    ulong PlayerSteam64 = __0.ReadUInt64();
                    CSteamID PlayerCSteamID = new CSteamID(PlayerSteam64);
                    PlayerCSteamID.m_SteamID = PlayerSteam64;
                    int PlayerChannel = __0.ReadByte();
                    Player JoiningPlayer = Player.FindPlayer(PlayerCSteamID, PlayerChannel);
                    Team? TargetTeam = __0.ReadTeam();

                    if (JoiningPlayer == null)
                    {
                        return false;
                    }

                    Team mTeam = JoiningPlayer.m_Team;

                    // requests to rejoin the same team
                    if (UnityEngine.Object.Equals(mTeam, TargetTeam))
                    {
                        SendClearRequest(PlayerSteam64, PlayerChannel);
                        return false;
                    }

                    // these would normally get processed at this point but check if early team switching is being stopped and the player already has a team
                    if (_PreventEarlyTeamSwitches.Value && GameMode.CurrentGameMode.GameOngoing && preventTeamSwitches && JoiningPlayer.Team != null)
                    {
                        // avoid chat spam
                        if (LastPlayerChatMessage != JoiningPlayer)
                        {
                            Player serverPlayer = NetworkGameServer.GetServerPlayer();
                            NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, HelperMethods.chatPrefix + HelperMethods.GetTeamColor(JoiningPlayer) + JoiningPlayer.PlayerName + HelperMethods.defaultColor + "'s switch was denied due to early game team lock", false);
                            LastPlayerChatMessage = JoiningPlayer;
                        }

                        MelonLogger.Msg(JoiningPlayer.PlayerName + "'s team switch was denied due to early game team lock");

                        SendClearRequest(PlayerSteam64, PlayerChannel);
                        return false;
                    }

                    // the team change should be permitted as it doesn't impact balance
                    if (!JoinCausesImbalance(TargetTeam))
                    {
                        JoiningPlayer.Team = TargetTeam;
                        NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, TargetTeam);
                        return false;
                    }

                    // if the player hasn't joined a team yet, force them to the team that needs it the most
                    if (JoiningPlayer.Team == null)
                    {
                        MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
                        MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;
                        Team? ForcedTeam = FindLowestPopulationTeam(versusMode);
                        if (ForcedTeam != null)
                        {
                            // avoid chat spam
                            if (LastPlayerChatMessage != JoiningPlayer)
                            {
                                Player serverPlayer = NetworkGameServer.GetServerPlayer();
                                NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, HelperMethods.chatPrefix + HelperMethods.GetTeamColor(TargetTeam) + JoiningPlayer.PlayerName + HelperMethods.defaultColor + " was forced to " + HelperMethods.GetTeamColor(ForcedTeam) + ForcedTeam.TeamName + HelperMethods.defaultColor + " to fix imbalance", false);
                                LastPlayerChatMessage = JoiningPlayer;
                            }

                            MelonLogger.Msg(JoiningPlayer.PlayerName + " was forced to " + ForcedTeam.TeamName + " to fix imbalance");

                            JoiningPlayer.Team = ForcedTeam;
                            NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, ForcedTeam);

                            return false;
                        }

                        MelonLogger.Warning("Error in FindLowestPopulationTeam(). Could not find a valid team.");
                        return false;
                    }
                        
                    // the player has already joined a team but the change would cause an imbalance
                                        
                    // avoid chat spam
                    if (LastPlayerChatMessage != JoiningPlayer)
                    {
                        Player serverPlayer = NetworkGameServer.GetServerPlayer();
                        NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, HelperMethods.chatPrefix + HelperMethods.GetTeamColor(JoiningPlayer) + JoiningPlayer.PlayerName + HelperMethods.defaultColor + "'s switch was denied due to imbalance", false);
                        LastPlayerChatMessage = JoiningPlayer;
                    }

                    MelonLogger.Msg(JoiningPlayer.PlayerName + "'s team switch was denied due to team imbalance");

                    SendClearRequest(PlayerSteam64, PlayerChannel);
                    return false;
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::ProcessNetRPC");
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatch_MusicJukeboxHandler_OnGameStarted
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0)
            {
                try
                {
                    if (_PreventEarlyTeamSwitches.Value)
                    {
                        preventTeamSwitches = true;

                        // seconds * 1000 millieseconds/1second = # milliseconds for System.Timers.Timer
                        double interval = _AllowTeamSwitchAfterTime.Value * 1000.0f;
                        Timer_AllowTeamSwitches = new System.Timers.Timer(interval);
                        Timer_AllowTeamSwitches.Elapsed += new ElapsedEventHandler(HandleTimerAllowTeamSwitching);
                        Timer_AllowTeamSwitches.AutoReset = false;
                        Timer_AllowTeamSwitches.Enabled = true;

                        MelonLogger.Msg("Early game team lock set for " + _AllowTeamSwitchAfterTime.Value.ToString() + " seconds.");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }

        private static void HandleTimerAllowTeamSwitching(object source, ElapsedEventArgs e)
        {
            preventTeamSwitches = false;
        }

        // account for if the game ends before the timer expires
        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameEnded))]
        private static class ApplyPatch_OnGameEnded
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0, Il2Cpp.Team __1)
            {
                try
                {
                    if (!_PreventEarlyTeamSwitches.Value || Timer_AllowTeamSwitches == null)
                    {
                        return;
                    }

                    if (Timer_AllowTeamSwitches.Enabled)
                    {
                        Timer_AllowTeamSwitches.Stop();

                        MelonLogger.Msg("Game ended before early game team lock expired. Forcing timer to expire.");
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MusicJukeboxHandler::OnGameStarted");
                }
            }
        }
    }
}