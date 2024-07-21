using System;
using System.Net.Sockets;
using System.Threading;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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

        public ArcadeHookMain()
        {
            Console.WriteLine("ArcadeHookMain started.");
            tcpClient = new TcpClient();
        }

        public void ConnectToServer()
        {
            Console.WriteLine("Waiting for the server to be available...");
            while (isRunning)
            {
                if (!tcpClient.Connected)
                {
                    gameName = null;
                    try
                    {
                        tcpClient.Connect(Hostname, Port);
                        Console.WriteLine("Connected to output server instance!");
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
                        Console.WriteLine($"Game: {gameName}");
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
                    Console.WriteLine($"Received key: {line.key} and received value {line.value}");
                    if (int.TryParse(line.value, out int intValue))
                        ProcessIniCommand(line.key, intValue);
                    if (line.key == "MameStop")
                    {
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
            string pattern = @"^wii [1-4] [1-5] (0|1|%s%)$";
            string iniCommand = IniFileHandler.ReadFromIniFile(gameName, key);
            if (!string.IsNullOrEmpty(iniCommand))
            {
                if (Regex.IsMatch(iniCommand, pattern, RegexOptions.Compiled))
                {
                    iniCommand = iniCommand.Replace("wii", "").Trim();
                    string[] readValues = iniCommand.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (int.TryParse(readValues[0], out int player) && int.TryParse(readValues[1], out int action))
                    {
                        string value = readValues[2];
                        ExecuteAction(recValue, player, action, value);
                    }
                }
                else
                    Console.WriteLine($"{iniCommand} Unsupported command");
            }
        }

        private void ExecuteAction(int recValue, int player, int action, string value)
        {
            int newValue;
            if (value != "%s%")
            {
                if (!int.TryParse(value, out newValue))
                    return;
            }
            else
                newValue = recValue;

            if (player >= 1 && player <= 4)
            {
                if (action == 5)
                    OnExecute?.Invoke("rumble", newValue, player);
                else if (action >= 1 && action <= 4)
                    OnExecute?.Invoke("LED", newValue, player);
                else if (action == 0)
                {
                    OnExecute?.Invoke("LED", recValue / 4 * newValue, player);
                }
            }
        }

        private void GameEnded()
        {
            for (int i = 1; i < 5; i++)
                OnExecute?.Invoke("MameStop", 0, i);
            gameName = null;
            Console.WriteLine("Game ended");
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
