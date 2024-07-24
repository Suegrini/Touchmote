using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;

//using Hardcodet.Wpf.TaskbarNotification;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows.Interop;
using System.Windows.Media;
using System.IO.Pipes;
using System.IO;

namespace WiiTUIO
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// The tray's taskbar icon
        /// </summary>
       // public static TaskbarIcon TB { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            Process thisProc = Process.GetCurrentProcess();
            if (e.Args.Length != 0)
            {
                switch (e.Args[0])
                {
                    case "-exit":
                        if (isAnotherInstanceRunning(thisProc))
                            sendCommand("exit");
                        break;
                    default:
                        Console.WriteLine("Invalid argument");
                        break;
                }
                Application.Current.Shutdown(220);
            }
            else if (isAnotherInstanceRunning(thisProc))
            {
                MessageBox.Show("Touchmote is already running. Look for it in the taskbar.");
                Application.Current.Shutdown(220);
            }

            // Initialise the Tray Icon
            //TB = (TaskbarIcon)FindResource("tbNotifyIcon");
            //TB.ShowBalloonTip("Touchmote is running", "Click here to set it up", BalloonIcon.Info);

            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
            Application.Current.Exit += appWillExit;


            base.OnStartup(e);
        }

        private void appWillExit(object sender, ExitEventArgs e)
        {
            if (e.ApplicationExitCode != 220)
            {
                WiiTUIO.Properties.Settings.Default.Save();
                //TB.Dispose();
                SystemProcessMonitor.Default.Dispose();
            }
        }


        private void mnuExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown(0);
        }
        /*
        private void TaskbarIcon_TrayBalloonTipClicked_1(object sender, RoutedEventArgs e)
        {
            TB.ShowTrayPopup();
        }
         * */

        private bool isAnotherInstanceRunning(Process thisProcess)
        {
            return Process.GetProcessesByName(thisProcess.ProcessName).Length > 1;
        }

        private void sendCommand(string command, string value = null)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", "Touchmote", PipeDirection.Out))
                {
                    client.Connect(500);
                    using (var writer = new StreamWriter(client) { AutoFlush = true })
                    {
                        if (value != null)
                            writer.Write($"{command} {value}");
                        else
                            writer.Write(command);
                    }
                    client.Close();
                }
            }
            catch { }
        }
    }
}

