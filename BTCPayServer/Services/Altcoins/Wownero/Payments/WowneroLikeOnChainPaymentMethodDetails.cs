using BTCPayServer.Payments;

namespace BTCPayServer.Services.Altcoins.Wownero.Payments
{
    public class WowneroLikeOnChainPaymentMethodDetails
    {
        public long AccountIndex { get; set; }
        public long AddressIndex { get; set; }
        public long? InvoiceSettledConfirmationThreshold { get; set; }
    }
}
