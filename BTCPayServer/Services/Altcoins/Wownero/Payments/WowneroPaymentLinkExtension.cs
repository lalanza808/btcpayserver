#nullable enable
using System.Globalization;
using BTCPayServer.Payments;
using BTCPayServer.Plugins.Altcoins;
using BTCPayServer.Services.Invoices;
using Microsoft.AspNetCore.Mvc;

namespace BTCPayServer.Services.Altcoins.Wownero.Payments
{
    public class WowneroPaymentLinkExtension : IPaymentLinkExtension
    {
        private readonly WowneroLikeSpecificBtcPayNetwork _network;

        public WowneroPaymentLinkExtension(PaymentMethodId paymentMethodId, WowneroLikeSpecificBtcPayNetwork network)
        {
            PaymentMethodId = paymentMethodId;
            _network = network;
        }
        public PaymentMethodId PaymentMethodId { get; }

        public string? GetPaymentLink(PaymentPrompt prompt, IUrlHelper? urlHelper)
        {
            var due = prompt.Calculate().Due;
            return $"{_network.UriScheme}:{prompt.Destination}?tx_amount={due.ToString(CultureInfo.InvariantCulture)}";
        }
    }
}
