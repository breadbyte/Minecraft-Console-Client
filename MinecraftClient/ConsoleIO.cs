using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MinecraftClient.ConsoleGui;

namespace MinecraftClient
{
    /// <summary>
    /// Allows simultaneous console input and output without breaking user input
    /// (Without having this annoying behaviour : User inp[Some Console output]ut)
    /// Provide some fancy features such as formatted output, text pasting and tab-completion.
    /// By ORelio - (c) 2012-2018 - Available under the CDDL-1.0 license
    /// </summary>
    public static class ConsoleIO
    {
        private static IAutoComplete autocomplete_engine;

        /// <summary>
        /// Set an auto-completion engine for TAB autocompletion.
        /// </summary>
        /// <param name="engine">Engine implementing the IAutoComplete interface</param>
        public static void SetAutoCompleteEngine(IAutoComplete engine) {
            autocomplete_engine = engine;
        }
        
        internal static void DoAutoComplete(string buffer) {
            if (autocomplete_engine == null)
                return;
            
            autocomplete_engine.AutoComplete(buffer);
        }

        /// <summary>
        /// Determines whether to use interactive IO or basic IO.
        /// Set to true to disable interactive command prompt and use the default Console.Read|Write() methods.
        /// Color codes are printed as is when BasicIO is enabled.
        /// </summary>
        public static bool BasicIO = false;

        /// <summary>
        /// Determines whether not to print color codes in BasicIO mode.
        /// </summary>
        public static bool BasicIO_NoColor = false;

        /// <summary>
        /// Determine whether WriteLineFormatted() should prepend lines with timestamps by default.
        /// </summary>
        public static bool EnableTimestamps = false;

        /// <summary>
        /// Specify a generic log line prefix for WriteLogLine()
        /// </summary>
        public static string LogPrefix = "§8[Log] ";
        
        /// <summary>
        /// Read a line from the standard input
        /// </summary>
        public static string ReadLine()
        {
            if (BasicIO)
            {
                return Console.ReadLine();
            }
            else {
                return ConsoleHandler.WaitForInput();
            }
        }

        /// <summary>
        /// Write a string to the standard output with a trailing newline
        /// </summary>
        public static void WriteLine(string line)
        {
            if (BasicIO) {
                Console.WriteLine(line);
            }
            else {
                ConsoleHandler.Instance.WriteLine(line);
            }
        }

        /// <summary>
        /// Write a Minecraft-Like formatted string to the standard output, using §c color codes
        /// See minecraft.gamepedia.com/Classic_server_protocol#Color_Codes for more info
        /// </summary>
        /// <param name="str">String to write</param>
        /// <param name="acceptnewlines">If false, space are printed instead of newlines</param>
        /// <param name="displayTimestamp">
        /// If false, no timestamp is prepended.
        /// If true, "hh-mm-ss" timestamp will be prepended.
        /// If unspecified, value is retrieved from EnableTimestamps.
        /// </param>
        public static void WriteLineFormatted(string str, bool acceptnewlines = true, bool displayTimestamp = false) {
            if (!acceptnewlines) {
                str = str.Replace('\n', ' ');
            }

            if (BasicIO) {
                if (displayTimestamp) {
                    Console.Write($"{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00}");
                }

                if (BasicIO_NoColor) {
                    str = ChatBot.GetVerbatim(str);
                }
                else {
                    Console.WriteLine(str);
                }
            }
            else
                ConsoleHandler.Instance!.WriteLine(displayTimestamp ? $"{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00} {str}" : str);
        }

        /// <summary>
        /// Write a prefixed log line. Prefix is set in LogPrefix.
        /// </summary>
        /// <param name="text">Text of the log line</param>
        /// <param name="acceptnewlines">Allow line breaks</param>
        public static void WriteLogLine(string text, bool acceptnewlines = true)
        {
            if (!acceptnewlines)
                text = text.Replace('\n', ' ');
            WriteLineFormatted(LogPrefix + text);
        }
    }

    /// <summary>
    /// Interface for TAB autocompletion
    /// Allows to use any object which has an AutoComplete() method using the IAutocomplete interface
    /// </summary>
    public interface IAutoComplete
    {
        /// <summary>
        /// Provide a list of auto-complete strings based on the provided input behing the cursor
        /// </summary>
        /// <param name="BehindCursor">Text behind the cursor, e.g. "my input comm"</param>
        /// <returns>List of auto-complete words, e.g. ["command", "comment"]</returns>
        IEnumerable<string> AutoComplete(string BehindCursor);
    }
}
