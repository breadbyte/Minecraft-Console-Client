using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentResults;
using Sentry;

namespace MinecraftClient {
    static class ClientDispatcher {

        public static async Task<Result<McClient>> CreateClient(string username, string uuid, string sessionID) {
            var cts = new CancellationTokenSource();
            var getSessionResult = await SessionDispatcher.GetSession();
            if (getSessionResult.IsFailed)
                return Result.Fail("Cannot retrieve session!");
                
            var client = new McClient(getSessionResult.Value, cts.Token);
            return Result.Ok(client);
        }
    }
}