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
using MelonLoader.Utils;

namespace SilicaAdminMod
{ 
    public class AudioHelper
    { 
        private static GameByteStreamWriter GenerateAudioStreamWriter(Player player, byte[] audioBytes, int offset)
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

		public static void SendAudioFile(string audioFilePath, Player? sendingPlayer = null)
		{
			string path = System.IO.Path.Combine(MelonEnvironment.UserDataDirectory, audioFilePath);
			
			if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
			{
				MelonLogger.Msg("Trying to load audio file: " + path);
			}

			// Load the WAV file
			var wav = WaveReader.Load(path);
			if (wav == null)
			{
				MelonLogger.Warning("Could not load wave file: " + path);
				return;
			}

			int sampleRate = wav.Frequency;
			int audioFormat = wav.AudioFormat;
			int bitsPerSample = wav.BitsPerSample;
			int channels = wav.Channels;
			float[] samples = wav.Samples;

			// Open WAV file and load all samples
			int sampleCount = samples.Length;

			if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
			{
				MelonLogger.Msg($"Loaded wave with frequency: {sampleRate} samples: {sampleCount} channels: {channels} format: {audioFormat} bps: {bitsPerSample}");
			}

			// Stream in 1024-sample blocks
			const int blockSize = 1024;
			int offset = 0;
			int packetsSent = 0;

			while (offset < samples.Length)
			{
				int count = Math.Min(blockSize, samples.Length - offset);

				// Copy block to buffer
				float[] buffer = new float[blockSize]; // always full size for packet
				Array.Clear(buffer, 0, blockSize);     // zero-pad last block if needed
				Array.Copy(samples, offset, buffer, 0, count);

				// Convert float [-1..1] to 8-bit unsigned PCM (0..255)
				byte[] packetBuffer = new byte[blockSize];
				for (int i = 0; i < blockSize; i++)
				{
					float normalized = buffer[i];
					byte b = 128; // silence
					if (normalized > 0.01f)
						b = (byte)Mathf.CeilToInt(normalized * 128f + 128f);
					else if (normalized < -0.01f)
						b = (byte)Mathf.FloorToInt(normalized * 128f + 128f);
					packetBuffer[i] = b;
				}

				// Send packet
				int startBufferOffset = offset + 1;
				AudioHelper.SendAudioPacket(packetBuffer, startBufferOffset);

				if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
				{
					MelonLogger.Msg("Sending audio packet with offset: " + startBufferOffset);
				}
				

				packetsSent++;
				offset += count;
			}
			if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
			{
				MelonLogger.Msg("Sent " + packetsSent.ToString() + " audio packets.");
			}
		}

		public static void SendAudioPacketToPlayer(Player targetPlayer, byte[] audioBytes, int offset, Player? originPlayer = null)
		{
			GameByteStreamWriter gameByteStreamWriter;
			if (originPlayer != null)
			{
				gameByteStreamWriter = GenerateAudioStreamWriter(originPlayer, audioBytes, offset);
			}
			else
			{
				gameByteStreamWriter = GenerateAudioStreamWriter(targetPlayer, audioBytes, offset);
			}
			
			uint byteDataSize = (uint)gameByteStreamWriter.GetByteDataSize();

			#if NET6_0
			NetworkLayer.NetBitsSent += byteDataSize * 8U;
			#else
			FieldInfo netBitsSentField = typeof(NetworkLayer).GetField("NetBitsSent", BindingFlags.NonPublic | BindingFlags.Static);
			uint bitsSent = (uint)netBitsSentField.GetValue(null);
			bitsSent += byteDataSize * 8U;
			netBitsSentField.SetValue(null, bitsSent);
			#endif

			#if NET6_0
			NetworkLayer.SendServerPacket(targetPlayer.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, targetPlayer.PlayerChannel);
			#else
			Type networkLayerType = typeof(NetworkLayer);
			MethodInfo sendServerPacketMethod = networkLayerType.GetMethod("SendServerPacket", BindingFlags.NonPublic | BindingFlags.Static);
			sendServerPacketMethod.Invoke(null, new object[] { targetPlayer.PlayerID, gameByteStreamWriter.GetByteData(), byteDataSize, ENetworkPacketSend.Reliable, targetPlayer.PlayerChannel });
			#endif
		}

		public static void SendAudioPacket(byte[] audioBytes, int offset, Player? originPlayer = null)
		{
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

				SendAudioPacketToPlayer(player, audioBytes, offset, originPlayer);
			}
		}
    }
}