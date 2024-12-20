using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AngleSharp.Dom;
using BTCPayServer.Abstractions.Extensions;
using BTCPayServer.BIP78.Sender;
using BTCPayServer.Data;
using BTCPayServer.Logging;
using BTCPayServer.Models;
using BTCPayServer.Models.InvoicingModels;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Altcoins;
using BTCPayServer.Rating;
using BTCPayServer.Services.Altcoins.Wownero.RPC.Models;
using BTCPayServer.Services.Altcoins.Wownero.Services;
using BTCPayServer.Services.Altcoins.Wownero.Utils;
using BTCPayServer.Services.Altcoins.Zcash.Payments;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using NBitcoin;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BTCPayServer.Services.Altcoins.Wownero.Payments
{
    public class WowneroLikePaymentMethodHandler : IPaymentMethodHandler
    {
        private readonly WowneroLikeSpecificBtcPayNetwork _network;
        public WowneroLikeSpecificBtcPayNetwork Network => _network;
        public JsonSerializer Serializer { get; }
        private readonly WowneroRPCProvider _wowneroRpcProvider;

        public PaymentMethodId PaymentMethodId { get; }

        public WowneroLikePaymentMethodHandler(WowneroLikeSpecificBtcPayNetwork network, WowneroRPCProvider wowneroRpcProvider)
        {
            PaymentMethodId = PaymentTypes.CHAIN.GetPaymentMethodId(network.CryptoCode);
            _network = network;
            Serializer = BlobSerializer.CreateSerializer().Serializer;
            _wowneroRpcProvider = wowneroRpcProvider;
        }

        public Task BeforeFetchingRates(PaymentMethodContext context)
        {
            context.Prompt.Currency = _network.CryptoCode;
            context.Prompt.Divisibility = _network.Divisibility;
            if (context.Prompt.Activated)
            {
                var supportedPaymentMethod = ParsePaymentMethodConfig(context.PaymentMethodConfig);
                var walletClient = _wowneroRpcProvider.WalletRpcClients[_network.CryptoCode];
                var daemonClient = _wowneroRpcProvider.DaemonRpcClients[_network.CryptoCode];
                context.State = new Prepare()
                {
                    GetFeeRate = daemonClient.SendCommandAsync<GetFeeEstimateRequest, GetFeeEstimateResponse>("get_fee_estimate", new GetFeeEstimateRequest()),
                    ReserveAddress = s => walletClient.SendCommandAsync<CreateAddressRequest, CreateAddressResponse>("create_address", new CreateAddressRequest() { Label = $"btcpay invoice #{s}", AccountIndex = supportedPaymentMethod.AccountIndex }),
                    AccountIndex = supportedPaymentMethod.AccountIndex
                };
            }
            return Task.CompletedTask;
        }

        public async Task ConfigurePrompt(PaymentMethodContext context)
        {
            if (!_wowneroRpcProvider.IsAvailable(_network.CryptoCode))
                throw new PaymentMethodUnavailableException($"Node or wallet not available");
            var invoice = context.InvoiceEntity;
            Prepare wowneroPrepare = (Prepare)context.State;
            var feeRatePerKb = await wowneroPrepare.GetFeeRate;
            var address = await wowneroPrepare.ReserveAddress(invoice.Id);

            var feeRatePerByte = feeRatePerKb.Fee / 1024;
            var details = new WowneroLikeOnChainPaymentMethodDetails()
            {
                AccountIndex = wowneroPrepare.AccountIndex,
                AddressIndex = address.AddressIndex,
                InvoiceSettledConfirmationThreshold = ParsePaymentMethodConfig(context.PaymentMethodConfig).InvoiceSettledConfirmationThreshold
            };
            context.Prompt.Destination = address.Address;
            context.Prompt.PaymentMethodFee = WowneroMoney.Convert(feeRatePerByte * 100);
            context.Prompt.Details = JObject.FromObject(details, Serializer);
            context.TrackedDestinations.Add(address.Address);
        }
        private WowneroPaymentPromptDetails ParsePaymentMethodConfig(JToken config)
        {
            return config.ToObject<WowneroPaymentPromptDetails>(Serializer) ?? throw new FormatException($"Invalid {nameof(WowneroLikePaymentMethodHandler)}");
        }
        object IPaymentMethodHandler.ParsePaymentMethodConfig(JToken config)
        {
            return ParsePaymentMethodConfig(config);
        }

        class Prepare
        {
            public Task<GetFeeEstimateResponse> GetFeeRate;
            public Func<string, Task<CreateAddressResponse>> ReserveAddress;

            public long AccountIndex { get; internal set; }
        }

        public WowneroLikeOnChainPaymentMethodDetails ParsePaymentPromptDetails(Newtonsoft.Json.Linq.JToken details)
        {
            return details.ToObject<WowneroLikeOnChainPaymentMethodDetails>(Serializer);
        }
        object IPaymentMethodHandler.ParsePaymentPromptDetails(Newtonsoft.Json.Linq.JToken details)
        {
            return ParsePaymentPromptDetails(details);
        }

        public WowneroLikePaymentData ParsePaymentDetails(JToken details)
        {
            return details.ToObject<WowneroLikePaymentData>(Serializer) ?? throw new FormatException($"Invalid {nameof(WowneroLikePaymentMethodHandler)}");
        }
        object IPaymentMethodHandler.ParsePaymentDetails(JToken details)
        {
            return ParsePaymentDetails(details);
        }
    }
}
