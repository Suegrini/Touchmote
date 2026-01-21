using System;
using System.Collections.Generic;
using System.IO;

namespace WiiTUIO.DeviceUtils
{
    internal class AudioUtil
    {
        private static readonly string[] audioExtensions = { ".wav", ".mp3", ".aac", ".ogg", ".flac" };

        public static bool IsValid(string fileName, out long headerSize)
        {
            headerSize = 0;

            string baseFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", fileName);

            foreach (string extension in audioExtensions)
            {
                string filePath = baseFilePath + extension;

                if (File.Exists(filePath))
                {
                    if (extension == ".wav" && IsValidFormat(filePath, out long size))
                    {
                        headerSize = size;
                        return true;
                    }

                    if (ConvertToYamahaADPCM(baseFilePath, extension))
                    {
                        string convertedPath = baseFilePath + ".wav";
                        return IsValidFormat(convertedPath, out headerSize); // re-validate converted file
                    }

                    return false;
                }
            }
            return false;
        }

        private static bool IsValidFormat(string filePath, out long headerSize)
        {
            headerSize = 0;

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read)) using (BinaryReader reader = new BinaryReader(fs))
            { 
                byte[] riff = reader.ReadBytes(4);
                if (System.Text.Encoding.ASCII.GetString(riff) != "RIFF")
                    return false;
                reader.ReadInt32();
                
                byte[] wave = reader.ReadBytes(4);
                if (System.Text.Encoding.ASCII.GetString(wave) != "WAVE")
                    return false;

                bool fmtValid = false;

                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string chunkId = new string(reader.ReadChars(4));
                    int chunkSize = reader.ReadInt32();

                    if (chunkId == "fmt ")
                    {
                        short formatCode = reader.ReadInt16();
                        short channels = reader.ReadInt16();
                        int sampleRate = reader.ReadInt32();
                        reader.ReadBytes(6);
                        short bitsPerSample = reader.ReadInt16();

                        fmtValid = (formatCode == 0x0020
                            && channels == 1
                            && sampleRate == 6000 //6000 since its the only sample rate that works
                            && bitsPerSample == 4);

                        int remaining = chunkSize - 16;

                        if (remaining > 0)
                            reader.BaseStream.Seek(remaining, SeekOrigin.Current);
                    }
                    else if (chunkId == "data")
                    {
                        headerSize = reader.BaseStream.Position;
                        break;
                    }
                    else
                    {
                        reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                        if (chunkSize % 2 == 1)
                            reader.BaseStream.Seek(1, SeekOrigin.Current);
                    }
                }
                return fmtValid && headerSize > 0;
            }
        }

        private static bool ConvertToYamahaADPCM(string baseFilePath, string extension) // Use ffmpeg to convert to valid audio file
        {
            string filePath = baseFilePath + extension;
            string outputPath = baseFilePath + ".wav";
            string bakPath = null;

            if (extension == ".wav")
            {
                bakPath = filePath + ".bak";

                if (File.Exists(bakPath))
                    File.Delete(bakPath);

                File.Move(filePath, bakPath);
                filePath = bakPath;
            }

            if (!Launcher.Launch(null, "ffmpeg", $"-i \"{filePath}\" -ar 6000 -ac 1 -c:a adpcm_yamaha \"{outputPath}\" -hide_banner", null)) //6000 since its the only sample rate that works
            {
                if (bakPath != null && File.Exists(bakPath))
                    File.Move(bakPath, baseFilePath + extension);

                return false;
            }

            bool success = File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;

            if (success && bakPath != null && File.Exists(bakPath))
                File.Delete(bakPath);

            return success;
        }
    }
}
