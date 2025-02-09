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
                bool alreadyLogged = VictimDamage[index].Any(info => info.AttackerName == attacker.PlayerName);

                if (!alreadyLogged)
                {
                    AttackerInfo newAttacker = new AttackerInfo
                    {
                        AttackerName = attacker.PlayerName,
                        Quantity = 1,
                        TotalDamage = amount
                    };

                    VictimDamage[index].Add(newAttacker);
                    return;
                }

                // supplement record
                VictimDamage[index].Find(info => info.AttackerName == attacker.PlayerName).TotalDamage += amount;
                VictimDamage[index].Find(info => info.AttackerName == attacker.PlayerName).Quantity++;
            }

            public static void OnPlayerDeath(Player victim)
            {
                int index = victim.GetIndex();

                // skip if there's nothing to print
                if (VictimDamage[index].Count <= 0)
                {
                    return;
                }

                // print stats
                string[] stats = GenerateStats(index);
                HelperMethods.SendConsoleMessageToPlayer(victim, stats);

                // reset stats
                ClearIndex(victim.GetIndex());
            }

            public static string[] GenerateStats(int index)
            {
                List<string> stats = new List<string>();
                stats.Add(Break());
                foreach (AttackerInfo attackerInfo in VictimDamage[index])
                {
                    stats.Add($"{attackerInfo.AttackerName} caused {attackerInfo.TotalDamage.ToString("#.#")} over {attackerInfo.Quantity} hits");
                }
                stats.Add(Break());

                return stats.ToArray();
            }

            public static string Break()
            {
                // emulate same number of dashes as existing game console commands
                return "-------------------------------------------------------";
            }

            public static void ClearIndex(int i)
            {
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