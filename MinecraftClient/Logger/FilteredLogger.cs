using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MinecraftClient.Logger
{
    public class FilteredLogger : LoggerBase {
        private string? ChatFilterRegex;
        private string? DebugFilterRegex;
        private Settings.FilterModeEnum? FilterMode;
        public FilteredLogger() { }
        public FilteredLogger(Settings.FilterModeEnum filterMode, string? chatFilterRegex, string? debugFilterRegex) {
            FilterMode = filterMode;
            ChatFilterRegex = chatFilterRegex;
            DebugFilterRegex = debugFilterRegex;
        }

        protected enum FilterChannel { Debug, Chat }

        protected bool ShouldDisplay(FilterChannel channel, string msg) {
            if (FilterMode == null)
                return true;
            
            Regex? regexToUse = null;
            // Convert to bool for XOR later. Whitelist = 0, Blacklist = 1
            bool filterMode = FilterMode == Settings.FilterModeEnum.Blacklist ? true : false;
            switch (channel)
            {
                case FilterChannel.Chat:
                    regexToUse = ChatFilterRegex != null ? new Regex(ChatFilterRegex) : null; break;
                case FilterChannel.Debug: 
                    regexToUse = DebugFilterRegex != null ? new Regex(DebugFilterRegex) : null; break;
            }
            if (regexToUse != null)
            {
                // IsMatch and white/blacklist result can be represented using XOR
                // e.g.  matched(true) ^ blacklist(true) => shouldn't log(false)
                return regexToUse.IsMatch(msg) ^ filterMode;
            }
            
            return true;
        }

        public override void Debug(string msg)
        {
            if (DebugEnabled)
            {
                if (ShouldDisplay(FilterChannel.Debug, msg))
                {
                    Log("§8[DEBUG] " + msg);
                }
                // Don't write debug lines here as it could cause a stack overflow
            }
        }

        public override void Info(string msg)
        {
            if (InfoEnabled)
                ConsoleIO.WriteLogLine(msg);
        }

        public override void Warn(string msg)
        {
            if (WarnEnabled)
                Log("§6[WARN] " + msg);
        }

        public override void Error(string msg)
        {
            if (ErrorEnabled)
                Log("§c[ERROR] " + msg);
        }

        public override void Chat(string msg)
        {
            if (ChatEnabled)
            {
                if (ShouldDisplay(FilterChannel.Chat, msg))
                {
                    Log(msg);
                }
                else Debug("[Logger] One Chat message filtered: " + msg);
            }
        }
    }
}
