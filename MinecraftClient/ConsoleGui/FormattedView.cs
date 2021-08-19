using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using MinecraftClient.Commands;
using Terminal.Gui;
using Attribute = Terminal.Gui.Attribute;

namespace MinecraftClient.ConsoleGui {
    public class FormattedView : TextView {
        private static List<List<Rune>> formattedLines = new();
        private object lineLock = new();
        private static Regex FormatRegex = new Regex("(§[0-9a-fk-or])((?:[^§]|§[^0-9a-fk-or])*)", RegexOptions.Compiled);

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
            // Get the current string from our formatted list.
            // line is the unformatted string, so we need to get the formatted ones from our list
            // to determine the formatting we need.

            lock (lineLock) {
                var currentFormattedLine = formattedLines.FirstOrDefault(x => {
                    var v = x.TrimFormatting();
                    return v.SequenceEqual(line.ToArray());

                    // Throws InvalidOperation when formattedLines has desynced with the current lines shown.
                    // Should never happen theoretically but the exception is here just in case so we fail fast.
                }) ?? null;

                if (currentFormattedLine == null)
                    Debugger.Break();

                // Need to sync `index` with `currentFormattedLine` index
                // and then backwards read the current color.
                var runestr = currentFormattedLine.ToRuneString();
                var formatIndex = index;

                // How many § we have encountered. Determines the correct formatting to be used.
                int matchIndex = 0;

                for (int i = 0; i <= formatIndex; i++) {
                    if (currentFormattedLine[i] == '§') {
                        // Trailing §
                        if (formatIndex + 2 >= currentFormattedLine.Count) {
                            formatIndex = currentFormattedLine.Count - 1;
                            break;
                        }

                        // Are we actually a valid formatting character?
                        if (((char) currentFormattedLine[i + 1]).IsValidFormattingChar()) {
                            formatIndex += 2;
                            matchIndex++;
                        }
                    }
                }


                // We have desynced from the current index.
                // Debug.Assert(line[index] == currentFormattedLine[formatIndex]);

                var matches = FormatRegex.Matches(runestr);
                if (matchIndex == 0 || matches.Count == 0)
                    return;

                switch (matches[matchIndex - 1].Groups[1].Value) {
                    case "§0": ApplyFormatting(ColorType.ColorBlack); break;
                    case "§1": ApplyFormatting(ColorType.ColorDarkBlue); break;
                    case "§2": ApplyFormatting(ColorType.ColorDarkGreen); break;
                    case "§3": ApplyFormatting(ColorType.ColorDarkAqua); break;
                    case "§4": ApplyFormatting(ColorType.ColorDarkRed); break;
                    case "§5": ApplyFormatting(ColorType.ColorDarkPurple); break;
                    case "§6": ApplyFormatting(ColorType.ColorGold); break;
                    case "§7": ApplyFormatting(ColorType.ColorGray); break;
                    case "§8": ApplyFormatting(ColorType.ColorDarkGray); break;
                    case "§9": ApplyFormatting(ColorType.ColorBlue); break;
                    case "§a": ApplyFormatting(ColorType.ColorGreen); break;
                    case "§b": ApplyFormatting(ColorType.ColorAqua); break;
                    case "§c": ApplyFormatting(ColorType.ColorRed); break;
                    case "§d": ApplyFormatting(ColorType.ColorLightPurple); break;
                    case "§e": ApplyFormatting(ColorType.ColorYellow); break;
                    case "§f": ApplyFormatting(ColorType.ColorWhite); break;
                    case "§r": ApplyFormatting(ColorType.ColorBlack); break;
                }
            }

            void ApplyFormatting(ColorType colorType) {
                switch (colorType) {
                    case ColorType.ColorBlack:
                        Driver.SetAttribute(black);
                        break;
                    case ColorType.ColorDarkBlue:
                        Driver.SetAttribute(dark_blue);
                        break;
                    case ColorType.ColorDarkGreen:
                        Driver.SetAttribute(dark_green);
                        break;
                    case ColorType.ColorDarkAqua:
                        Driver.SetAttribute(dark_aqua);
                        break;
                    case ColorType.ColorDarkRed:
                        Driver.SetAttribute(dark_red);
                        break;
                    case ColorType.ColorDarkPurple:
                        Driver.SetAttribute(dark_purple);
                        break;
                    case ColorType.ColorGold:
                        Driver.SetAttribute(gold);
                        break;
                    case ColorType.ColorGray:
                        Driver.SetAttribute(gray);
                        break;
                    case ColorType.ColorDarkGray:
                        Driver.SetAttribute(dark_gray);
                        break;
                    case ColorType.ColorBlue:
                        Driver.SetAttribute(blue);
                        break;
                    case ColorType.ColorGreen:
                        Driver.SetAttribute(green);
                        break;
                    case ColorType.ColorAqua:
                        Driver.SetAttribute(aqua);
                        break;
                    case ColorType.ColorRed:
                        Driver.SetAttribute(red);
                        break;
                    case ColorType.ColorLightPurple:
                        Driver.SetAttribute(light_purple);
                        break;
                    case ColorType.ColorYellow:
                        Driver.SetAttribute(yellow);
                        break;
                    case ColorType.ColorWhite:
                        Driver.SetAttribute(white);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void WriteToConsole(string str, bool autoScroll) {
            List<Rune> rList = new List<Rune>();
            foreach (var rune in str.EnumerateRunes()) {
                var r = new Rune((uint) rune.Value);
                rList.Add(r);
            }

            lock (lineLock) {
                formattedLines.Add(rList);
            }
            
            Application.MainLoop.Invoke(() => {
                this.Text += str.TrimFormatting() + '\n';
                if (autoScroll)
                    MoveEnd();
            });
        }

        enum ColorType {
            ColorBlack,
            ColorDarkBlue,
            ColorDarkGreen,
            ColorDarkAqua,
            ColorDarkRed,
            ColorDarkPurple,
            ColorGold,
            ColorGray,
            ColorDarkGray,
            ColorBlue,
            ColorGreen,
            ColorAqua,
            ColorRed,
            ColorLightPurple,
            ColorYellow,
            ColorWhite
        }
    }
}