using System;
using System.Windows;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WindowsInput.Native;
using WindowsInput;
using WiiTUIO.Provider;
using System.Windows.Threading;
using System.Diagnostics;
using WiiTUIO.Filters;
using WiiTUIO.Properties;
using WiiTUIO.DeviceUtils;

namespace WiiTUIO.Output.Handlers
{
    internal class WiimoteHandler : IButtonHandler, IRumbleFeedback
    {
        public Action<byte, byte> OnRumble { get; set; }

        private enum RumbleState
        {
            none,
            rumbleshort,
            rumblelong,
            rumblehold,
            rumblealt
        }

        private bool rumbleState = false;
        private bool prevRumbleState = false;
        private RumbleState currRumbleState = RumbleState.none;
        private long prevRumbleTime;

        public WiimoteHandler()
        {

        }

        public bool connect()
        {
            return true;
        }

        public bool disconnect()
        {
            return true;
        }

        public bool reset()
        {
            return true;
        }

        public bool setButtonDown(string key)
        {
            if (Enum.TryParse(key, true, out RumbleState state))
            {
                rumbleState = true;
                prevRumbleTime = Stopwatch.GetTimestamp();
                currRumbleState = state;

                return true;
            }

            return false;
        }

        public bool setButtonUp(string key)
        {
            if (key.Equals("rumblehold") || key.Equals("rumblealt"))
            {
                rumbleState = false;
                currRumbleState = RumbleState.none;

                return true;
            }

            return false;
        }

        public bool startUpdate()
        {
            return true;
        }

        public bool endUpdate()
        {
            long currentTime = Stopwatch.GetTimestamp();
            double elapsedMs = (currentTime - prevRumbleTime) * (1000.0 / Stopwatch.Frequency);

            switch (currRumbleState)
            {
                case RumbleState.rumbleshort when elapsedMs >= Settings.Default.wiimode_rumbleTime_short:
                case RumbleState.rumblelong when elapsedMs >= Settings.Default.wiimode_rumbleTime_long:
                    rumbleState = false;
                    currRumbleState = RumbleState.none;
                    break;
                case RumbleState.rumblealt when (rumbleState && elapsedMs >= Settings.Default.wiimode_rumbleTime_alternatingOn) || (!rumbleState && elapsedMs >= Settings.Default.wiimode_rumbleTime_alternatingOff):
                    rumbleState = !rumbleState;
                    prevRumbleTime = Stopwatch.GetTimestamp();
                    break;
                default:
                    break;
            }

            if (rumbleState != prevRumbleState)
            {
                OnRumble?.Invoke((byte)(rumbleState ? 255 : 0), 0);
                prevRumbleState = rumbleState;
            }

            return true;
        }
    }
}