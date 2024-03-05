/*
Silica Eject Command
Copyright (C) 2023 by databomb

* Description *
Provides an admin command to eject a player from their current 
vehicle.

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

using MelonLoader;
using Si_Eject;
using System;
using SilicaAdminMod;

[assembly: MelonInfo(typeof(Eject), "Eject Command", "1.0.3", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_Eject
{
    public class Eject : MelonMod
    {
        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback ejectCallback = Command_Eject;
            HelperMethods.RegisterAdminCommand("eject", ejectCallback, Power.Eject);
        }
        public static void Command_Eject(Player callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];
            
            // validate argument count
            int argumentCount = args.Split(' ').Length - 1;
            if (argumentCount > 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too many arguments");
                return;
            }
            else if (argumentCount < 1)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Too few arguments");
                return;
            }

            // validate argument contents
            String sTarget = args.Split(' ')[1];
            Player? playerToEject = HelperMethods.FindTargetPlayer(sTarget);

            if (playerToEject == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Ambiguous or invalid target");
                return;
            }

            if (!callerPlayer.CanAdminTarget(playerToEject))
            {
                HelperMethods.ReplyToCommand_Player(playerToEject, "is immune due to level");
                return;
            }

            Unit unitOfPlayer = playerToEject.ControlledUnit;
            if (unitOfPlayer == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Player does not have a vehicle");
                return;
            }

            UnitCompartment compartmentToEject = unitOfPlayer.DriverCompartment;
            if (compartmentToEject == null)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Player is not inside the vehicle");
                return;
            }

            if (!compartmentToEject.IsDriver)
            {
                HelperMethods.SendChatMessageToPlayer(callerPlayer, HelperMethods.chatPrefix, commandName, ": Player is not driving the vehicle");
                return;
            }    

            // the DriverUnit is the original Unit that should be spawned when exiting the vehicle
            Unit driverUnit = compartmentToEject.DriverUnit;
            compartmentToEject.RequestRemoveUnit(driverUnit);

            MelonLogger.Msg("Ejecting player (" + playerToEject.PlayerName + ") from vehicle: " + unitOfPlayer.ToString());

            HelperMethods.AlertAdminActivity(callerPlayer, playerToEject, "ejected");
        }
    }
}