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
        private static LinkedList<string> autocomplete_words = new LinkedList<string>();
        private static LinkedList<string> previous = new LinkedList<string>();
        private static string buffer = "";
        private static string buffer2 = "";

        /// <summary>
        /// Set an auto-completion engine for TAB autocompletion.
        /// </summary>
        /// <param name="engine">Engine implementing the IAutoComplete interface</param>
        public static void SetAutoCompleteEngine(IAutoComplete engine) {
            autocomplete_engine = engine;
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
        /// Read a password from the standard input
        /// </summary>
        public static string ReadPassword() {
            StringBuilder password = new StringBuilder();

            ConsoleKeyInfo k;
            while ((k = Console.ReadKey(true)).Key != ConsoleKey.Enter)
            {
                switch (k.Key)
                {
                    case ConsoleKey.Backspace:
                        if (password.Length > 0)
                        {
                            Console.Write("\b \b");
                            password.Remove(password.Length - 1, 1);
                        }
                        break;

                    case ConsoleKey.Escape:
                    case ConsoleKey.LeftArrow:
                    case ConsoleKey.RightArrow:
                    case ConsoleKey.Home:
                    case ConsoleKey.End:
                    case ConsoleKey.Delete:
                    case ConsoleKey.DownArrow:
                    case ConsoleKey.UpArrow:
                    case ConsoleKey.Tab:
                        break;

                    default:
                        if (k.KeyChar != 0)
                        {
                            Console.Write('*');
                            password.Append(k.KeyChar);
                        }
                        break;
                }
            }

            Console.WriteLine();
            return password.ToString();
        }

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
                throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Write a string to the standard output, without newline character
        /// </summary>
        public static void Write(string text)
        {
            // TODO FIXME: Write only! no newline! 
            if (BasicIO)
            {
                Console.Write(text);
            }
            else {
                ConsoleHandler.Instance.WriteLine(text);
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
        /// Write a single character to the standard output
        /// </summary>
        public static void Write(char c) {
            if (BasicIO) {
                Write("" + c);
            }
            else {
                throw new NotImplementedException();
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
                    ConsoleIO.Write($"{DateTime.Now.Hour:00}:{DateTime.Now.Minute:00}:{DateTime.Now.Second:00}");
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

        #region Subfunctions

        /// <summary>
        /// Clear all text inside the input prompt
        /// </summary>
        private static void ClearLineAndBuffer()
        {
            while (buffer2.Length > 0)
            {
                GoRight();
            }
            while (buffer.Length > 0)
            {
                RemoveOneChar();
            }
        }

        /// <summary>
        /// Remove one character on the left of the cursor in input prompt
        /// </summary>
        private static void RemoveOneChar()
        {
            if (buffer.Length > 0)
            {
                try
                {
                    GoBack();
                    Console.Write(' ');
                    GoBack();
                }
                catch (ArgumentOutOfRangeException) { /* Console was resized!? */ }
                buffer = buffer.Substring(0, buffer.Length - 1);

                if (buffer2.Length > 0)
                {
                    Console.Write(buffer2);
                    Console.Write(' ');
                    GoBack();
                    for (int i = 0; i < buffer2.Length; i++)
                    {
                        GoBack();
                    }
                }
            }
        }

        /// <summary>
        /// Move the cursor one character to the left inside the console, regardless of input prompt state
        /// </summary>
        private static void GoBack()
        {
            try
            {
                if (Console.CursorLeft == 0)
                {
                    Console.CursorLeft = Console.BufferWidth - 1;
                    if (Console.CursorTop > 0)
                        Console.CursorTop--;
                }
                else
                {
                    Console.CursorLeft = Console.CursorLeft - 1;
                }
            }
            catch (ArgumentOutOfRangeException) { /* Console was resized!? */ }
        }

        /// <summary>
        /// Move the cursor one character to the left in input prompt, adjusting buffers accordingly
        /// </summary>
        private static void GoLeft()
        {
            if (buffer.Length > 0)
            {
                buffer2 = "" + buffer[buffer.Length - 1] + buffer2;
                buffer = buffer.Substring(0, buffer.Length - 1);
                GoBack();
            }
        }

        /// <summary>
        /// Move the cursor one character to the right in input prompt, adjusting buffers accordingly
        /// </summary>
        private static void GoRight()
        {
            if (buffer2.Length > 0)
            {
                buffer = buffer + buffer2[0];
                Console.Write(buffer2[0]);
                buffer2 = buffer2.Substring(1);
            }
        }

        /// <summary>
        /// Insert a new character in the input prompt
        /// </summary>
        /// <param name="c">New character</param>
        private static void AddChar(char c)
        {
            Console.Write(c);
            buffer += c;
            Console.Write(buffer2);
            for (int i = 0; i < buffer2.Length; i++)
            {
                GoBack();
            }
        }

        #endregion

        #region Clipboard management

        /// <summary>
        /// Read a string from the system clipboard
        /// </summary>
        /// <returns>String from the system clipboard</returns>
        private static string ReadClipboard() {
            return TextCopy.ClipboardService.GetText() ?? "";
        }

        #endregion
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
