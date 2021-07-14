using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using MinecraftClient.Protocol;
using MinecraftClient.Protocol.Session;
using MinecraftClient.Proxy;
using Sentry;

namespace MinecraftClient {
    public class ClientDispatcher {
        private readonly SessionDispatcher sessionDispatcher;

        public ClientDispatcher(SessionDispatcher sessionDispatcher) {
            this.sessionDispatcher = sessionDispatcher;
        }

        public async Task<Result<McClient>> CreateClient(ProxyHandler handler, Settings settings, string username, string password, ProtocolHandler.AccountType accountType, ProtocolHandler.LoginMethod loginMethod) {
            var cts = new CancellationTokenSource();
            var getSessionResult = await sessionDispatcher.GetSession(username, password, accountType, loginMethod);
            if (getSessionResult.IsFailed)
                return Result.Fail("Cannot retrieve session!");
                
            var client = new McClient(handler, settings, getSessionResult.Value, cts.Token);
            return Result.Ok(client);
        }

        public async Task<Result<McClient>> CreateClientWithSession(ProxyHandler handler, Settings settings, SessionToken sessionToken) {
            var cts = new CancellationTokenSource();
            var client = new McClient(handler, settings, sessionToken, cts.Token);
            return Result.Ok(client);
        }
    }
}