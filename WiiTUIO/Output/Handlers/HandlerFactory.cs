using Nefarius.ViGEm.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WiiTUIO.Output;
using WiiTUIO.Output.Handlers.Xinput;
using VMultiDllWrapper;

namespace WiiTUIO.Output.Handlers
{
    public class HandlerFactory
    {
        private Dictionary<long, List<IOutputHandler>> outputHandlers;

        public HandlerFactory()
        {
            outputHandlers = new Dictionary<long, List<IOutputHandler>>();
        }

        private List<IOutputHandler> createOutputHandlers(long id)
        {
            VMulti vmulti = new VMulti();
            List<IOutputHandler> all = new List<IOutputHandler>();
            IOutputHandler keyboardHandler = new KeyboardHandler();
            if (vmulti.connect((int)id))
            {
                keyboardHandler = new VmultiKeyboardHandler(vmulti);
                all.Add(new VmultiMouseHandler(vmulti));
            }
            all.Add(keyboardHandler);
            all.Add(new MouseHandler());
            ViGEmHandler gamepadHandler = new ViGEmHandler(id);
            if (gamepadHandler.isAvailable) all.Add(gamepadHandler);
            return all;
        }

        public List<IOutputHandler> getOutputHandlers(long id)
        {
            List<IOutputHandler> handlerList;
            if (outputHandlers.TryGetValue(id, out handlerList))
            {
                return handlerList;
            }
            else
            {
                handlerList = this.createOutputHandlers(id);
                return handlerList;
            }
        }

    }
}
