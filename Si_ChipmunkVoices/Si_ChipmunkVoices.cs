/*
Silica Chipmunk Voices
Copyright (C) 2026 by databomb

* Description *
For Silica servers, changes player voices to sound like chipmunks 
before relaying the voice stream to other clients.

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

using HarmonyLib;
using MelonLoader;
using SilicaAdminMod;
using System;
using Si_ChipmunkVoices;

[assembly: MelonInfo(typeof(ChipmunkVoices), "Chipmunk Voices", "0.9.1", "databomb", "https://github.com/data-bomb/Silica")]
[assembly: MelonGame("Bohemia Interactive", "Silica")]
[assembly: MelonOptionalDependencies("Admin Mod")]

namespace Si_ChipmunkVoices
{
    public class ChipmunkVoices : MelonMod
    {
        static bool chipmunkMode = ShouldHaveChipmunks();

        public override void OnLateInitializeMelon()
        {
            HelperMethods.CommandCallback chipmunkCallback = Command_Chipmunk;
            HelperMethods.RegisterAdminCommand("chipmunk", chipmunkCallback, Power.Slay, "Toggles chipmunk voices. Usage: !chipmunk");
        }

        public static bool ShouldHaveChipmunks()
        {
            DateTime currentDateTime = DateTime.Today;
            if (currentDateTime.Month == 4)
            {
                if (currentDateTime.Day == 1)
                {
                    return true;
                }
            }

            return false;
        }

        public static void Command_Chipmunk(Player? callerPlayer, String args)
        {
            string commandName = args.Split(' ')[0];

            chipmunkMode = !chipmunkMode;
            HelperMethods.ReplyToCommand(commandName + ": Chipmunk mode " + (chipmunkMode ? "ENABLED" : "DISABLED"));
        }

        [HarmonyPatch(typeof(NetworkLayer), nameof(NetworkLayer.RelayVoiceStreamPacket))]
        public class ApplyPatch_NetworkLayer_RelayVoiceStreamPacket
        {
            private static void Prefix(ref byte[] __0, uint __1, ENetworkPacketSend __2, Player __3, bool __4)
            {
                try
                {
                    if (!chipmunkMode || __0 == null)
                    {
                        return;
                    }

                    // artificially speed up by skipping every other sample
                    const int skip = 2;

                    /* NetworkLayer bytes processed by client
                     *  [2] ProcessMessage networkPacketType [ReadType(Byte) + Byte]
                     *  [9] ProcessMessage(VoiceStream) networkID [ReadType(UInt64) + UInt64]
                     *  [2] ProcessMessage(VoiceStream) channel [ReadType(Byte) + Byte]
                     *  [2] ProcessMessage(VoiceStream) proximity [ReadType(Bool) + Byte]
                     *  [2] DecompressVoice silence [ReadType(Bool) + Byte]
                     *  [3] DecompressVoice offset [ReadType(UInt16) + UInt16]
                     *  [1] ReadType (ByteArray)
                     *  [2] ByteArray length
                     *  [N] ByteArray bytes
                     */

                    const int silenceOffset = 16;
                    const int byteArrayOffset = 23;
                    

                    // check if the silence flag is set
                    if (__0[silenceOffset] > 0)
                    {
                        return;
                    }

                    int writeIndex = byteArrayOffset;
                    for (int readIndex = writeIndex; readIndex < __1; readIndex += skip)
                    {
                        __0[writeIndex++] = __0[readIndex];
                    }

                    // fill remaining with silence
                    int lastIndex = writeIndex - 1;
                    while (writeIndex < __1)
                    {
                        __0[writeIndex++] = (byte)128;
                    }
                }
                catch (Exception error)
                {
                    HelperMethods.PrintError(error, "Failed to run NetworkLayer::RelayVoiceStreamPacket");
                }
            }
        }
    }
}