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

using MelonLoader;
using System;
using System.IO;
using System.Text;

namespace SilicaAdminMod
{
    public class WaveReader
    {
		public const int WAVE_FORMAT_PCM		= 1;
		public const int WAVE_FORMAT_IEEE_FLOAT = 3;

		private float[] _samples = null!;

		public int Frequency
        {
            get; 
            set; 
        }

        public int Channels
        {
            get;
            set;
        }

		public int BitsPerSample
		{
			get;
			set;
		}
		
		public int AudioFormat
		{
			get;
			set;
		}

        public float[] Samples 
        {
            get => _samples;
            set => _samples = value ?? throw new ArgumentNullException("Samples are required.");
		}

        public static WaveReader? Load(string filePath)
        {
			try
			{
				byte[] fileData = File.ReadAllBytes(filePath);

				// check file header https://en.wikipedia.org/wiki/WAV#WAV_file_header
				if (Encoding.ASCII.GetString(fileData, 0, 4) != "RIFF")
				{
					throw new Exception("Invalid .WAV file. Missing 'RIFF'.");
				}

				if (Encoding.ASCII.GetString(fileData, 8, 4) != "WAVE")
				{
					throw new Exception("Invalid .WAV file. Missing 'WAVE'.");
				}

				int bytePosition = 12;
				int channels = 0;
				int frequency = 0;
				int bitsPerSample = 0;
				int audioFormat = 0;
				byte[]? dataChunk = null;

				// find "fmt " & "data" chunks
				while (bytePosition < fileData.Length)
				{
					string chunkName = Encoding.ASCII.GetString(fileData, bytePosition, 4);
					bytePosition += 4;
					// chunkSize doubles as DataSize in data block and BlocSize for fmt  chunk
					int chunkSize = BitConverter.ToInt32(fileData, bytePosition);
					bytePosition += 4;

					if (chunkName == "fmt ")
					{
						// bytePosition goes from start of audioFormat after chunk size
						audioFormat = BitConverter.ToInt16(fileData, bytePosition);
						channels = BitConverter.ToInt16(fileData, bytePosition + 2);
						frequency = BitConverter.ToInt32(fileData, bytePosition + 4);
						bitsPerSample = BitConverter.ToInt16(fileData, bytePosition + 14);
						bytePosition += chunkSize;
					}
					else if (chunkName == "data")
					{
						dataChunk = new byte[chunkSize];
						Array.Copy(fileData, bytePosition, dataChunk, 0, chunkSize);

						if (SiAdminMod.Pref_Admin_DebugLogMessages.Value)
						{
							MelonLogger.Msg("Found .WAV data region with size: " + chunkSize);
						}

						break;
					}
					else
					{
						bytePosition += chunkSize;
					}
				}

				if (dataChunk == null)
				{
					throw new Exception("Invalid .WAV file. Missing 'DATA' section.");
				}

				int bytesPerSample = bitsPerSample / 8;
				int sampleCount = dataChunk.Length / bytesPerSample;
				float[] samples = new float[sampleCount];

				for (int i = 0; i < sampleCount; i++)
				{
					int offset = i * bytesPerSample;

					// Audio File Format Specifications:
					// https://mmsp.ece.mcgill.ca/Documents/AudioFormats/WAVE/WAVE.html
					switch (audioFormat)
					{
						case WAVE_FORMAT_PCM: 
						{
							switch (bitsPerSample)
							{
								case 8:
								{
									samples[i] = (dataChunk[offset] - 128) / 128f;
									break;
								}

								case 16:
								{
									short s16 = BitConverter.ToInt16(dataChunk, offset);
									samples[i] = (s16 -32768f) / 32768f;
									break;
								}

								default:
								{
									throw new Exception(bitsPerSample.ToString() + " is an unsupported PCM bit depth. Use 8 or 16.");
								}
							}
							break;
						}

						case WAVE_FORMAT_IEEE_FLOAT:
						{
							if (bitsPerSample != 32)
							{
								throw new Exception("Unsupported float format using bits-per-sample of: " + bitsPerSample.ToString());
							}
								
							samples[i] = BitConverter.ToSingle(dataChunk, offset);
							break;
						}

						default:
						{
							throw new Exception("Unsupported WAV encoding format of: " + audioFormat.ToString());
						}
					}
				}

				return new WaveReader
				{
					Frequency = frequency,
					Channels = channels,
					BitsPerSample = bitsPerSample,
					AudioFormat = audioFormat,
					Samples = samples
				};
			}
			catch (Exception error)
			{
				HelperMethods.PrintError(error, "Error processing .WAV file: " + filePath);
			}

			return null;
		}
    }
}