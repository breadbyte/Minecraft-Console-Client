using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using MinecraftClient.Protocol;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using MinecraftClient.Protocol.Handlers.Forge;
using MinecraftClient.Protocol.Session;
using MinecraftClient.Proxy;
using MinecraftClient.WinAPI;
using Sentry;
using Serilog;
using Serilog.Enrichers.Sensitive;

namespace MinecraftClient
{
    /// <summary>
    /// Minecraft Console Client by ORelio and Contributors (c) 2012-2020.
    /// Allows to connect to any Minecraft server, send and receive text, automated scripts.
    /// This source code is released under the CDDL 1.0 License.
    /// </summary>
    /// <remarks>
    /// Typical steps to update MCC for a new Minecraft version
    ///  - Implement protocol changes (see Protocol18.cs)
    ///  - Handle new block types and states (see Material.cs)
    ///  - Add support for new entity types (see EntityType.cs)
    ///  - Add new item types for inventories (see ItemType.cs)
    ///  - Mark new version as handled (see ProtocolHandler.cs)
    ///  - Update MCHighestVersion field below (for versionning)
    /// </remarks>
    static partial class Program
    {
        private static McClient client;
        private static CancellationTokenSource clientCancellationToken = new();
        public static string[] startupargs;

        public const string Version = Const.MCHighestVersion;
        public static readonly string BuildInfo = null;

        private static Tuple<Task, CancellationTokenSource>? offlinePrompt = null;
        private static bool useMcVersionOnce = false;

        /// <summary>
        /// The main entry point of Minecraft Console Client
        /// </summary>
        static void Main(string[] args) => MainAsync(args).GetAwaiter().GetResult(); 
        
        static async Task MainAsync(string[] args) {
            using (SentrySdk.Init(o => {
                o.Dsn = "https://881fa1bd09de4e2791add4facf090525@o405596.ingest.sentry.io/5848263";
                o.TracesSampleRate = 1.0;
            })) {
                Log.Logger = new LoggerConfiguration()
                             .WriteTo.Async(x => x.File("MinecraftConsoleClient.log", outputTemplate:"{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level}] {Message}{NewLine}{Exception}"))
                             .Enrich.WithSensitiveDataMasking()
                             .MinimumLevel.Verbose()
                             .CreateLogger();
                
                Console.WriteLine("Console Client for MC {0} to {1} - v{2} - By ORelio & Contributors", Const.MCLowestVersion, Const.MCHighestVersion, Version);

                //Build information to facilitate processing of bug reports
                if (BuildInfo != null) {
                    ConsoleIO.WriteLineFormatted("§8" + BuildInfo + " buildinfo");
                }

                //Debug input ?
                if (args.Length == 1 && args[0] == "--keyboard-debug") {
                    ConsoleIO.WriteLine("Keyboard debug mode: Press any key to display info");
                    ConsoleIO.DebugReadInput();
                }

                //Setup ConsoleIO
                ConsoleIO.LogPrefix = "§8[MCC] ";
                if (args.Length >= 1 && args[args.Length - 1] == "BasicIO" ||
                    args.Length >= 1 && args[args.Length - 1] == "BasicIO-NoColor") {
                    if (args.Length >= 1 && args[args.Length - 1] == "BasicIO-NoColor") {
                        ConsoleIO.BasicIO_NoColor = true;
                    }

                    ConsoleIO.BasicIO = true;
                    args = args.Where(o => !Object.ReferenceEquals(o, args[args.Length - 1])).ToArray();
                }

                //Take advantage of Windows 10 / Mac / Linux UTF-8 console
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                    // If we're on windows, check if our version is Win10 or greater.
                    if (WindowsVersion.WinMajorVersion >= 10)
                        Console.OutputEncoding = Console.InputEncoding = Encoding.UTF8;
                }
                else {
                    // Apply to all other operating systems.
                    Console.OutputEncoding = Console.InputEncoding = Encoding.UTF8;
                }

                var globalSettings = new Settings(); 
                
                //Process ini configuration file
                if (args.Length >= 1 && System.IO.File.Exists(args[0]) &&
                    System.IO.Path.GetExtension(args[0]).ToLower() == ".ini") {
                    globalSettings.LoadFile(args[0]);

                    //remove ini configuration file from arguments array
                    List<string> args_tmp = args.ToList<string>();
                    args_tmp.RemoveAt(0);
                    args = args_tmp.ToArray();
                }
                else if (System.IO.File.Exists("MinecraftClient.ini")) {
                    globalSettings.LoadFile("MinecraftClient.ini");
                }
                else globalSettings.WriteDefaultSettings("MinecraftClient.ini");

                //Load external translation file. Should be called AFTER settings loaded
                Translations.LoadExternalTranslationFile(globalSettings.Language);

                //Other command-line arguments
                if (args.Length >= 1) {
                    if (args.Contains("--help")) {
                        Console.WriteLine("Command-Line Help:");
                        Console.WriteLine("MinecraftClient.exe <username> <password> <server>");
                        Console.WriteLine("MinecraftClient.exe <username> <password> <server> \"/mycommand\"");
                        Console.WriteLine("MinecraftClient.exe --setting=value [--other settings]");
                        Console.WriteLine("MinecraftClient.exe --section.setting=value [--other settings]");
                        Console.WriteLine("MinecraftClient.exe <settings-file.ini> [--other settings]");
                        return;
                    }

                    try {
                        globalSettings.LoadArguments(args);
                    }
                    catch (ArgumentException e) {
                        SentrySdk.CaptureException(e);
                        globalSettings.interactiveMode = false;
                        HandleFailure(null, e.Message);
                        return;
                    }
                }

                if (globalSettings.ConsoleTitle != "") {
                    Console.Title = globalSettings.ExpandVars(globalSettings.ConsoleTitle);
                }

                //Test line to troubleshoot invisible colors
                if (globalSettings.DebugMessages) {
                    ConsoleIO.WriteLineFormatted(Translations.Get("debug.color_test",
                        "[0123456789ABCDEF]: [§00§11§22§33§44§55§66§77§88§99§aA§bB§cC§dD§eE§fF§r]"));
                }
                
                //Asking the user to type in missing data such as Username and Password
                bool useBrowser = globalSettings.AccountType == ProtocolHandler.AccountType.Microsoft &&
                                  globalSettings.LoginMethod == "browser";
                if (globalSettings.Login == "") {
                    if (useBrowser)
                        ConsoleIO.WriteLine(
                            "Press Enter to skip session cache checking and continue sign-in with browser");
                }

                // Setup exit cleaning code
                ExitCleanUp.Add(delegate() {
                    // Do NOT use Program.Exit() as creating new Thread cause program to freeze
                    if (client != null) {
                        client.Disconnect();
                        ConsoleIO.Reset();
                    }

                    if (offlinePrompt != null) {
                        offlinePrompt.Item2.Cancel();
                        offlinePrompt.Item1.Dispose();
                        offlinePrompt = null;
                        ConsoleIO.Reset();
                    }

                    if (globalSettings.playerHeadAsIcon) {
                        ConsoleIcon.revertToMCCIcon();
                    }

                    SentrySdk.FlushAsync(TimeSpan.FromMinutes(2));
                    SentrySdk.Close();
                });


                startupargs = args;
                await InitializeClient(globalSettings);

                // Keep the program alive
                await Task.Delay(-1, CancellationToken.None);
            }
        }

        /// <summary>
        /// Reduest user to submit password.
        /// </summary>
        private static string RequestPassword(string? login)
        {
            Console.Write(ConsoleIO.BasicIO ? Translations.Get("mcc.password_basic_io", login) + "\n" : Translations.Get("mcc.password"));
            var password = ConsoleIO.BasicIO ? Console.ReadLine() : ConsoleIO.ReadPassword();
            if (!ConsoleIO.BasicIO)
            {
                //Hide password length
                Console.CursorTop--; Console.Write(Translations.Get("mcc.password_hidden", "<******>"));
                for (int i = 19; i < Console.BufferWidth; i++) { Console.Write(' '); }
            }

            return password;
        }

        /// <summary>
        /// Start a new Client
        /// </summary>
        private static async Task InitializeClient(Settings settings) {
            McClient client;
            
            var proxyHandler = new ProxyHandler(settings.GetProxySettings());
            var sessionDispatcher = new SessionDispatcher(settings.SessionCaching, proxyHandler);
            var clientDispatcher = new ClientDispatcher(sessionDispatcher);
            
            if (string.IsNullOrWhiteSpace(settings.Login)) {
                Console.Write(ConsoleIO.BasicIO ? Translations.Get("mcc.login_basic_io") + "\n" : Translations.Get("mcc.login"));
                settings.Login = Console.ReadLine();
            }

            Result<McClient> clientResult;
            var cachedSessionResult = await sessionDispatcher.GetCachedSession(settings.Login);
            if (cachedSessionResult.IsFailed) {
                if (string.IsNullOrWhiteSpace(settings.Password)) {
                    settings.Password = RequestPassword(settings.Login);
                }
                clientResult = await clientDispatcher.CreateClient(proxyHandler, settings, settings.Login, settings.Password, settings.AccountType, settings.loginEnum);
            }
            else {
                clientResult = await clientDispatcher.CreateClientWithSession(proxyHandler, settings, cachedSessionResult.Value);
            }

            if (clientResult.IsFailed) {
                HandleFailure(settings, clientResult.Errors.First().Message, false, ChatBot.DisconnectReason.LoginRejected);
                return;
            }

            client = clientResult.Value;
            bool isRealms = false;

            if (settings.ConsoleTitle != "")
                Console.Title = settings.ExpandVars(settings.ConsoleTitle);

            if (settings.playerHeadAsIcon)
                ConsoleIcon.setPlayerIconAsync(settings.Username);

            Serilog.Log.Debug(Translations.Get("debug.session_id",clientResult.Value.GetSessionID()));


            List<string> availableWorlds = new List<string>();
            if (settings.MinecraftRealmsEnabled && !String.IsNullOrEmpty(clientResult.Value.GetSessionID())) {
                var getRealmsWorld = await ProtocolHandler.RealmsListWorldsAsync(proxyHandler, settings.Username, client.GetPlayerID(), client.GetSessionID());
                // todo this means we have an internal http error
                if (getRealmsWorld.IsFailed)
                    Console.WriteLine(getRealmsWorld.Errors[0].Message);
                else
                    availableWorlds = getRealmsWorld.Value;
            }

            if (settings.ServerIP == "") {
                Translations.Write("mcc.ip");
                string addressInput = Console.ReadLine();
                if (addressInput.StartsWith("realms:")) {
                    if (settings.MinecraftRealmsEnabled) {
                        if (availableWorlds.Count == 0) {
                            HandleFailure(settings, Translations.Get("error.realms.access_denied"), false,
                                ChatBot.DisconnectReason.LoginRejected);
                            return;
                        }

                        int worldIndex = 0;
                        string worldId = addressInput.Split(':')[1];
                        if (!availableWorlds.Contains(worldId) && int.TryParse(worldId, out worldIndex) &&
                            worldIndex < availableWorlds.Count)
                            worldId = availableWorlds[worldIndex];
                        if (availableWorlds.Contains(worldId)) {
                            var getRealmsWorldServer = await ProtocolHandler.GetRealmsWorldServerAddressAsync(proxyHandler, worldId, settings.Username, client.GetPlayerID(), client.GetSessionID());
                            if (getRealmsWorldServer.IsFailed) {
                                Console.WriteLine(getRealmsWorldServer.Errors[0].Message);
                                HandleFailure(settings, Translations.Get("error.realms.server_unavailable"), false, ChatBot.DisconnectReason.LoginRejected);
                                return;
                            }
                            else {
                                string RealmsAddress = getRealmsWorldServer.Value;
                                if (RealmsAddress != "") {
                                    addressInput = RealmsAddress;
                                    isRealms = true;
                                    settings.ServerVersion = Const.MCHighestVersion;
                                }
                            }
                        }
                        else {
                            HandleFailure(settings, Translations.Get("error.realms.server_id"), false,
                                ChatBot.DisconnectReason.LoginRejected);
                            return;
                        }
                    }
                    else {
                        HandleFailure(settings, Translations.Get("error.realms.disabled"), false, null);
                        return;
                    }
                }

                settings.SetServerIP(addressInput);
            }

            await client.ConnectToServer(settings.ServerIP, settings.ServerPort);
            await client.BeginInteraction();
        }

        /// <summary>
        /// Disconnect the current client from the server and restart it
        /// </summary>
        /// <param name="delaySeconds">Optional delay, in seconds, before restarting</param>
        public static void Restart(int delaySeconds = 0) {
            Task.Run(async () => {
                if (client != null) { clientCancellationToken.Cancel(); client.Disconnect(); ConsoleIO.Reset(); }
                if (offlinePrompt != null) { offlinePrompt.Item2.Cancel(); offlinePrompt = null; ConsoleIO.Reset(); }
                if (delaySeconds > 0)
                {
                    Translations.WriteLine("mcc.restart_delay", delaySeconds);
                    System.Threading.Thread.Sleep(delaySeconds * 1000);
                }
                Translations.WriteLine("mcc.restart");

                var settings = new Settings();
                settings.LoadFile("MinecraftClient.ini");
                await InitializeClient(settings);
            });
        }

        /// <summary>
        /// Disconnect the current client from the server and exit the app
        /// </summary>
        public static void Exit(int exitcode = 0) {
            Task.Run(() => {
                if (client != null) { clientCancellationToken.Cancel(); client.Disconnect(); ConsoleIO.Reset(); }
                if (offlinePrompt != null) { offlinePrompt.Item2.Cancel(); offlinePrompt = null; ConsoleIO.Reset(); }
                Environment.Exit(exitcode);
            });
        }

        /// <summary>
        /// Handle fatal errors such as ping failure, login failure, server disconnection, and so on.
        /// Allows AutoRelog to perform on fatal errors, prompt for server version, and offline commands.
        /// </summary>
        /// <param name="errorMessage">Error message to display and optionally pass to AutoRelog bot</param>
        /// <param name="versionError">Specify if the error is related to an incompatible or unkown server version</param>
        /// <param name="disconnectReason">If set, the error message will be processed by the AutoRelog bot</param>
        public static void HandleFailure(Settings settings, string errorMessage = null, bool versionError = false, ChatBots.AutoRelog.DisconnectReason? disconnectReason = null)
        {
            // TODO Console-specific settings should be static
            StackTrace stackTrace = new StackTrace();
            Console.WriteLine($"Failure handled from {stackTrace.GetFrame(1)?.GetMethod()?.Name}");
            if (!String.IsNullOrEmpty(errorMessage))
            {
                ConsoleIO.Reset();
                while (Console.KeyAvailable)
                    Console.ReadKey(true);
                Console.WriteLine(errorMessage);

                if (disconnectReason.HasValue)
                {
                    if (settings.AutoRelog_Enabled) {
                        if (ChatBots.AutoRelog.OnDisconnectStatic(disconnectReason.Value, errorMessage, settings.AutoRelog_Delay_Min, settings.AutoRelog_Delay_Max, settings.AutoRelog_Retries))
                            return; //AutoRelog is triggering a restart of the client
                    }
                }
            }

            if (settings.interactiveMode)
            {
                if (versionError)
                {
                    Translations.Write("mcc.server_version");
                    settings.ServerVersion = Console.ReadLine();
                    if (settings.ServerVersion != "")
                    {
                        useMcVersionOnce = true;
                        Restart();
                        return;
                    }
                }

                if (offlinePrompt == null) {
                    var cancellationTokenSource = new CancellationTokenSource();
                    var offlineTask = Task.Factory.StartNew(() => {
                        string command = " ";
                        ConsoleIO.WriteLineFormatted(Translations.Get("mcc.disconnected", (settings.internalCmdChar == ' ' ? "" : "" + settings.internalCmdChar)));
                        Translations.WriteLineFormatted("mcc.press_exit");

                        while (!cancellationTokenSource.IsCancellationRequested) {
                            while (command.Length > 0) {
                                if (!ConsoleIO.BasicIO) {
                                    ConsoleIO.Write('>');
                                }

                                command = Console.ReadLine().Trim();
                                if (command.Length > 0) {
                                    string message = "";

                                    if (settings.internalCmdChar != ' '
                                        && command[0] == settings.internalCmdChar)
                                        command = command.Substring(1);

                                    if (command.StartsWith("reco")) {
                                        message = new Commands.Reco().Run(settings, null, settings.ExpandVars(command), null);
                                    }
                                    else if (command.StartsWith("connect")) {
                                        message = new Commands.Connect().Run(settings, null, settings.ExpandVars(command), null);
                                    }
                                    else if (command.StartsWith("exit") || command.StartsWith("quit")) {
                                        message = new Commands.Exit().Run(settings, null, settings.ExpandVars(command), null);
                                    }
                                    else if (command.StartsWith("help")) {
                                        ConsoleIO.WriteLineFormatted("§8MCC: " +
                                                                     (settings.internalCmdChar == ' '
                                                                         ? ""
                                                                         : "" + settings.internalCmdChar) +
                                                                     new Commands.Reco().GetCmdDescTranslated());
                                        ConsoleIO.WriteLineFormatted("§8MCC: " +
                                                                     (settings.internalCmdChar == ' '
                                                                         ? ""
                                                                         : "" + settings.internalCmdChar) +
                                                                     new Commands.Connect().GetCmdDescTranslated());
                                    }
                                    else
                                        ConsoleIO.WriteLineFormatted(Translations.Get("icmd.unknown",
                                            command.Split(' ')[0]));

                                    if (message != "")
                                        ConsoleIO.WriteLineFormatted("§8MCC: " + message);
                                }
                            }
                        }
                    }, cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
                    
                    offlinePrompt = new(offlineTask, cancellationTokenSource);
                }
            }
            else
            {
                // Not in interactive mode, just exit and let the calling script handle the failure
                if (disconnectReason.HasValue)
                {
                    // Return distinct exit codes for known failures.
                    if (disconnectReason.Value == ChatBot.DisconnectReason.UserLogout) Exit(1);
                    if (disconnectReason.Value == ChatBot.DisconnectReason.InGameKick) Exit(2);
                    if (disconnectReason.Value == ChatBot.DisconnectReason.ConnectionLost) Exit(3);
                    if (disconnectReason.Value == ChatBot.DisconnectReason.LoginRejected) Exit(4);
                }
                Exit();
            }

        }

        /// <summary>
        /// Detect if the user is running Minecraft Console Client through Mono
        /// </summary>
        public static bool isUsingMono
        {
            get
            {
                return Type.GetType("Mono.Runtime") != null;
            }
        }

        /// <summary>
        /// Enumerate types in namespace through reflection
        /// </summary>
        /// <param name="nameSpace">Namespace to process</param>
        /// <param name="assembly">Assembly to use. Default is Assembly.GetExecutingAssembly()</param>
        /// <returns></returns>
        public static Type[] GetTypesInNamespace(string nameSpace, Assembly assembly = null)
        {
            if (assembly == null) { assembly = Assembly.GetExecutingAssembly(); }
            return assembly.GetTypes().Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal)).ToArray();
        }

        /// <summary>
        /// Static initialization of build information, read from assembly information
        /// </summary>
        static Program()
        {
            AssemblyConfigurationAttribute attribute
             = typeof(Program)
                .Assembly
                .GetCustomAttributes(typeof(System.Reflection.AssemblyConfigurationAttribute), false)
                .FirstOrDefault() as AssemblyConfigurationAttribute;
            if (attribute != null)
                BuildInfo = attribute.Configuration;
        }
    }
}
