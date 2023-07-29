/*
 Silica Basic Team Balance Mod
 Copyright (C) 2023 by databomb
 
 * Description *
 For Silica listen servers, only permit players to cause a moderate
 team imbalance when attempting to join other teams.

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

[assembly: MelonInfo(typeof(BasicTeamBalance), "[Si] Basic Team Balance", "1.0.8", "databomb")]
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

        static Il2Cpp.Player? LastPlayerChatMessage;

        private const string ModCategory = "Silica";

        public override void OnInitializeMelon()
        {
            if (_modCategory == null)
            {
                _modCategory = MelonPreferences.CreateCategory(ModCategory);
            }
            if (_TwoTeamBalanceDivisor == null)
            {
                _TwoTeamBalanceDivisor = _modCategory.CreateEntry<float>("TeamBalance_TwoTeam_Divisor", 8.0f);
            }
            if (_TwoTeamBalanceAddend == null)
            {
                _TwoTeamBalanceAddend = _modCategory.CreateEntry<float>("TeamBalance_TwoTeam_Addend", 1.0f);
            }
            if (_ThreeTeamBalanceDivisor == null)
            {
                _ThreeTeamBalanceDivisor = _modCategory.CreateEntry<float>("TeamBalance_ThreeTeam_Divisor", 10.0f);
            }
            if (_ThreeTeamBalanceAddend == null)
            {
                _ThreeTeamBalanceAddend = _modCategory.CreateEntry<float>("TeamBalance_ThreeTeam_Addend", 0.0f);
            }
        }

        public static void SendClearRequest(ulong thisPlayerSteam64, int thisPlayerChannel)
        {
             // send RPC_ClearRequest
            Il2Cpp.GameByteStreamWriter clearWriteInstance = Il2Cpp.GameMode.CurrentGameMode.CreateRPCPacket(3);
            if (clearWriteInstance != null)
            {
                clearWriteInstance.WriteUInt64(thisPlayerSteam64);
                clearWriteInstance.WriteByte((byte)thisPlayerChannel);
                Il2Cpp.GameMode.CurrentGameMode.SendRPCPacket(clearWriteInstance);
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

        public static Il2Cpp.Team FindLowestPopulationTeam(Il2Cpp.MP_Strategy.ETeamsVersus versusMode)
        {
            int LowestTeamNumPlayers = 37;
            Il2Cpp.Team? LowestPopTeam = null;

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
                // has the team been eliminated?
                else if (versusMode == MP_Strategy.ETeamsVersus.HUMANS_VS_HUMANS_VS_ALIENS && thisTeam.BaseStructure == null)
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
        private static class ApplyPatchJoinTeamPacket
        {
            public static bool Prefix(Il2Cpp.MP_Strategy __instance, Il2Cpp.GameByteStreamReader __0, byte __1)
            {
                try
                {
                    if (__instance != null && __0 != null)
                    {
                        // check for RPC_RequestJoinTeam byte
                        if (__1 == 1)
                        {
                            ulong PlayerSteam64 = __0.ReadUInt64();
                            Il2CppSteamworks.CSteamID PlayerCSteamID = new CSteamID(PlayerSteam64);
                            PlayerCSteamID.m_SteamID = PlayerSteam64;
                            int PlayerChannel = __0.ReadByte();
                            Il2Cpp.Player JoiningPlayer = Il2Cpp.Player.FindPlayer(PlayerCSteamID, PlayerChannel);
                            Il2Cpp.Team? TargetTeam = __0.ReadTeam();

                            if (JoiningPlayer == null)
                            {
                                return false;
                            }

                            Il2Cpp.Team mTeam = JoiningPlayer.m_Team;

                            if (!UnityEngine.Object.Equals(mTeam, TargetTeam))
                            {
                                // this would normally get processed but check for imbalance
                                if (JoinCausesImbalance(TargetTeam))
                                {
                                    if (JoiningPlayer != null)
                                    {
                                        // if the player hasn't joined a team yet, force them to the other team
                                        if (JoiningPlayer.Team == null)
                                        {
                                            Il2Cpp.MP_Strategy strategyInstance = GameObject.FindObjectOfType<Il2Cpp.MP_Strategy>();
                                            Il2Cpp.MP_Strategy.ETeamsVersus versusMode = strategyInstance.TeamsVersus;
                                            Il2Cpp.Team? ForcedTeam = FindLowestPopulationTeam(versusMode);
                                            if (ForcedTeam != null)
                                            {
                                                // avoid chat spam
                                                if (LastPlayerChatMessage != JoiningPlayer)
                                                {
                                                    Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                                                    Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, HelperMethods.chatPrefix + HelperMethods.GetTeamColor(TargetTeam) + JoiningPlayer.PlayerName + HelperMethods.defaultColor + " was forced to " + HelperMethods.GetTeamColor(ForcedTeam) + ForcedTeam.TeamName + HelperMethods.defaultColor + " to fix imbalance", false);
                                                    LastPlayerChatMessage = JoiningPlayer;
                                                }

                                                MelonLogger.Msg(JoiningPlayer.PlayerName + " was forced to " + ForcedTeam.TeamName + " to fix imbalance");

                                                JoiningPlayer.Team = ForcedTeam;
                                                NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, ForcedTeam);

                                                return false;
                                            }
                                        }
                                        
                                        // avoid chat spam
                                        if (LastPlayerChatMessage != JoiningPlayer)
                                        {
                                            Il2Cpp.Player serverPlayer = Il2Cpp.NetworkGameServer.GetServerPlayer();
                                            Il2Cpp.NetworkLayer.SendChatMessage(serverPlayer.PlayerID, serverPlayer.PlayerChannel, HelperMethods.chatPrefix + HelperMethods.GetTeamColor(JoiningPlayer) + JoiningPlayer.PlayerName + HelperMethods.defaultColor + "'s switch was denied due to imbalance", false);
                                            LastPlayerChatMessage = JoiningPlayer;
                                        }

                                        MelonLogger.Msg(JoiningPlayer.PlayerName + "'s team switch was denied due to team imbalance");
                                    }

                                    SendClearRequest(PlayerSteam64, PlayerChannel);
                                }
                                else
                                {
                                    if (JoiningPlayer != null)
                                    {
                                        JoiningPlayer.Team = TargetTeam;
                                        NetworkLayer.SendPlayerSelectTeam(JoiningPlayer, TargetTeam);
                                    }
                                }
                            }
                            else
                            {
                                SendClearRequest(PlayerSteam64, PlayerChannel);
                            }

                            return false;
                        }
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run MP_Strategy::ProcessNetRPC");
                }

                return true;
            }
        }
    }
}