/*
Silica Commander Management Mod
Copyright (C) 2023 by databomb

* Description *
For Silica listen servers, allows hosts to use the !kick or !afk command
to disconnect a player without a session ban.

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
using Si_AFKManager;
using AdminExtension;

[assembly: MelonInfo(typeof(AwayFromKeyboard), "AFK Manager", "1.1.0", "databomb")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]

namespace Si_AFKManager
{
    public class AwayFromKeyboard : MelonMod
    {
        static bool AdminModAvailable = false;

        public override void OnLateInitializeMelon()
        {
            AdminModAvailable = RegisteredMelons.Any(m => m.Info.Name == "Admin Mod");

            if (AdminModAvailable)
            {
                HelperMethods.CommandCallback kickCallback = Command_Kick;
                HelperMethods.RegisterAdminCommand("!kick", kickCallback, Power.Kick);
                HelperMethods.RegisterAdminCommand("!afk", kickCallback, Power.Kick);
            }
            else
            {
                MelonLogger.Warning("Dependency missing: Admin Mod");
            }
        }

        public void Command_Kick(Il2Cpp.Player callerPlayer, String args)
        {
            // check for authorized
            if (!callerPlayer.CanAdminExecute(Power.Kick))
            {
                HelperMethods.ReplyToCommand_Player(callerPlayer, "cannot use " + args.Split(' ')[0]);
                return;
            }

            // validate argument count
            int argumentCount = args.Split(' ').Count() - 1;
            if (argumentCount > 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Il2Cpp.Player? playerToKick = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToKick == null)
            {
                HelperMethods.ReplyToCommand(args.Split(' ')[0] + ": Ambiguous or invalid target");
                return;
            }

            if (callerPlayer.CanAdminTarget(playerToKick))
            {
                if (HelperMethods.KickPlayer(playerToKick))
                {
                    HelperMethods.AlertAdminActivity(callerPlayer, playerToKick, "kicked");
                }
                else
                {
                    HelperMethods.ReplyToCommand_Player(playerToKick, "is the host and cannot be targeted");
                }
            }
            else
            {
                HelperMethods.ReplyToCommand_Player(playerToKick, "is immune due to level");
            }
        }

        [HarmonyPatch(typeof(Il2Cpp.MusicJukeboxHandler), nameof(Il2Cpp.MusicJukeboxHandler.OnGameStarted))]
        private static class ApplyPatchOnGameStarted
        {
            public static void Postfix(Il2Cpp.MusicJukeboxHandler __instance, Il2Cpp.GameMode __0)
            {
                try
                {
                    // TODO: Begin timer to track AFK players every 30 seconds
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run OnGameStarted");
                }
            }
        }
    }
}