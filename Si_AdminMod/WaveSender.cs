/*
Silica Admin Mod
Copyright (C) 2026 by databomb

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
#else
using System.Reflection;
#endif

using MelonLoader;
using System;
using System.Text;
using UnityEngine;

namespace SilicaAdminMod
{ 
    public class AudioHelper
    { 
        public static GameByteStreamWriter GenerateAudioStreamWriter(Player player, byte[] audioBytes, int offset)
        {
			GameByteStreamWriter gameByteStreamWriter = GameByteStreamWriter.GetGameByteStreamWriter(0U, "NetworkLayer::SendCustomAudio", true);

			// what is processed by netcode
			gameByteStreamWriter.WriteByte((byte)ENetworkPacketType.VoiceStream);
			gameByteStreamWriter.WriteUInt64((ulong)player.PlayerID);
			gameByteStreamWriter.WriteByte((byte)player.PlayerChannel);
			gameByteStreamWriter.WriteBool(false); // proximity

			// what is processed by DecompressVoice
			gameByteStreamWriter.WriteBool(false); // proximity
			gameByteStreamWriter.WriteUInt16((ushort)offset);
			gameByteStreamWriter.WriteByteArray(audioBytes);

            return gameByteStreamWriter;
		}

        public static void SendAudioPacket(Player sendingPlayer, byte[] audioBytes, int offset)
        {
            GameByteStreamWriter gameByteStreamWriter = GenerateAudioStreamWriter(sendingPlayer, audioBytes, offset);

			uint byteDataSize = (uint)gameByteStreamWriter.GetByteDataSize();

			for (int i = 0; i < Player.Players.Count; i++)
			{
				Player player = Player.Players[i];
				if (player == null)
				{
					continue;
				}

				if (player == NetworkGameServer.GetServerPlayer())
				{
					continue;
				}

				#if NET6_0
				NetworkLayer.NetBitsSent += byteDataSize * 8U;
				#else
				FieldInfo netBitsSentField = typeof(NetworkLayer).GetField("NetBitsSent", BindingFlags.NonPublic | BindingFlags.Static);
				uint bitsSent = (uint)netBitsSentField.GetValue(null);
				bitsSent += byteDataSize * 8U;
				netBitsSentField.SetValue(null, bitsSent);
				#endif

				#if NET6_0
				NetworkLayer.SendServerPacket(player.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, player.PlayerChannel);
				#else
				Type networkLayerType = typeof(NetworkLayer);
				MethodInfo sendServerPacketMethod = networkLayerType.GetMethod("SendServerPacket", BindingFlags.NonPublic | BindingFlags.Static);
				sendServerPacketMethod.Invoke(null, new object[] { player.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, player.PlayerChannel });
				#endif
			}
		}
    }
}