using System;

namespace WiiTUIO.ArcadeHook
{
    public class OutputProvider
    {
        private ArcadeHookMain arcadeHook;
        public Action<string, int> OnOutput;
        int wiiMoteID;

        public OutputProvider(int id)
        {
            this.arcadeHook = ArcadeHookSingleton.Default;
            this.wiiMoteID = id;
            this.arcadeHook.OnExecute += SendOutput;
        }

        public void SendOutput(string key, int value, int player)
        {
            if (player == this.wiiMoteID)
                OnOutput?.Invoke(key, value);
        }
    }
}
