/*
Silica Admin Mod
Copyright (C) 2023 by databomb

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

namespace SilicaAdminMod
{
    public class PlayerAdmin : Player
    {
        public static bool CanAdminExecute(Player callerPlayer, Power power, Player? targetPlayer = null)
        {
            long playerSteamId = (long)callerPlayer.PlayerID.SteamID.m_SteamID;
            Admin? callerMatch = AdminMethods.FindAdminFromSteamId(playerSteamId);
            Power callerPowers = Power.None;
            if (callerMatch != null)
            {
                callerPowers = callerMatch.Powers;
            }

            if (targetPlayer != null)
            {
                if (CanAdminTarget(callerPlayer, targetPlayer))
                {
                    if (AdminMethods.PowerInPowers(power, callerPowers))
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            if (AdminMethods.PowerInPowers(power, callerPowers))
            {
                return true;
            }

            return false;
        }

        public static bool CanAdminTarget(Player callerPlayer, Player targetPlayer)
        {
            long callerSteamId = (long)callerPlayer.PlayerID.SteamID.m_SteamID;
            Admin? callerMatch = AdminMethods.FindAdminFromSteamId(callerSteamId);
            byte callerLevel = 0;
            if (callerMatch != null)
            {
                callerLevel = callerMatch.Level;
            }

            long targetSteamId = (long)targetPlayer.PlayerID.SteamID.m_SteamID;
            Admin? targetMatch = AdminMethods.FindAdminFromSteamId(targetSteamId);
            byte targetLevel = 0;
            if (targetMatch != null)
            {
                targetLevel = targetMatch.Level;
            }

            if (callerLevel > 0 && callerLevel >= targetLevel)
            {
                return true;
            }

            return false;
        }

        public static Power GetAdminPowers(Player callerPlayer)
        {
            long callerSteamId = (long)callerPlayer.PlayerID.SteamID.m_SteamID;
            Admin? match = AdminMethods.FindAdminFromSteamId(callerSteamId);
            if (match != null)
            {
                return match.Powers;
            }

            return Power.None;
        }

        public static byte GetAdminLevel(Player callerPlayer)
        {
            long callerSteamId = (long)callerPlayer.PlayerID.SteamID.m_SteamID;
            Admin? match = AdminMethods.FindAdminFromSteamId(callerSteamId);
            if (match != null)
            {
                return match.Level;
            }

            return 0;
        }

        public static bool IsAdmin(Player callerPlayer)
        {
            long callerSteamId = (long)callerPlayer.PlayerID.SteamID.m_SteamID;
            Admin? match = AdminMethods.FindAdminFromSteamId(callerSteamId);
            if (match != null)
            {
                // filter out non-admin powers
                Power powers = match.Powers;
                powers &= ~(Power.Slot | Power.Vote | Power.Skip | Power.Custom1 | Power.Custom2 | Power.Custom3 | Power.Custom4);

                if (powers != Power.None)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
