using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SqlServer.Server;
using Terminal.Gui;

namespace MinecraftClient.ConsoleGui {
    public class ConsoleHandler{
        public static ConsoleHandler? Instance { get; private set; }
        private bool autoScroll = true;
        private static object inputLock = new();
        private static bool inputReady = false;
        private static string userInput = "";

        private static AutoResetEvent InputTextAvailable = new(false);
        
        static FormattedView consoleOutput = new FormattedView() {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            ReadOnly = true,
        };
        static TextField textBox = new TextField("Enter a command...") {
            X = 0,
            Y = Pos.Bottom(consoleOutput),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        private Window rootWindow = new Window("Minecraft Console Client");

        public ConsoleHandler() {
            var appTop = Application.Top;
            
            // Color scheme doesn't set on static so we have to set it here
            consoleOutput.ColorScheme = Colors.ColorSchemes["TopLevel"];
            textBox.ColorScheme = Colors.ColorSchemes["TopLevel"];
            rootWindow.ColorScheme = Colors.ColorSchemes["Menu"];

            var status = new StatusBar(new StatusItem[] {
                    new StatusItem(Key.F1, "~F1~ Help", null),
                    new StatusItem(Key.F2, "~F2~ Load", null),
                    new StatusItem(Key.F3, "~F3~ Save", null),
                    new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", null)
                });
            
            textBox.KeyPress += (x => {
                // Prevent the Tab Handler from switching us to a different view.
                if (x.KeyEvent.Key == Key.Tab) {
                    x.Handled = true;
                    return;
                }

                // Handle everything else.
                x.Handled = false;
            });
            textBox.KeyDown += (x => {
                switch (x.KeyEvent.Key) {
                    case Key.Enter:
                        userInput = textBox.Text.ToString() ?? "";
                        InputTextAvailable.Set();
                        lock (inputLock)
                            textBox.Text = "";
                        break;
                    case Key.Tab:
                        // todo tab handler
                        textBox.Text += "TAB";
                        textBox.CursorPosition = textBox.Text.Length;
                        break;
                }

                x.Handled = true;
            });
            
            textBox.Enter += (x => {
                if (textBox.Text == "Enter a command...") {
                    textBox.Text = "";
                    x.Handled = true;
                }
            });
            textBox.Leave += (x => {
                if (string.IsNullOrWhiteSpace(textBox.Text.ToString())) {
                    textBox.Text = "Enter a command...";
                }

                x.Handled = true;
            });
            
            rootWindow.Add(consoleOutput, textBox);
            appTop.Add(rootWindow, status);
            
            consoleOutput.SetFocus();
            Instance = this;
        }

        public static string WaitForInput(bool passwordInput = false) {
            if (passwordInput)
                textBox.Secret = true;
            
            InputTextAvailable.WaitOne();
            
            if (passwordInput)
                textBox.Secret = false;
            
            lock (inputLock)
                return userInput;
        }

        public void WriteLine(string strToWrite) {
            var newlines = strToWrite.Split('\n');
            foreach (var line in newlines) {
                consoleOutput.WriteToConsole("§r" + line, Settings.AutoScroll);
            }
        }

        public void SetTitle(string title) {
            rootWindow.Title = title;
        }
    }
}