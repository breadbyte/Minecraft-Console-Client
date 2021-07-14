using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MinecraftClient.Commands
{
    public class Debug : Command
    {
        public override string CmdName { get { return "debug"; } }
        public override string CmdUsage { get { return "debug [on|off]"; } }
        public override string CmdDesc { get { return "cmd.debug.desc"; } }

        public override string Run(Settings settings, McClient handler, string command,
            Dictionary<string, object> localVars)
        {
            if (hasArg(command))
            {
                settings.DebugMessages = (getArg(command).ToLower() == "on");
            }
            else settings.DebugMessages = !settings.DebugMessages;
            return Translations.Get(settings.DebugMessages ? "cmd.debug.state_on" : "cmd.debug.state_off");
        }
    }
}
