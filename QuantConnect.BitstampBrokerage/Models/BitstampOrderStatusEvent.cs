namespace QuantConnect.Brokerages.Bitstamp.Models
{
    public class BitstampOrderStatusEvent : BitstampTradeExecutionEvent
    {
        /// <summary>
        /// Last update timestamp
        /// </summary>
        public string LastUpdated { get; set; }
        
        /// <summary>
        /// Status of order
        /// pending = order pending book entry
        /// open = open order
        /// closed = closed order
        /// canceled = order canceled
        /// expired = order expired
        /// </summary>
        public string Status { get; set; }
    }
}