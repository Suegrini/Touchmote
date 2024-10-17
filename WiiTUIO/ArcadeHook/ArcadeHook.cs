using System;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace WiiTUIO.ArcadeHook
{
    public class ArcadeHookMain
    {
        private TcpClient tcpClient;
        private NetworkStream stream;
        private const string Hostname = "localhost";
        private const int Port = 8000;
        private const int RetryDelayMs = 1000;
        private string gameName;
        private bool isRunning = true;
        public event Action<string, int, int> OnExecute;
        string pattern = @"^wii [1-4] [0-5] (?:\d+|%s%)$";

        public ArcadeHookMain()
        {
            Debug.WriteLine("ArcadeHookMain started.");
            tcpClient = new TcpClient();
        }

        public void ConnectToServer()
        {
            Debug.WriteLine("Waiting for the server to be available...");
            while (isRunning)
            {
                if (!tcpClient.Connected)
                {
                    if (gameName != null)
                        GameEnded();

                    try
                    {
                        tcpClient.Connect(Hostname, Port);
                        Debug.WriteLine("Connected to output server instance!");
                        stream = tcpClient.GetStream();
                    }
                    catch
                    {
                        Thread.Sleep(RetryDelayMs);
                    }
                }
                else
                    ReadData();
            }
        }

        private void ReadData()
        {
            if (gameName == null || gameName == "___empty")
                InitializeGame();
            else
                ProcessValues();
        }

        private void InitializeGame()
        {
            List<(string key, string value)> valueList = ReadFromServer();
            if (valueList != null)
            {
                foreach (var line in valueList)
                {
                    if (line.key == "MameStart")
                    {
                        gameName = line.value;
                        ProcessIniCommand(line.key, 1);
                        Debug.WriteLine($"Game: {gameName}");
                        break;
                    }
                }
            }
        }

        private void ProcessValues()
        {
            List<(string key, string value)> valueList = ReadFromServer();
            if (valueList != null)
            {
                foreach (var line in valueList)
                {
                    Debug.WriteLine($"Received key: {line.key} and received value {line.value}");
                    if (int.TryParse(line.value, out int intValue))
                        ProcessIniCommand(line.key, intValue);
                    if (line.key == "MameStop")
                    {
                        ProcessIniCommand(line.key, 1);
                        GameEnded();
                        break;
                    }
                }
            }
        }

        private List<(string key, string value)> ReadFromServer()
        {
            List<(string key, string value)> keyValuePairsList = new List<(string key, string value)>();
            try
            {
                stream.ReadTimeout = RetryDelayMs;
                byte[] buffer = new byte[1024];
                StringBuilder messageBuilder = new StringBuilder();
                string receivedData;

                do
                {
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    receivedData = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    messageBuilder.Append(receivedData);
                }
                while (!receivedData.EndsWith("\r"));

                string[] lines = messageBuilder.ToString().Split(new[] { "\r" }, StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    string[] keyValue = line.Split(new[] { " = " }, StringSplitOptions.RemoveEmptyEntries);
                    if (keyValue.Length == 2)
                    {
                        switch (keyValue[0])
                        {
                            case "mame_stop":
                                keyValue[0] = "MameStop";
                                break;
                            case "mame_start":
                                keyValue[0] = "MameStart";
                                break;
                            case "mame_pause":
                                keyValue[0] = "OnPause";
                                break;
                        }
                        keyValuePairsList.Add((key: keyValue[0], value: keyValue[1]));
                    }
                }
            }
            catch
            {
                return new List<(string key, string value)>();
            }
            return keyValuePairsList;
        }

        private void ProcessIniCommand(string key, int recValue)
        {
            string iniCommands = IniFileHandler.ReadFromIniFile(gameName, key);
            if (!string.IsNullOrEmpty(iniCommands))
            {
                string[] commands = iniCommands.Split('&');
                foreach (string command in commands)
                {
                    string trimmedCommand = command.Trim();
                    if (Regex.IsMatch(trimmedCommand, pattern, RegexOptions.Compiled))
                    {
                        string[] readValues = trimmedCommand.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                        if (int.TryParse(readValues[1], out int player))
                        {
                            string device = readValues[0];
                            string action = readValues[2];
                            int value = readValues[3] == "%s%" ? recValue : int.Parse(readValues[3]);
                            ExecuteAction(device, player, action, value, recValue);
                        }
                    }
                }
            }
        }

        private void ExecuteAction(string device, int player, string action, int value, int recValue)
        {
            if (player >= 1 && player <= 4)
            {
                switch (device)
                {
                    case "wii":
                        switch (action)
                        {
                            case "0":
                                double fillResult = (double)recValue / value * 4;
                                OnExecute?.Invoke("LEDFill", (int)Math.Round(fillResult), player);
                                break;
                            case "1":
                            case "2":
                            case "3":
                            case "4":
                                OnExecute?.Invoke("LED", int.Parse(action), player);
                                break;
                            case "5":
                                OnExecute?.Invoke("Rumble", value, player);
                                break;
                        }
                        break;
                }
            }
        }

        private void GameEnded()
        {
            for (int i = 1; i < 5; i++)
                OnExecute?.Invoke("MameStop", 0, i);
            gameName = null;
            Debug.WriteLine("Game ended");
            tcpClient.Close();
            tcpClient.Dispose();
            tcpClient = new TcpClient();
        }

        public void Stop()
        {
            isRunning = false;
            tcpClient.Close();
            tcpClient.Dispose();
        }
    }
}
