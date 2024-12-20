using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Services;
using System.Net.Http;
using System.Net;
using BTCPayServer.Hosting;
using BTCPayServer.Payments;
using BTCPayServer.Payments.Bitcoin;
using BTCPayServer.Services.Altcoins.Wownero.Configuration;
using BTCPayServer.Services.Altcoins.Wownero.Payments;
using BTCPayServer.Services.Altcoins.Wownero.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using NBitcoin;
using BTCPayServer.Configuration;
using System.Linq;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BTCPayServer.Plugins.Altcoins;

public partial class AltcoinsPlugin
{
    public void InitWownero(IServiceCollection services)
    {
        var network = new WowneroLikeSpecificBtcPayNetwork()
        {
            CryptoCode = "WOW",
            DisplayName = "Wownero",
            Divisibility = 11,
            DefaultRateRules = new[]
            {
                "WOW_X = WOW_BTC * BTC_X;",
                "WOW_BTC = trade_ogre(WOW_BTC);",
                "WOW_USD = kraken(BTC_USD) * WOW_BTC;",
            },
            CryptoImagePath = "/imlegacy/wownero.svg",
            UriScheme = "wownero"
        };
        var blockExplorerLink = ChainName == ChainName.Mainnet
                    ? "https://explorer.suchwow.xyz/tx/{0}"
                    : "https://explore.wownero.com/tx/{0}";
        var pmi = PaymentTypes.CHAIN.GetPaymentMethodId("WOW");
        services.AddDefaultPrettyName(pmi, network.DisplayName);
        services.AddBTCPayNetwork(network)
                .AddTransactionLinkProvider(pmi, new SimpleTransactionLinkProvider(blockExplorerLink));


        services.AddSingleton(provider =>
                ConfigureWowneroLikeConfiguration(provider));
        services.AddHttpClient("WOWclient")
            .ConfigurePrimaryHttpMessageHandler(provider =>
            {
                var configuration = provider.GetRequiredService<WowneroLikeConfiguration>();
                if (!configuration.WowneroLikeConfigurationItems.TryGetValue("WOW", out var wowConfig) || wowConfig.Username is null || wowConfig.Password is null)
                {
                    return new HttpClientHandler();
                }
                return new HttpClientHandler
                {
                    Credentials = new NetworkCredential(wowConfig.Username, wowConfig.Password),
                    PreAuthenticate = true
                };
            });
        services.AddSingleton<WowneroRPCProvider>();
        services.AddHostedService<WowneroLikeSummaryUpdaterHostedService>();
        services.AddHostedService<WowneroListener>();
        services.AddSingleton<IPaymentMethodHandler>(provider =>
                (IPaymentMethodHandler)ActivatorUtilities.CreateInstance(provider, typeof(WowneroLikePaymentMethodHandler), new object[] { network }));
        services.AddSingleton<IPaymentLinkExtension>(provider =>
(IPaymentLinkExtension)ActivatorUtilities.CreateInstance(provider, typeof(WowneroPaymentLinkExtension), new object[] { network, pmi }));
        services.AddSingleton<ICheckoutModelExtension>(provider =>
(ICheckoutModelExtension)ActivatorUtilities.CreateInstance(provider, typeof(WowneroCheckoutModelExtension), new object[] { network, pmi }));

        services.AddUIExtension("store-nav", "Wownero/StoreNavWowneroExtension");
        services.AddUIExtension("store-wallets-nav", "Wownero/StoreWalletsNavWowneroExtension");
        services.AddUIExtension("store-invoices-payments", "Wownero/ViewWowneroLikePaymentData");
        services.AddSingleton<ISyncSummaryProvider, WowneroSyncSummaryProvider>();
    }
    private static WowneroLikeConfiguration ConfigureWowneroLikeConfiguration(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetService<IConfiguration>();
        var btcPayNetworkProvider = serviceProvider.GetService<BTCPayNetworkProvider>();
        var result = new WowneroLikeConfiguration();

        var supportedNetworks = btcPayNetworkProvider.GetAll()
            .OfType<WowneroLikeSpecificBtcPayNetwork>();

        foreach (var wowneroLikeSpecificBtcPayNetwork in supportedNetworks)
        {
            var daemonUri =
                configuration.GetOrDefault<Uri>($"{wowneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_uri",
                    null);
            var walletDaemonUri =
                configuration.GetOrDefault<Uri>(
                    $"{wowneroLikeSpecificBtcPayNetwork.CryptoCode}_wallet_daemon_uri", null);
            var walletDaemonWalletDirectory =
                configuration.GetOrDefault<string>(
                    $"{wowneroLikeSpecificBtcPayNetwork.CryptoCode}_wallet_daemon_walletdir", null);
            var daemonUsername =
                configuration.GetOrDefault<string>(
                    $"{wowneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_username", null);
            var daemonPassword =
                configuration.GetOrDefault<string>(
                    $"{wowneroLikeSpecificBtcPayNetwork.CryptoCode}_daemon_password", null);
            if (daemonUri == null || walletDaemonUri == null || walletDaemonWalletDirectory == null)
            {
                throw new ConfigException($"{wowneroLikeSpecificBtcPayNetwork.CryptoCode} is misconfigured");
            }

            result.WowneroLikeConfigurationItems.Add(wowneroLikeSpecificBtcPayNetwork.CryptoCode, new WowneroLikeConfigurationItem()
            {
                DaemonRpcUri = daemonUri,
                Username = daemonUsername,
                Password = daemonPassword,
                InternalWalletRpcUri = walletDaemonUri,
                WalletDirectory = walletDaemonWalletDirectory
            });
        }
        return result;
    }
}

