using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.Threading.Tasks;
using FluentResults;
using Polly;
using Sentry;
using Starksoft.Aspen.Proxy;
using AspenProxyType = Starksoft.Aspen.Proxy.ProxyType;

namespace MinecraftClient.Proxy {
    /// <summary>
    /// Automatically handle proxies according to the app Settings.
    /// Note: Underlying proxy handling is taken from Starksoft, LLC's Biko Library.
    /// This library is open source and provided under the MIT license. More info at biko.codeplex.com.
    /// </summary>

    public class ProxyHandler {
        public enum MCCProxyType {
            HTTP,
            SOCKS4,
            SOCKS4a,
            SOCKS5
        };

        private ProxyClientFactory factory = new ProxyClientFactory();
        private IProxyClient proxy;
        private bool proxy_ok = false;
        private Settings.ProxySettings _proxySettings;
        
        public ProxyHandler(Settings.ProxySettings proxySettings) {
            _proxySettings = proxySettings;
        }

        /// <summary>
        /// Create a regular TcpClient or a proxied TcpClient according to the app Settings.
        /// </summary>
        /// <param name="host">Target host</param>
        /// <param name="port">Target port</param>
        /// <param name="login">True if the purpose is logging in to a Minecraft account</param>
        public async Task<Result<TcpClient>> CreateTcpClient(string host, int port, bool login = false) {
            TcpClient? client = null;

            // Is the login bool true? Then use the proxy value for EnabledLogin (Use proxy for proxying to Microsoft/Mojang)
            // Is the login bool false? Then use the proxy value for EnabledIngame (Use proxy for connecting to a server)
            if (login ? _proxySettings.ProxyEnabledLogin : _proxySettings.ProxyEnabledInGame) {
                var aspenProxyType = AspenProxyType.Http;

                switch (_proxySettings.ProxyType) {
                    case MCCProxyType.HTTP:
                        aspenProxyType = AspenProxyType.Http;
                        break;
                    case MCCProxyType.SOCKS4:
                        aspenProxyType = AspenProxyType.Socks4;
                        break;
                    case MCCProxyType.SOCKS4a:
                        aspenProxyType = AspenProxyType.Socks4a;
                        break;
                    case MCCProxyType.SOCKS5:
                        aspenProxyType = AspenProxyType.Socks5;
                        break;
                }

                if (!string.IsNullOrWhiteSpace(_proxySettings.ProxyUsername) &&
                    !string.IsNullOrWhiteSpace(_proxySettings.ProxyUsername)) {
                    proxy = factory.CreateProxyClient(aspenProxyType, _proxySettings.ProxyHost,
                        _proxySettings.ProxyPort, _proxySettings.ProxyUsername, _proxySettings.ProxyPassword);
                }
                else
                    proxy = factory.CreateProxyClient(aspenProxyType, _proxySettings.ProxyHost,
                        _proxySettings.ProxyPort);

                proxy_ok = true;

                // Set our retry policy.
                var retryPolicy = Policy.Handle<ProxyException>().WaitAndRetryAsync(3,
                    secondsUntilRetry => TimeSpan.FromSeconds(3),
                    (exception, span, retryCount, context) => {
                        ConsoleIO.WriteLine(
                            $"Failed to connect to proxy {_proxySettings.ProxyUsername}@{_proxySettings.ProxyHost}:{_proxySettings.ProxyPort} with exception, retrying {(retryCount - 3) * -1} times... \n {exception.Message}");
                    });

                // Attempt connecting to the specified proxy. Will retry according to the retry policy.
                try {
                    return await retryPolicy.ExecuteAsync<Result<TcpClient>>(async () => {
                        var result = await Task.Run(() => proxy.CreateConnection(host, port));
                        if (result == null)
                            return Result.Fail($"Failed to connect to host {host} with port {port}");
                        return Result.Ok(result);
                    });
                }
                catch (ProxyException e) {
                    return Result
                           .Fail(Translations.Get("proxy.connected", _proxySettings.ProxyHost, _proxySettings.ProxyPort)).WithError(e.Message);
                }
            }


            try {
                var tcpClient = new TcpClient();
                tcpClient.ReceiveTimeout = 5000;
                await tcpClient.ConnectAsync(host, port);
                return Result.Ok(tcpClient);
            }
            catch (SocketException e) {
                if (e.SocketErrorCode == SocketError.HostUnreachable)
                    Translations.WriteLineFormatted("error.connection_timeout");
                if (e.SocketErrorCode == SocketError.HostNotFound)
                    ConsoleIO.WriteLine("The IP Address does not exist. Are you sure you typed it correctly?"); //TODO Translation
            }

            return Result.Fail("Failed to create a TcpClient");
        }

        public Settings.ProxySettings GetProxySettings() {
            return _proxySettings;
        }
    }
}
