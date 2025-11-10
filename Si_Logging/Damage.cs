/*
 Silica Logging Mod
 Copyright (C) 2023-2025 by databomb
 
 * Description *
 For Silica servers, creates a log file with console replication
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

#if NET6_0
using Il2Cpp;
#endif

using Harmony;
using SilicaAdminMod;
using System;
using System.Collections.Generic;
using System.Linq;
using MelonLoader;

namespace Si_Logging
{
    public partial class HL_Logging
    {
        public static List<AttackerInfo>[] VictimDamage = null!;

        public static class DamageDatabase
        {
            
            public static void AddDamage(Player victim, Player attacker, float amount)
            {
                int index = victim.GetIndex();

                if (VictimDamage[index] == null)
                {
                    MelonLogger.Msg($"Creating new damage database entry for {victim.PlayerName}");
                    VictimDamage[index] = new List<AttackerInfo>();
                }

                int attackerIndex = VictimDamage[index].FindIndex(info => info.SteamId == attacker.PlayerID.SteamID.m_SteamID);

                // MelonLogger.Msg($"Adding damage with victim index {index} and attacker index {attackerIndex}");

                // -1: no steamID found in VictimDamage
                if (attackerIndex < 0)
                {
                    AttackerInfo newAttacker = new AttackerInfo
                    {
                        AttackerName = attacker.PlayerName,
                        SteamId = attacker.PlayerID.SteamID.m_SteamID,
                        TeamIndex = (attacker.Team == null ? -1 : attacker.Team.Index),
                        Quantity = 1,
                        TotalDamage = amount
                    };

                    VictimDamage[index].Add(newAttacker);

                    MelonLogger.Msg($"Added new attacker {newAttacker.AttackerName} to victim index {index}");

                    return;
                }

                //MelonLogger.Msg($"Updated victim index {index} with additional damage for {VictimDamage[index][attackerIndex].AttackerName}");

                VictimDamage[index][attackerIndex].TotalDamage += amount;
                VictimDamage[index][attackerIndex].Quantity++;
            }

            public static void OnPlayerDeath(Player victim)
            {
                int index = victim.GetIndex();

                // skip if there's nothing to print or team is invalid (GetIndex could return -1 in odd situations)
                if (index < 0 || VictimDamage.GetLength(0) > index || VictimDamage[index] == null || VictimDamage[index].Count <= 0 || victim.Team == null)
                {
                    MelonLogger.Warning("Could not print player death stats. Index value: ", index, " and VictimDamage Size: ", VictimDamage.GetLength(0));
                    return;
                }

                // print stats
                string[] stats = GenerateStats(index, victim.Team.Index);
                HelperMethods.SendConsoleMessageToPlayer(victim, stats);

                // reset stats
                ClearIndex(index);
            }

            public static string[] GenerateStats(int index, int teamIndex)
            {
                List<string> stats = new List<string>();

                stats.Add(Header());
                foreach (AttackerInfo attackerInfo in VictimDamage[index])
                {
                    string color = GetConsoleColorCode(teamIndex, attackerInfo.TeamIndex);
                    stats.Add($"{color}{attackerInfo.AttackerName} caused {attackerInfo.TotalDamage.ToString("#.#")} dmg in {attackerInfo.Quantity} {(attackerInfo.Quantity > 1 ? "hits" : "hit")}</color>");
                }
                stats.Add(Footer());

                return stats.ToArray();
            }

            public static string GetConsoleColorCode(int victimIndex, int attackerIndex)
            {
                // color friendly attackers red
                if (victimIndex == attackerIndex)
                {
                    return "<color=#964545>";
                }

                return "<color=#FFFFFF>";
            }

            public static string Header()
            {
                return "<color=#FFFFFF>---------- <b>PvP Damage Report</b> ----------</color>";
            }

            public static string Footer()
            {
                return "<color=#FFFFFF>-----------------------------------------------------</color>";
            }

            public static void ClearIndex(int i)
            {
                if (VictimDamage[i] == null)
                {
                    return;
                }

                VictimDamage[i].Clear();
            }

            public static void ResetRound()
            {
                for (int i = 0; i < VictimDamage.Length; i++)
                {
                    ClearIndex(i);
                }
            }

            static DamageDatabase()
            {
                VictimDamage = new List<AttackerInfo>[66];
            }
        }

        public class AttackerInfo
        {
            private string _attackerName = null!;

            public string AttackerName
            {
                get => _attackerName;
                set => _attackerName = value ?? throw new ArgumentNullException("Attacker name is required.");
            }
            public ulong SteamId
            {
                get;
                set;
            }

            public int TeamIndex
            {
                get;
                set;
            }

            public int Quantity
            {
                get;
                set;
            }

            public float TotalDamage 
            { 
                get;
                set; 
            }
        }
    }
}