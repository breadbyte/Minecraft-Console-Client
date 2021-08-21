using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MinecraftClient.Commands;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace MinecraftClient.ConsoleGui {
    public class FormattedView : TextView {
        private static List<Rune[]> formattedLines = new();
        private static List<Rune[]> currentLines = new();
        private static List<char[]> formattingCache = new();
        private object lineLock = new();
        private static Regex FormatRegex = new Regex("(§[0-9a-fk-or])((?:[^§]|§[^0-9a-fk-or])*)", RegexOptions.Compiled);
        private static ArrayPool<char> ArrayPool = ArrayPool<char>.Create();
        FastRuneCompare _fastRuneCompare = new FastRuneCompare();

        private Attribute black = Driver.MakeAttribute(Color.Gray, Color.Black);
        private Attribute dark_blue = Driver.MakeAttribute(Color.Blue, Color.Black);
        private Attribute dark_green = Driver.MakeAttribute(Color.Green, Color.Black);
        private Attribute dark_aqua = Driver.MakeAttribute(Color.Cyan, Color.Black);
        private Attribute dark_red = Driver.MakeAttribute(Color.Red, Color.Black);
        private Attribute dark_purple = Driver.MakeAttribute(Color.Magenta, Color.Black);
        private Attribute gold = Driver.MakeAttribute(Color.BrightYellow, Color.Black);
        private Attribute gray = Driver.MakeAttribute(Color.Gray, Color.Black);
        private Attribute dark_gray = Driver.MakeAttribute(Color.DarkGray, Color.Black);
        private Attribute blue = Driver.MakeAttribute(Color.BrightBlue, Color.Black);
        private Attribute green = Driver.MakeAttribute(Color.BrightGreen, Color.Black);
        private Attribute aqua = Driver.MakeAttribute(Color.BrightCyan, Color.Black);
        private Attribute red = Driver.MakeAttribute(Color.BrightRed, Color.Black);
        private Attribute light_purple = Driver.MakeAttribute(Color.BrightMagenta, Color.Black);
        private Attribute yellow = Driver.MakeAttribute(Color.BrightYellow, Color.Black);
        private Attribute white = Driver.MakeAttribute(Color.White, Color.Black);

        protected override void ColorNormal(List<Rune> line, int index) {
            char[] cLine = null!;
            lock (lineLock)
                foreach (var runearr in currentLines) {
                    if (line.Count == runearr.Length) {
                        foreach (var rune in runearr) {
                            if (runearr.SequenceEqual(line, _fastRuneCompare)) {
                                cLine = formattingCache[currentLines.IndexOf(runearr)];
                                break;
                            }
                        }
                    }
                    continue;
                }
            
            switch (cLine![index]) {
                case '0': Driver.SetAttribute(black); break;
                case '1': Driver.SetAttribute(dark_blue); break;
                case '2': Driver.SetAttribute(dark_green); break;
                case '3': Driver.SetAttribute(dark_aqua); break;
                case '4': Driver.SetAttribute(dark_red); break;
                case '5': Driver.SetAttribute(dark_purple); break;
                case '6': Driver.SetAttribute(gold); break;
                case '7': Driver.SetAttribute(gray); break;
                case '8': Driver.SetAttribute(dark_gray); break;
                case '9': Driver.SetAttribute(blue); break;
                case 'a': Driver.SetAttribute(green); break;
                case 'b': Driver.SetAttribute(aqua); break;
                case 'c': Driver.SetAttribute(red); break;
                case 'd': Driver.SetAttribute(light_purple); break;
                case 'e': Driver.SetAttribute(yellow); break;
                case 'f': Driver.SetAttribute(white); break;
                case 'r': Driver.SetAttribute(white); break;
                default:
                    Driver.SetAttribute(white);
                    break;
            }
        }

        private char[] Format(string formattedString) {
            char[] arr = new char[formattedString.Length];
            for (int index = 0; index < formattedString.Length; index++) {
                // Need to sync `index` with `currentFormattedLine` index
                // and then backwards read the current color.
                var runestr = formattedString;
                var formatIndex = index;

                // How many § we have encountered. Determines the correct formatting to be used.
                int matchIndex = 0;

                for (int i = 0; i <= formatIndex; i++) {
                    if (formattedString[i] == '§') {
                        // Trailing §
                        if (formatIndex + 2 >= formattedString.Length) {
                            formatIndex = formattedString.Length - 1;
                            break;
                        }

                        // Are we actually a valid formatting character?
                        if (((char) formattedString[i + 1]).IsValidFormattingChar()) {
                            formatIndex += 2;
                            matchIndex++;
                        }
                    }
                }

                // We have desynced from the current index.
                // Debug.Assert(line[index] == currentFormattedLine[formatIndex]);

                var matches = FormatRegex.Matches(runestr);
                if (matchIndex == 0 || matches.Count == 0) {
                    arr[index] = 'r';
                    continue;
                }

                switch (matches[matchIndex - 1].Groups[1].Value) {
                    case "§0": arr[index] = '0'; break;
                    case "§1": arr[index] = '1'; break;
                    case "§2": arr[index] = '2'; break;
                    case "§3": arr[index] = '3'; break;
                    case "§4": arr[index] = '4'; break;
                    case "§5": arr[index] = '5'; break;
                    case "§6": arr[index] = '6'; break;
                    case "§7": arr[index] = '7'; break;
                    case "§8": arr[index] = '8'; break;
                    case "§9": arr[index] = '9'; break;
                    case "§a": arr[index] = 'a'; break;
                    case "§b": arr[index] = 'b'; break;
                    case "§c": arr[index] = 'c'; break;
                    case "§d": arr[index] = 'd'; break;
                    case "§e": arr[index] = 'e'; break;
                    case "§f": arr[index] = 'f'; break;
                    case "§r": arr[index] = 'r'; break;
                }
            }
            return arr;
        }

        public void WriteToConsole(string str, bool autoScroll) {
            List<Rune> rList = new List<Rune>();
            foreach (var rune in str.EnumerateRunes()) {
                var r = new Rune((uint) rune.Value);
                rList.Add(r);
            }

            lock (lineLock) {
                formattedLines.Add(rList.ToArray());
                formattingCache.Add(Format(str));
                currentLines.Add(rList.TrimFormatting().ToArray());
            }
            
            Application.MainLoop.Invoke(() => {
                this.Text += str.TrimFormatting() + '\n';
                if (autoScroll)
                    MoveEnd();
            });
        }
    }

    class FastRuneCompare : IEqualityComparer<Rune> {
        public bool Equals(Rune x, Rune y) {
            return x.GetHashCode() == y.GetHashCode();
        }

        public int GetHashCode(Rune obj) {
            return obj.GetHashCode();
        }
    }
}