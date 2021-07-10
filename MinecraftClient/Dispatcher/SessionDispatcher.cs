using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentResults;
using MinecraftClient.Protocol;
using MinecraftClient.Protocol.Session;

namespace MinecraftClient {
    public static class SessionDispatcher {
        public static async Task<Result<SessionToken>> GetSession() {
            var cache = await GetCachedSession();

            if (cache.IsSuccess)
                return cache;

            if (Settings.Password == "-") {
                return Result.Ok(await CreateOfflineSession());
            }

            var newSession = await CreateYggdrasilSession();

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

        public static async Task<SessionToken> CreateOfflineSession() {
            var session = new SessionToken();
            Translations.WriteLineFormatted("mcc.offline");
            session.PlayerID = "0";
            session.PlayerName = Settings.Login;
            return session;
        }

        private static async Task<Result<SessionToken>> CreateYggdrasilSession() {
            Translations.WriteLine("mcc.connecting", Settings.AccountType == ProtocolHandler.AccountType.Mojang ? "Minecraft.net" : "Microsoft");
            var result = await ProtocolHandler.GetLogin(Settings.Login, Settings.Password, Settings.AccountType);
            
            if (result.IsSuccess && Settings.SessionCaching != CacheType.None) {
                SessionCache.Store(Settings.Login.ToLower(), result.Value);
                return result;
            }

            return result;
        }

        private static async Task<Result<SessionToken>> GetCachedSession() {
            if (Settings.SessionCaching != CacheType.None && SessionCache.Contains(Settings.Login.ToLower())) {
                var session = SessionCache.Get(Settings.Login.ToLower());
                var result = await ProtocolHandler.GetTokenValidation(session);
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