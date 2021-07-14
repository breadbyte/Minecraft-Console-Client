using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentResults;
using MinecraftClient.Protocol;
using MinecraftClient.Protocol.Session;
using MinecraftClient.Proxy;

namespace MinecraftClient {
    public class SessionDispatcher {
        private SessionCache? _sessionCache;
        private ProxyHandler _proxyHandler;

        public SessionDispatcher(CacheType cacheType, ProxyHandler proxyHandler) {
            if (cacheType == CacheType.None)
                return;
            
            _sessionCache = new SessionCache(cacheType);
            _proxyHandler = proxyHandler;
            
            var initCache = _sessionCache.InitializeDiskCache();
            Serilog.Log.Debug(initCache ? Translations.Get("debug.session_cache_ok") : Translations.Get("debug.session_cache_fail"));
        }

        public async Task<Result<SessionToken>> GetSession(string username, string password, ProtocolHandler.AccountType accountType, ProtocolHandler.LoginMethod loginMethod) {
            var cache = await GetCachedSession(username);
            if (cache.IsSuccess)
                return cache;

            if (string.IsNullOrWhiteSpace(password)) {
                return Result.Ok(await CreateOfflineSession(username));
            }

            var newSession = await CreateYggdrasilSession(username, password, accountType, loginMethod);
            if (newSession.IsSuccess)
                return newSession;

            string failureMessage = Translations.Get("error.login");
            string failureReason = "";

            switch (((LoginFailure)newSession.Errors[0]).Result) {
                case ProtocolHandler.LoginResult.AccountMigrated: failureReason = "error.login.migrated"; break;
                case ProtocolHandler.LoginResult.ServiceUnavailable: failureReason = "error.login.server"; break;
                case ProtocolHandler.LoginResult.WrongPassword: failureReason = "error.login.blocked"; break;
                case ProtocolHandler.LoginResult.InvalidResponse: failureReason = "error.login.response"; break;
                case ProtocolHandler.LoginResult.NotPremium: failureReason = "error.login.premium"; break;
                case ProtocolHandler.LoginResult.OtherError: failureReason = "error.login.network"; break;
                case ProtocolHandler.LoginResult.SSLError: failureReason = "error.login.ssl"; break;
                case ProtocolHandler.LoginResult.UserCancel: failureReason = "error.login.cancel"; break;
                default: failureReason = "error.login.unknown"; break;
            }

            failureMessage += Translations.Get(failureReason);

            return Result.Fail(failureMessage);
        }

        public async Task<SessionToken> CreateOfflineSession(string username) {
            var session = new SessionToken();
            Translations.WriteLineFormatted("mcc.offline");
            session.PlayerID = "0";
            session.PlayerName = username;
            return session;
        }

        private async Task<Result<SessionToken>> CreateYggdrasilSession(string username, string password, ProtocolHandler.AccountType accountType, ProtocolHandler.LoginMethod loginMethod) {
            Translations.WriteLine("mcc.connecting", accountType == ProtocolHandler.AccountType.Mojang ? "Minecraft.net" : "Microsoft");
            var result = await ProtocolHandler.GetLogin(_proxyHandler, username, password, accountType, loginMethod);
            
            return result;
        }

        public async Task<Result<SessionToken>> GetCachedSession(string username) {
            if (_sessionCache == null)
                return Result.Fail(Translations.Get("cache.read_fail_plain", "SessionDispatcher does not have caching enabled!"));
            if (_sessionCache.Contains(username.ToLower())) {
                var session = _sessionCache.GetCachedSession(username.ToLower());
                var result = await ProtocolHandler.GetTokenValidation(_proxyHandler, session);
                if (result.Value != ProtocolHandler.LoginResult.Success) {
                    return Result.Fail(Translations.Get("mcc.session_invalid")).WithError(new LoginFailure(result.Value));
                }
                
                ConsoleIO.WriteLineFormatted(Translations.Get("mcc.session_valid", session.PlayerName));
                return Result.Ok(session);
            }

            return Result.Fail(Translations.Get("mcc.session_invalid"));
        }
    }

    public class LoginFailure : Error {
        public readonly ProtocolHandler.LoginResult Result;

        public LoginFailure(ProtocolHandler.LoginResult loginResult) : base() {
            this.Result = loginResult;
        }
    }
}