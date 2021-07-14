using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Net.Security;
using System.Threading;
using System.Threading.Tasks;
using DnsClient;
using FluentResults;
using MinecraftClient.Proxy;
using MinecraftClient.Protocol.Handlers;
using MinecraftClient.Protocol.Handlers.Forge;
using MinecraftClient.Protocol.Session;
using Sentry;

namespace MinecraftClient.Protocol {
    /// <summary>
    /// Handle login, session, server ping and provide a protocol handler for interacting with a minecraft server.
    /// </summary>
    /// <remarks>
    /// Typical update steps for marking a new Minecraft version as supported:
    ///  - Add protocol ID in GetProtocolHandler()
    ///  - Add 1.X.X case in MCVer2ProtocolVersion()
    /// </remarks>
    public static class ProtocolHandler {
        /// <summary>
        /// Perform a DNS lookup for a Minecraft Service using the specified domain name
        /// </summary>
        /// <param name="domain">Input domain name, updated with target host if any, else left untouched</param>
        /// <param name="port">Updated with target port if any, else left untouched</param>
        /// <returns>TRUE if a Minecraft Service was found.</returns>
        public static bool MinecraftServiceLookup(ref string domain, ref ushort port) {
            bool foundService = false;
            string domainVal = domain;
            ushort portVal = port;

            if (!String.IsNullOrEmpty(domain) && domain.Any(c => char.IsLetter(c))) {

                try {
                    Translations.WriteLine("mcc.resolve", domainVal);
                    var lookupClient = new LookupClient();
                    var response = lookupClient.Query(new DnsQuestion($"_minecraft._tcp.{domainVal}", QueryType.SRV));
                    if (response.HasError != true && response.Answers.SrvRecords().Any()) {
                        //Order SRV records by priority and weight, then randomly
                        var result = response.Answers.SrvRecords()
                                             .OrderBy(record => record.Priority)
                                             .ThenByDescending(record => record.Weight)
                                             .ThenBy(record => Guid.NewGuid())
                                             .First();
                        string target = result.Target.Value.Trim('.');
                        ConsoleIO.WriteLineFormatted(Translations.Get("mcc.found", target, result.Port, domainVal));
                        domainVal = target;
                        portVal = result.Port;
                        foundService = true;
                    }
                }
                catch (Exception e) {
                    ConsoleIO.WriteLineFormatted(Translations.Get("mcc.not_found", domainVal, e.GetType().FullName,
                        e.Message));
                }

                domain = domainVal;
                port = portVal;
                return foundService;
            }

            return foundService;
        }

        /// <summary>
        /// Retrieve information about a Minecraft server
        /// </summary>
        /// <param name="serverIP">Server IP to ping</param>
        /// <param name="serverPort">Server Port to ping</param>
        /// <returns>TRUE if ping was successful</returns>
        public static async Task<Result<ProtocolPingResult>> GetServerInfo(ProxyHandler handler, Settings.ProxySettings settings, string serverIP, ushort serverPort) {
            bool success = false;
            ProtocolPingResult protocolResult;
            var protocol16 = await Protocol16Handler.doPing(settings, handler, serverIP, serverPort);
            if (protocol16.IsSuccess) {
                protocolResult = protocol16.Value;
            }
            else {
                var protocol18 = await Protocol18Handler.doPing(settings, handler, serverIP, serverPort);
                if (protocol18.IsFailed) {
                    return Result.Fail(Translations.Get("error.connect"));
                }

                protocolResult = protocol18.Value;
            }
            
            return Result.Ok(protocolResult);
        }

        /// <summary>
        /// Get a protocol handler for the specified Minecraft version
        /// </summary>
        /// <param name="Client">Tcp Client connected to the server</param>
        /// <param name="ProtocolVersion">Protocol version to handle</param>
        /// <param name="Handler">Handler with the appropriate callbacks</param>
        /// <returns></returns>
        public static Result<IMinecraftCom> GetProtocolHandler(TcpClient client, Settings settings, int ProtocolVersion, ForgeInfo forgeInfo, IMinecraftComHandler Handler)
        {
            int[] supportedVersions_Protocol16 = { 51, 60, 61, 72, 73, 74, 78 };
            if (Array.IndexOf(supportedVersions_Protocol16, ProtocolVersion) > -1)
                return Result.Ok<IMinecraftCom>(new Protocol16Handler(client, ProtocolVersion, Handler));
            int[] supportedVersions_Protocol18 = { 4, 5, 47, 107, 108, 109, 110, 210, 315, 316, 335, 338, 340, 393, 401, 404, 477, 480, 485, 490, 498, 573, 575, 578, 735, 736, 751, 753, 754, 755 };
            if (Array.IndexOf(supportedVersions_Protocol18, ProtocolVersion) > -1)
                return Result.Ok<IMinecraftCom>(new Protocol18Handler(settings, client, ProtocolVersion, Handler, forgeInfo));
            return Result.Fail("Version not supported");
        }

        /// <summary>
        /// Convert a human-readable Minecraft version number to network protocol version number
        /// </summary>
        /// <param name="MCVersion">The Minecraft version number</param>
        /// <returns>The protocol version number or 0 if could not determine protocol version: error, unknown, not supported</returns>
        public static int MCVer2ProtocolVersion(string MCVersion)
        {
            if (MCVersion.Contains('.'))
            {
                switch (MCVersion.Split(' ')[0].Trim())
                {
                    case "1.4.6":
                    case "1.4.7":
                        return 51;
                    case "1.5.1":
                        return 60;
                    case "1.5.2":
                        return 61;
                    case "1.6":
                    case "1.6.0":
                        return 72;
                    case "1.6.1":
                    case "1.6.2":
                    case "1.6.3":
                    case "1.6.4":
                        return 73;
                    case "1.7.2":
                    case "1.7.3":
                    case "1.7.4":
                    case "1.7.5":
                        return 4;
                    case "1.7.6":
                    case "1.7.7":
                    case "1.7.8":
                    case "1.7.9":
                    case "1.7.10":
                        return 5;
                    case "1.8":
                    case "1.8.0":
                    case "1.8.1":
                    case "1.8.2":
                    case "1.8.3":
                    case "1.8.4":
                    case "1.8.5":
                    case "1.8.6":
                    case "1.8.7":
                    case "1.8.8":
                    case "1.8.9":
                        return 47;
                    case "1.9":
                    case "1.9.0":
                        return 107;
                    case "1.9.1":
                        return 108;
                    case "1.9.2":
                        return 109;
                    case "1.9.3":
                    case "1.9.4":
                        return 110;
                    case "1.10":
                    case "1.10.0":
                    case "1.10.1":
                    case "1.10.2":
                        return 210;
                    case "1.11":
                    case "1.11.0":
                        return 315;
                    case "1.11.1":
                    case "1.11.2":
                        return 316;
                    case "1.12":
                    case "1.12.0":
                        return 335;
                    case "1.12.1":
                        return 338;
                    case "1.12.2":
                        return 340;
                    case "1.13":
                        return 393;
                    case "1.13.1":
                        return 401;
                    case "1.13.2":
                        return 404;
                    case "1.14":
                    case "1.14.0":
                        return 477;
                    case "1.14.1":
                        return 480;
                    case "1.14.2":
                        return 485;
                    case "1.14.3":
                        return 490;
                    case "1.14.4":
                        return 498;
                    case "1.15":
                    case "1.15.0":
                        return 573;
                    case "1.15.1":
                        return 575;
                    case "1.15.2":
                        return 578;
                    case "1.16":
                    case "1.16.0":
                        return 735;
                    case "1.16.1":
                        return 736;
                    case "1.16.2":
                        return 751;
                    case "1.16.3":
                        return 753;
                    case "1.16.4":
                    case "1.16.5":
                        return 754;
                    case "1.17":
                        return 755;
                    default:
                        return 0;
                }
            }
            else
            {
                try
                {
                    return Int32.Parse(MCVersion);
                }
                catch (Exception e)
                {
                    SentrySdk.CaptureException(e);
                    return 0;
                }
            }
        }

        /// <summary>
        /// Convert a network protocol version number to human-readable Minecraft version number
        /// </summary>
        /// <remarks>Some Minecraft versions share the same protocol number. In that case, the lowest version for that protocol is returned.</remarks>
        /// <param name="protocol">The Minecraft protocol version number</param>
        /// <returns>The 1.X.X version number, or 0.0 if could not determine protocol version</returns>
        public static string ProtocolVersion2MCVer(int protocol)
        {
            switch (protocol)
            {
                case 51: return "1.4.6";
                case 60: return "1.5.1";
                case 62: return "1.5.2";
                case 72: return "1.6";
                case 73: return "1.6.1";
                case 4: return "1.7.2";
                case 5: return "1.7.6";
                case 47: return "1.8";
                case 107: return "1.9";
                case 108: return "1.9.1";
                case 109: return "1.9.2";
                case 110: return "1.9.3";
                case 210: return "1.10";
                case 315: return "1.11";
                case 316: return "1.11.1";
                case 335: return "1.12";
                case 338: return "1.12.1";
                case 340: return "1.12.2";
                case 393: return "1.13";
                case 401: return "1.13.1";
                case 404: return "1.13.2";
                case 477: return "1.14";
                case 480: return "1.14.1";
                case 485: return "1.14.2";
                case 490: return "1.14.3";
                case 498: return "1.14.4";
                case 573: return "1.15";
                case 575: return "1.15.1";
                case 578: return "1.15.2";
                case 735: return "1.16";
                case 736: return "1.16.1";
                case 751: return "1.16.2";
                case 753: return "1.16.3";
                case 754: return "1.16.5";
                default: return "0.0";
            }
        }

        /// <summary>
        /// Check if we can force-enable Forge support for a Minecraft version without using server Ping
        /// </summary>
        /// <param name="protocolVersion">Minecraft protocol version</param>
        /// <returns>TRUE if we can force-enable Forge support without using server Ping</returns>
        public static bool ProtocolMayForceForge(int protocol)
        {
            return Protocol18Forge.ServerMayForceForge(protocol);
        }

        /// <summary>
        /// Server Info: Consider Forge to be enabled regardless of server Ping
        /// </summary>
        /// <param name="protocolVersion">Minecraft protocol version</param>
        /// <returns>ForgeInfo item stating that Forge is enabled</returns>
        public static ForgeInfo ProtocolForceForge(int protocol)
        {
            return Protocol18Forge.ServerForceForge(protocol);
        }

        public enum LoginResult { OtherError, ServiceUnavailable, SSLError, Success, WrongPassword, AccountMigrated, NotPremium, LoginRequired, InvalidToken, InvalidResponse, NullError, UserCancel };
        public enum AccountType { Mojang, Microsoft };
        public enum LoginMethod { MCC, Browser };

        /// <summary>
        /// Allows to login to a premium Minecraft account using the Yggdrasil authentication scheme.
        /// </summary>
        /// <param name="user">Login</param>
        /// <param name="pass">Password</param>
        /// <param name="session">In case of successful login, will contain session information for multiplayer</param>
        /// <returns>Returns the status of the login (Success, Failure, etc.)</returns>
        public static async Task<Result<SessionToken>> GetLogin(ProxyHandler proxyHandler, string user, string pass, AccountType type, LoginMethod loginMethod) {
            SessionToken s = new SessionToken();

            switch (type) {
                case AccountType.Microsoft:
                    if (loginMethod == LoginMethod.MCC)
                        return MicrosoftMCCLogin(proxyHandler, user, pass);
                    else
                        return MicrosoftBrowserLogin(proxyHandler);
                case AccountType.Mojang:
                    return await MojangLogin(proxyHandler, user, pass);
                    break;
                default:
                    throw new NotImplementedException("This account type is not implemented!");
            }
        }

        /// <summary>
        /// Login using Mojang account. Will be outdated after account migration
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pass"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        private static async Task<Result<SessionToken>> MojangLogin(ProxyHandler _proxyHandler, string user, string pass) {
            var session = new SessionToken() {ClientID = Guid.NewGuid().ToString().Replace("-", "")};
            string json_request = "{\"agent\": { \"name\": \"Minecraft\", \"version\": 1 }, \"username\": \"" +
                                  JsonEncode(user) + "\", \"password\": \"" + JsonEncode(pass) +
                                  "\", \"clientToken\": \"" + JsonEncode(session.ClientID) + "\" }";
            var requestResult = await DoHTTPSPostAsync(_proxyHandler, "authserver.mojang.com", "/authenticate", json_request);
            if (requestResult.IsFailed)
                return Result.Fail(requestResult.Errors[0].Message);
            
            if (requestResult.Value.StatusCode == 200) {
                if (requestResult.Value.Response.Contains("availableProfiles\":[]}")) {
                    return Result.Fail(new LoginFailure(LoginResult.NotPremium));
                }
                else {
                    Json.JSONData loginResponse = Json.ParseJson(requestResult.Value.Response);
                    if (loginResponse.Properties.ContainsKey("accessToken")
                        && loginResponse.Properties.ContainsKey("selectedProfile")
                        && loginResponse.Properties["selectedProfile"].Properties.ContainsKey("id")
                        && loginResponse.Properties["selectedProfile"].Properties.ContainsKey("name")) {
                        session.ID = loginResponse.Properties["accessToken"].StringValue;
                        session.PlayerID = loginResponse.Properties["selectedProfile"].Properties["id"].StringValue;
                        session.PlayerName = loginResponse.Properties["selectedProfile"].Properties["name"].StringValue;
                        return Result.Ok(session);
                    }
                    else return Result.Fail(new LoginFailure(LoginResult.InvalidResponse));
                }
            }

            if (requestResult.Value.StatusCode == 403) {
                if (requestResult.Value.Response.Contains("UserMigratedException")) {
                    return Result.Fail(new LoginFailure(LoginResult.AccountMigrated));
                }
                
                return Result.Fail(new LoginFailure(LoginResult.WrongPassword));
            }

            if (requestResult.Value.StatusCode == 503) {
                Result.Fail(new LoginFailure(LoginResult.ServiceUnavailable));
            }

            return Result.Fail(new LoginFailure(LoginResult.OtherError)).WithReason(new Error(requestResult.Value.StatusCode.ToString()));
        }

        /// <summary>
        /// Sign-in to Microsoft Account without using browser. Only works if 2FA is disabled.
        /// Might not work well in some rare cases.
        /// </summary>
        /// <param name="email"></param>
        /// <param name="password"></param>
        /// <param name="session"></param>
        /// <returns></returns>
        private static Result<SessionToken> MicrosoftMCCLogin(ProxyHandler _proxyHandler, string email, string password) {
            var ms = new XboxLive(_proxyHandler);
            try
            {
                var msaResponse = ms.UserLogin(email, password, ms.PreAuth());
                return MicrosoftLogin(_proxyHandler, msaResponse);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                ConsoleIO.WriteLineFormatted("§cMicrosoft authenticate failed: " + e.Message);
                Serilog.Log.Debug(e.StackTrace);
                return Result.Fail(new LoginFailure(LoginResult.WrongPassword)); // Might not always be wrong password
            }
        }

        /// <summary>
        /// Sign-in to Microsoft Account by asking user to open sign-in page using browser. 
        /// </summary>
        /// <remarks>
        /// The downside is this require user to copy and paste lengthy content from and to console.
        /// Sign-in page: 218 chars
        /// Response URL: around 1500 chars
        /// </remarks>
        /// <param name="session"></param>
        /// <returns></returns>
        public static Result<SessionToken> MicrosoftBrowserLogin(ProxyHandler _proxyHandler)
        {
            var session = new SessionToken();
            var ms = new XboxLive(_proxyHandler);
            string[] askOpenLink =
            {
                "Copy the following link to your browser and login to your Microsoft Account",
                ">>>>>>>>>>>>>>>>>>>>>>",
                "",
                ms.SignInUrl,
                "",
                "<<<<<<<<<<<<<<<<<<<<<<",
                "NOTICE: Once successfully logged in, you will see a blank page in your web browser.",
                "Copy the contents of your browser's address bar and paste it below to complete the login process.",
            };
            ConsoleIO.WriteLine(string.Join("\n", askOpenLink));
            string[] parts = { };
            while (true)
            {
                string link = ConsoleIO.ReadLine();
                if (string.IsNullOrEmpty(link))
                {
                    return Result.Fail(new LoginFailure(LoginResult.UserCancel));
                }
                parts = link.Split('#');
                if (parts.Length < 2)
                {
                    ConsoleIO.WriteLine("Invalid link. Please try again.");
                    continue;
                }
                else break;
            }
            string hash = parts[1];
            var dict = Request.ParseQueryString(hash);
            var msaResponse = new XboxLive.UserLoginResponse()
            {
                AccessToken = dict["access_token"],
                RefreshToken = dict["refresh_token"],
                ExpiresIn = int.Parse(dict["expires_in"])
            };
            try
            {
                return MicrosoftLogin(_proxyHandler, msaResponse);
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                session = new SessionToken() { ClientID = Guid.NewGuid().ToString().Replace("-", "") };
                ConsoleIO.WriteLineFormatted("§cMicrosoft authenticate failed: " + e.Message);
                Serilog.Log.Debug(e.StackTrace);
                return Result.Fail(new LoginFailure(LoginResult.WrongPassword)); // Might not always be wrong password
            }
        }

        private static Result<SessionToken> MicrosoftLogin(ProxyHandler _proxyHandler, XboxLive.UserLoginResponse msaResponse)
        {
            var session = new SessionToken() { ClientID = Guid.NewGuid().ToString().Replace("-", "") };
            var ms = new XboxLive(_proxyHandler);
            var mc = new MinecraftWithXbox(_proxyHandler);

            try
            {
                var xblResponse = ms.XblAuthenticate(msaResponse);
                var xsts = ms.XSTSAuthenticate(xblResponse); // Might throw even password correct

                string accessToken = mc.LoginWithXbox(xsts.UserHash, xsts.Token);
                bool hasGame = mc.UserHasGame(accessToken);
                if (hasGame)
                {
                    var profile = mc.GetUserProfile(accessToken);
                    session.PlayerName = profile.UserName;
                    session.PlayerID = profile.UUID;
                    session.ID = accessToken;
                    return Result.Ok(session);
                }

                return Result.Fail(new LoginFailure(LoginResult.NotPremium));
            }
            catch (Exception e)
            {
                SentrySdk.CaptureException(e);
                ConsoleIO.WriteLineFormatted("§cMicrosoft authenticate failed: " + e.Message);
                Serilog.Log.Debug(e.StackTrace);
                return Result.Fail(new LoginFailure(LoginResult.WrongPassword)); // Might not always be wrong password
            }
        }

        /// <summary>
        /// Validates whether accessToken must be refreshed
        /// </summary>
        /// <param name="session">Session token to validate</param>
        /// <returns>Returns the status of the token (Valid, Invalid, etc.)</returns>
        public static async Task<Result<LoginResult>> GetTokenValidation(ProxyHandler _proxyHandler, SessionToken session) {
            string json_request = "{\"accessToken\": \"" + JsonEncode(session.ID) + "\", \"clientToken\": \"" +
                                  JsonEncode(session.ClientID) + "\" }";
            var requestResult = await DoHTTPSPostAsync(_proxyHandler, "authserver.mojang.com", "/validate", json_request);
            if (requestResult.IsFailed)
                return Result.Fail(requestResult.Errors[0].Message);
            
            if (requestResult.Value.StatusCode == 204) {
                return Result.Ok(LoginResult.Success);
            }

            if (requestResult.Value.StatusCode == 403) {
                return Result.Ok(LoginResult.LoginRequired);
            }

            return Result.Ok(LoginResult.OtherError);
        }

        /// <summary>
        /// Refreshes invalid token
        /// </summary>
        /// <param name="user">Login</param>
        /// <param name="session">In case of successful token refresh, will contain session information for multiplayer</param>
        /// <returns>Returns the status of the new token request (Success, Failure, etc.)</returns>
        public static async Task<Result<SessionToken>> GetNewToken(ProxyHandler _proxyHandler, SessionToken currentsession) {
            var session = new SessionToken();
            string json_request = "{ \"accessToken\": \"" + JsonEncode(currentsession.ID) + "\", \"clientToken\": \"" +
                                  JsonEncode(currentsession.ClientID) + "\", \"selectedProfile\": { \"id\": \"" +
                                  JsonEncode(currentsession.PlayerID) + "\", \"name\": \"" +
                                  JsonEncode(currentsession.PlayerName) + "\" } }";
            var requestResult = await DoHTTPSPostAsync(_proxyHandler, "authserver.mojang.com", "/refresh", json_request);
            if (requestResult.IsFailed)
                return Result.Fail(requestResult.Errors[0].Message);

            if (requestResult.Value.StatusCode == 200) {
                Json.JSONData loginResponse = Json.ParseJson(requestResult.Value.Response);
                if (loginResponse.Properties.ContainsKey("accessToken")
                    && loginResponse.Properties.ContainsKey("selectedProfile")
                    && loginResponse.Properties["selectedProfile"].Properties.ContainsKey("id")
                    && loginResponse.Properties["selectedProfile"].Properties.ContainsKey("name")) {
                    session.ID = loginResponse.Properties["accessToken"].StringValue;
                    session.PlayerID = loginResponse.Properties["selectedProfile"].Properties["id"].StringValue;
                    session.PlayerName = loginResponse.Properties["selectedProfile"].Properties["name"].StringValue;
                    return Result.Ok(session);
                }
            }
            
            if (requestResult.Value.StatusCode == 403)
                return Result.Fail("Invalid Session Token");
            
            return Result.Fail(Translations.Get("error.auth", requestResult.Value.StatusCode));
        }

        /// <summary>
        /// Check session using Mojang's Yggdrasil authentication scheme. Allows to join an online-mode server
        /// </summary>
        /// <param name="user">Username</param>
        /// <param name="accesstoken">Session ID</param>
        /// <param name="serverhash">Server ID</param>
        /// <returns>TRUE if session was successfully checked</returns>
        public static async Task<Result> SessionCheckAsync(ProxyHandler _proxyHandler, string uuid, string accesstoken, string serverhash) {
            string json_request = "{\"accessToken\":\"" + accesstoken + "\",\"selectedProfile\":\"" + uuid +
                                  "\",\"serverId\":\"" + serverhash + "\"}";
            var result = await DoHTTPSPostAsync(_proxyHandler, "sessionserver.mojang.com", "/session/minecraft/join", json_request);
            if (result.IsFailed)
                return Result.Fail(result.Errors[0].Message);
            if (result.IsSuccess || result.IsFailed && result.Errors[1].Message == "200")
                return Result.Ok();

            return Result.Fail(Translations.Get("mcc.session_fail"));
        }

        /// <summary>
        /// Retrieve available Realms worlds of a player and display them
        /// </summary>
        /// <param name="username">Player Minecraft username</param>
        /// <param name="uuid">Player UUID</param>
        /// <param name="accesstoken">Access token</param>
        /// <returns>List of ID of available Realms worlds</returns>
        public static async Task<Result<List<string>>> RealmsListWorldsAsync(ProxyHandler _proxyHandler, string username, string uuid, string accesstoken)
        {
            string cookies = String.Format("sid=token:{0}:{1};user={2};version={3}", accesstoken, uuid, username, Const.MCHighestVersion);
            var getResult = await DoHTTPSGetAsync(_proxyHandler, "pc.realms.minecraft.net", "/worlds", cookies);
            if (getResult.IsFailed)
                return Result.Fail(getResult.Errors[0].Message);
            
            Json.JSONData realmsWorlds = Json.ParseJson(getResult.Value.Response);
            List<string> realmsWorldsResult = new List<string>(); // Store world ID
            if (realmsWorlds.Properties.ContainsKey("servers")
                && realmsWorlds.Properties["servers"].Type == Json.JSONData.DataType.Array
                && realmsWorlds.Properties["servers"].DataArray.Count > 0)
            {
                List<string> availableWorlds = new List<string>(); // Store string to print
                int index = 0;
                foreach (Json.JSONData realmsServer in realmsWorlds.Properties["servers"].DataArray)
                {
                    if (realmsServer.Properties.ContainsKey("name")
                        && realmsServer.Properties.ContainsKey("owner")
                        && realmsServer.Properties.ContainsKey("id")
                        && realmsServer.Properties.ContainsKey("expired"))
                    {
                        if (realmsServer.Properties["expired"].StringValue == "false")
                        {
                            availableWorlds.Add(String.Format("[{0}] {2} ({3}) - {1}",
                                index++,
                                realmsServer.Properties["id"].StringValue,
                                realmsServer.Properties["name"].StringValue,
                                realmsServer.Properties["owner"].StringValue));
                            realmsWorldsResult.Add(realmsServer.Properties["id"].StringValue);
                        }
                    }
                }
                if (availableWorlds.Count > 0)
                {
                    Translations.WriteLine("mcc.realms_available");
                    foreach (var world in availableWorlds)
                        ConsoleIO.WriteLine(world);
                    Translations.WriteLine("mcc.realms_join");
                }
            }

            return Result.Ok(realmsWorldsResult);
        }

        /// <summary>
        /// Get the server address of a Realms world by world ID
        /// </summary>
        /// <param name="worldId">The world ID of the Realms world</param>
        /// <param name="username">Player Minecraft username</param>
        /// <param name="uuid">Player UUID</param>
        /// <param name="accesstoken">Access token</param>
        /// <returns>Server address (host:port) or empty string if failure</returns>
        public static async Task<Result<string>> GetRealmsWorldServerAddressAsync(ProxyHandler _proxyHandler, string worldId, string username, string uuid, string accesstoken) {
            string cookies = String.Format("sid=token:{0}:{1};user={2};version={3}", accesstoken, uuid, username, Const.MCHighestVersion);
            var getAsync = await DoHTTPSGetAsync(_proxyHandler, "pc.realms.minecraft.net", "/worlds/v1/" + worldId + "/join/pc", cookies);
            if (getAsync.IsFailed)
                return Result.Fail(Translations.Get("error.realms.access_denied"));

            Json.JSONData serverAddress = Json.ParseJson(getAsync.Value.Response);
            if (serverAddress.Properties.ContainsKey("address"))
                return Result.Ok(serverAddress.Properties["address"].StringValue);
            return Result.Fail(Translations.Get("error.realms.ip_error"));
        }

        /// <summary>
        /// Make a HTTPS GET request to the specified endpoint of the Mojang API
        /// </summary>
        /// <param name="host">Host to connect to</param>
        /// <param name="endpoint">Endpoint for making the request</param>
        /// <param name="cookies">Cookies for making the request</param>
        /// <param name="result">Request result</param>
        /// <returns>HTTP Status code</returns>
        private static async Task<Result<HttpRequestResult>> DoHTTPSGetAsync(ProxyHandler _proxyHandler, string host, string endpoint, string cookies)
        {
            List<String> http_request = new List<string>();
            http_request.Add("GET " + endpoint + " HTTP/1.1");
            http_request.Add("Cookie: " + cookies);
            http_request.Add("Cache-Control: no-cache");
            http_request.Add("Pragma: no-cache");
            http_request.Add("Host: " + host);
            http_request.Add("User-Agent: Java/1.6.0_27");
            http_request.Add("Accept-Charset: ISO-8859-1,UTF-8;q=0.7,*;q=0.7");
            http_request.Add("Connection: close");
            http_request.Add("");
            http_request.Add("");
            return await DoHTTPSRequestAsync(_proxyHandler, http_request, host);
        }

        /// <summary>
        /// Make a HTTPS POST request to the specified endpoint of the Mojang API
        /// </summary>
        /// <param name="host">Host to connect to</param>
        /// <param name="endpoint">Endpoint for making the request</param>
        /// <param name="request">Request payload</param>
        /// <param name="result">Request result</param>
        /// <returns>HTTP Status code</returns>
        private static async Task<Result<HttpRequestResult>> DoHTTPSPostAsync(ProxyHandler _proxyHandler,string host, string endpoint, string request)
        {
            List<String> http_request = new List<string>();
            http_request.Add("POST " + endpoint + " HTTP/1.1");
            http_request.Add("Host: " + host);
            http_request.Add("User-Agent: MCC/" + Program.Version);
            http_request.Add("Content-Type: application/json");
            http_request.Add("Content-Length: " + Encoding.ASCII.GetBytes(request).Length);
            http_request.Add("Connection: close");
            http_request.Add("");
            http_request.Add(request);
            return await DoHTTPSRequestAsync(_proxyHandler, http_request, host);
        }

        /// <summary>
        /// Manual HTTPS request since we must directly use a TcpClient because of the proxy.
        /// This method connects to the server, enables SSL, do the request and read the response.
        /// </summary>
        /// <param name="headers">Request headers and optional body (POST)</param>
        /// <param name="host">Host to connect to</param>
        /// <param name="result">Request result</param>
        /// <returns>HTTP Status code</returns>
        private static async Task<Result<HttpRequestResult>> DoHTTPSRequestAsync(ProxyHandler _proxyHandler, List<string> headers, string host) {
            string? postResult = null;
            int statusCode = 520;

            Serilog.Log.Debug(Translations.Get("debug.request", host));

            var clientResult = await _proxyHandler.CreateTcpClient(host, 443, true);
            if (clientResult.IsFailed)
                return Result.Fail(clientResult.Errors[0].Message).WithError("520");

            SslStream stream = new SslStream(clientResult.Value.GetStream());
            await stream.AuthenticateAsClientAsync(host);

            foreach (string line in headers)
               Serilog.Log.Debug(line);

            await stream.WriteAsync(Encoding.ASCII.GetBytes(String.Join("\r\n", headers.ToArray())));
            System.IO.StreamReader sr = new System.IO.StreamReader(stream);
            string raw_result = await sr.ReadToEndAsync();

            foreach (string line in raw_result.Split('\n'))
                Serilog.Log.Debug(line);

            if (raw_result.StartsWith("HTTP/1.1")) {
                postResult = raw_result.Substring(raw_result.IndexOf("\r\n\r\n") + 4);
                statusCode = Settings.str2int(raw_result.Split(' ')[1]);
            }
            
            if (postResult == null)
                return Result.Fail($"Failed to connect to host {host}, server is returning {statusCode}").WithError(statusCode.ToString()); //Web server is returning an unknown error
            
            return Result.Ok(new HttpRequestResult(statusCode, postResult));
        }


        /// <summary>
        /// Encode a string to a json string.
        /// Will convert special chars to \u0000 unicode escape sequences.
        /// </summary>
        /// <param name="text">Source text</param>
        /// <returns>Encoded text</returns>
        private static string JsonEncode(string text)
        {
            StringBuilder result = new StringBuilder();

            foreach (char c in text)
            {
                if ((c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'z') ||
                    (c >= 'A' && c <= 'Z'))
                {
                    result.Append(c);
                }
                else
                {
                    result.AppendFormat(@"\u{0:x4}", (int)c);
                }
            }

            return result.ToString();
        }

        public struct ProtocolPingResult {
            public int ProtocolVersion;
            public ForgeInfo ForgeInfo;

            public ProtocolPingResult(int protocolVersion, ForgeInfo? forgeInfo) {
                ProtocolVersion = protocolVersion;
                ForgeInfo = forgeInfo;
            }
        }

        public struct HttpRequestResult {
            public int StatusCode;
            public string Response;

            public HttpRequestResult(int statusCode, string response) {
                Response = response;
                StatusCode = statusCode;
            }
        }
    }
}
