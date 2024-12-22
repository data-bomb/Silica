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
using Il2CppSystem.Diagnostics;
#else
using System.Diagnostics;
#endif

using HarmonyLib;
using SilicaAdminMod;
using System;
using System.Collections.Generic;
using UnityEngine;
using MelonLoader;

namespace Si_CommanderManagement
{
    public class SuppressChat
    {
        [HarmonyPatch(typeof(Player), nameof(Player.SendServerChatMessage))]
        private static class CommanderManager_Patch_Player_SendServerChatMessage
        {
            static bool Prefix(bool __result, string __0)
            {
                try
                {
                    // for routine map restarts cheats is disabled through the Game.ClearAll() or OnEnable methods
                    // if that's how cheats is getting disabled then no need to replicate that message here
                    string callingMethod = new StackFrame(2, true).GetMethod().Name;
                    if (CommanderManager._SuppressRoundStartCommanderChat.Value && callingMethod.Contains("OnMissionStateChanged"))
                    {
                        return false;
                    }
                    else if (CommanderManager._SuppressChangeCommanderChat.Value && callingMethod.Contains("SetCommander"))
                    {
                        return false;
                    }
                    else if (CommanderManager._SuppressCountdownChat.Value && string.Equals(callingMethod, "UpdateGameLoop"))
                    {
                        return false;
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run Player::SendServerChatMessage");
                }

                return true;
            }
        }
    }
}