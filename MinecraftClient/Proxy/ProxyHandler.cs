using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using Polly;
using Sentry;
using Starksoft.Aspen.Proxy;

namespace MinecraftClient.Proxy {
    /// <summary>
    /// Automatically handle proxies according to the app Settings.
    /// Note: Underlying proxy handling is taken from Starksoft, LLC's Biko Library.
    /// This library is open source and provided under the MIT license. More info at biko.codeplex.com.
    /// </summary>

    public static class ProxyHandler {
        public enum Type {
            HTTP,
            SOCKS4,
            SOCKS4a,
            SOCKS5
        };

        private static ProxyClientFactory factory = new ProxyClientFactory();
        private static IProxyClient proxy;
        private static bool proxy_ok = false;

        /// <summary>
        /// Create a regular TcpClient or a proxied TcpClient according to the app Settings.
        /// </summary>
        /// <param name="host">Target host</param>
        /// <param name="port">Target port</param>
        /// <param name="login">True if the purpose is logging in to a Minecraft account</param>

        public static async Task<TcpClient?> newTcpClient(string host, int port, bool login = false) {
            TcpClient? client = null;
            if (login ? Settings.ProxyEnabledLogin : Settings.ProxyEnabledIngame) {
                ProxyType innerProxytype = ProxyType.Http;

                switch (Settings.proxyType) {
                    case Type.HTTP:
                        innerProxytype = ProxyType.Http;
                        break;
                    case Type.SOCKS4:
                        innerProxytype = ProxyType.Socks4;
                        break;
                    case Type.SOCKS4a:
                        innerProxytype = ProxyType.Socks4a;
                        break;
                    case Type.SOCKS5:
                        innerProxytype = ProxyType.Socks5;
                        break;
                }

                if (Settings.ProxyUsername != "" && Settings.ProxyPassword != "") {
                    proxy = factory.CreateProxyClient(innerProxytype, Settings.ProxyHost, Settings.ProxyPort, Settings.ProxyUsername, Settings.ProxyPassword);
                }
                else proxy = factory.CreateProxyClient(innerProxytype, Settings.ProxyHost, Settings.ProxyPort);

                proxy_ok = true;
                
                var retryPolicy = Policy.Handle<ProxyException>().WaitAndRetryAsync(3,
                    secondsUntilRetry => TimeSpan.FromSeconds(3),
                    (exception, span, retryCount, context) => {
                        ConsoleIO.WriteLine(
                            $"Failed to connect to proxy {Settings.ProxyUsername}@{Settings.ProxyHost}:{Settings.ProxyPort} with exception, retrying {(retryCount - 3) * -1} times... \n {exception.Message}");
                    });

                try {
                    return await retryPolicy.ExecuteAsync(() => {
                        return Task.Run(() => proxy.CreateConnection(host, port));
                    });
                }
                catch (ProxyException e) {
                    ConsoleIO.WriteLineFormatted("§8" + e.Message);
                    client = null;
                }

                if (client != null) {
                    ConsoleIO.WriteLineFormatted(Translations.Get("proxy.connected", Settings.ProxyHost, Settings.ProxyPort));
                }
            }
            else {

                try {
                    var tcpClient = new TcpClient();
                    tcpClient.ReceiveTimeout = 5000;
                    await tcpClient.ConnectAsync(host, port);
                    return tcpClient;
                }
                // todo handle gracefully
                catch (SocketException e) {
                    if (e.SocketErrorCode == SocketError.HostUnreachable)
                        Translations.WriteLineFormatted("error.connection_timeout");
                    if (e.SocketErrorCode == SocketError.HostNotFound)
                        ConsoleIO.WriteLine("The IP Address does not exist. Are you sure you typed it correctly?"); //TODO Translation
                }
            }

            return null;
        }
    }
}
