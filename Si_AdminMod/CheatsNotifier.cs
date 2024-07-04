/*
Silica Admin Mod
Copyright (C) 2024 by databomb

* Description *
Provides basic admin mod system to allow additional admins beyond
the host.

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
using Il2CppSystem.Diagnostics;
#else
using System.Diagnostics;
#endif

using HarmonyLib;
using MelonLoader;
using System;
using System.Text;
using UnityEngine;

namespace SilicaAdminMod
{
    public class CheatsNotification
    {
        [HarmonyPatch(typeof(Game), nameof(Game.CheatsEnabled), MethodType.Setter)]
        private static class ApplyPatch_Game_CheatsEnabled_Setter
        {
            public static void Postfix(bool __0)
            {
                try
                {
                    // check if this is password protected and server host does not want to replicate cheats status change to all clients
                    if (NetworkGameServer.GetServerPasswordProtected() && !SiAdminMod.Pref_Admin_ReplicateCheatsForPasswordedServers.Value)
                    {
                        return;
                    }
                    
                    // for routine map restarts cheats is disabled through the Game.ClearAll() or OnEnable methods
                    // if that's how cheats is getting disabled then no need to replicate that message here
                    string callingMethod = new StackFrame(2, true).GetMethod().Name;
                    if (string.Equals(callingMethod, "OnEnable") || string.Equals(callingMethod, "ClearAll"))
                    {
                        return;
                    }

                    HelperMethods.AlertAdminAction(null, (Game.CheatsEnabled ? "ENABLED CHEATS" : "disabled cheats") + ((Game.CheatsEnabled && SiAdminMod.Pref_Admin_StopNonAdminCheats.Value) ? " (admins only)" : ""));
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Game::CheatsEnabled::Setter");
                }
            }

        }
    }
}
