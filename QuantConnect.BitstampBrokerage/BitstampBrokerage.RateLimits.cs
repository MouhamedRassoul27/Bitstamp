/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Timers;
using System.Threading;
using QuantConnect.Util;
using QuantConnect.Logging;
using System.Collections.Generic;
using Timer = System.Timers.Timer;
using QuantConnect.Brokerages.Bitstamp.Models;

namespace QuantConnect.Brokerages.Bitstamp
{
    /// <summary>
    /// Bitstamp rate limits implementation
    /// </summary>
    public class BitstampBrokerageRateLimits : IDisposable
    {
        private readonly Timer _1sRateLimitTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Dictionary<Symbol, int> _rateLimitsOrderPerSymbolDictionary;
        private readonly Dictionary<string, decimal> _rateLimitsCancelPerSymbolDictionary;

        /// <summary>
        /// Lockers to make code thread safe
        /// </summary>
        private readonly object RestLocker = new();
        private readonly object OrderLocker = new();
        private readonly object CancelLocker = new();
        
        // By default - Starter verification tier limits
        private readonly Dictionary<BitstampRateLimitType, decimal> _rateLimitsDictionary = new ()
        {
            {BitstampRateLimitType.Common, 15},
            {BitstampRateLimitType.Orders, 60},
            {BitstampRateLimitType.Cancel, 60}
        };

        private readonly Dictionary<BitstampRateLimitType, decimal> _rateLimitsDecayDictionary = new ()
        {
            {BitstampRateLimitType.Common, 0.33m},
            {BitstampRateLimitType.Cancel, 1m},
        };

        /// <summary>
        /// The rest rate limit counter
        /// </summary>
        protected decimal RateLimitCounter { get; private set; }

        /// <summary>
        /// Event that fires when an error is encountered in the brokerage
        /// </summary>
        public event EventHandler<BrokerageMessageEvent> Message;

        /// <summary>
        /// Choosing right rate limits based on verification tier
        /// </summary>
        /// <param name="verificationTier">Starter, Intermediate, Pro</param>
        /// <param name="timerInterval">Rate limit timer interval, useful for testing</param>
        public BitstampBrokerageRateLimits(string verificationTier, int timerInterval = 1000)
        {
            _1sRateLimitTimer = new Timer(timerInterval);
            _cancellationTokenSource = new CancellationTokenSource();
            _rateLimitsOrderPerSymbolDictionary = new Dictionary<Symbol, int>();
            _rateLimitsCancelPerSymbolDictionary = new Dictionary<string, decimal>();

            Enum.TryParse(verificationTier, true, out BitstampVerificationTier tier);
            
            switch (tier)
            {
                case BitstampVerificationTier.Starter:
                    _rateLimitsDictionary[BitstampRateLimitType.Common] = 15;
                    _rateLimitsDictionary[BitstampRateLimitType.Orders] = 60;
                    _rateLimitsDictionary[BitstampRateLimitType.Cancel] = 60;
                    _rateLimitsDecayDictionary[BitstampRateLimitType.Common] = 0.33m;
                    _rateLimitsDecayDictionary[BitstampRateLimitType.Cancel] = 1m;
                    break;
                case BitstampVerificationTier.Intermediate:
                    _rateLimitsDictionary[BitstampRateLimitType.Common] = 20;
                    _rateLimitsDictionary[BitstampRateLimitType.Orders] = 80;
                    _rateLimitsDictionary[BitstampRateLimitType.Cancel] = 125;
                    _rateLimitsDecayDictionary[BitstampRateLimitType.Common] = 0.5m;
                    _rateLimitsDecayDictionary[BitstampRateLimitType.Cancel] = 2.34m;
                    break;
                case BitstampVerificationTier.Pro:
                    _rateLimitsDictionary[BitstampRateLimitType.Common] = 20;
                    _rateLimitsDictionary[BitstampRateLimitType.Orders] = 225;
                    _rateLimitsDictionary[BitstampRateLimitType.Cancel] = 180;
                    _rateLimitsDecayDictionary[BitstampRateLimitType.Common] = 1;
                    _rateLimitsDecayDictionary[BitstampRateLimitType.Cancel] = 3.75m;
                    break;
                default:
                    throw new ArgumentException($"Unexpected rate limit tier {tier}");
            }
            
            _1sRateLimitTimer.Elapsed += DecaySpotRateLimits;
            _1sRateLimitTimer.Start();
        }

        /// <summary>
        /// Usual Rest request rate limit check
        /// </summary>
        public void RateLimitCheck()
        {
            bool isExceeded;
            lock (RestLocker)
            {
                isExceeded = ++RateLimitCounter > _rateLimitsDictionary[BitstampRateLimitType.Common];
            }

            if (isExceeded)
            {
                Message?.Invoke(this, new BrokerageMessageEvent(BrokerageMessageType.Warning, "RestRateLimit",
                    $"The API request has been rate limited. To avoid this message, please reduce the frequency of API calls. Will wait {GetRateLimitedWait()}..."));

                _cancellationTokenSource.Token.WaitHandle.WaitOne(GetRateLimitedWait());
            }
        }

        /// <summary>
        /// Bitstamp order rate
        /// </summary>
        /// <param name="symbol">Brokerage symbol</param>
        /// <exception cref="BrokerageException"> Rate limit exceeded</exception>
        public void OrderRateLimitCheck(Symbol symbol)
        {
            int currentOrdersCount;
            lock (OrderLocker)
            {
                _rateLimitsOrderPerSymbolDictionary.TryGetValue(symbol, out currentOrdersCount);
                _rateLimitsOrderPerSymbolDictionary[symbol] = ++currentOrdersCount;
            }

            if (currentOrdersCount >= _rateLimitsDictionary[BitstampRateLimitType.Orders])
            {
                Message?.Invoke(this, new BrokerageMessageEvent(BrokerageMessageType.Error, "RateLimit",
                    $"Placing new orders of {symbol} symbol is not allowed. Your order limit: {_rateLimitsDictionary[BitstampRateLimitType.Orders]}, opened orders now: {currentOrdersCount}. " +
                    "Cancel orders to have ability to place new."));

                throw new BrokerageException("Order placing limit is exceeded. Cancel open orders and then place new ones.");
            }
        }
        
        /// <summary>
        /// Bitstamp order rate decay
        /// </summary>
        /// <param name="symbol">Brokerage symbol</param>
        public void OrderRateLimitDecay(Symbol symbol)
        {
            lock (OrderLocker)
            {
                if (_rateLimitsOrderPerSymbolDictionary.TryGetValue(symbol, out var currentOrdersCount))
                {
                    if (currentOrdersCount <= 0)
                    {
                        _rateLimitsOrderPerSymbolDictionary[symbol] = 0;
                    }
                    else
                    {
                        _rateLimitsOrderPerSymbolDictionary[symbol]--;
                    }
                }
            }
        }
        
        /// <summary>
        /// Bitstamp Cancel Order Rate limit check
        /// </summary>
        /// <param name="symbol">Brokerage symbol</param>
        /// <param name="time">Time order was placed</param>
        public void CancelOrderRateLimitCheck(string symbol, DateTime time)
        {
            var weight = GetRateLimitWeightCancelOrder(time);
            decimal currentCancelOrderRate;
            
            lock (CancelLocker)
            {
                _rateLimitsCancelPerSymbolDictionary.TryGetValue(symbol, out currentCancelOrderRate);
                currentCancelOrderRate += weight;
                _rateLimitsCancelPerSymbolDictionary[symbol] = currentCancelOrderRate;
            }
            
            if (currentCancelOrderRate >= _rateLimitsDictionary[BitstampRateLimitType.Cancel])
            {
                Message?.Invoke(this, new BrokerageMessageEvent(BrokerageMessageType.Warning, "CancelRateLimit",
                    $"The cancel order API request has been rate limited. To avoid this message, please reduce the frequency of cancel order API calls. Will wait {GetRateLimitedWait()}..."));

                _cancellationTokenSource.Token.WaitHandle.WaitOne(GetRateLimitedWait());
            }
        }

        /// <summary>
        /// Returns the gate limit wait time
        /// </summary>
        /// <remarks>Useful for faster testing</remarks>
        protected virtual TimeSpan GetRateLimitedWait()
        {
            return TimeSpan.FromSeconds(20);
        }

        private int GetRateLimitWeightCancelOrder(DateTime time)
        {
            var timePassed = DateTime.UtcNow - time;
            if (timePassed < TimeSpan.FromSeconds(5))
            {
                return 8;
            }

            if (timePassed < TimeSpan.FromSeconds(10))
            {
                return 6;
            }

            if (timePassed < TimeSpan.FromSeconds(15))
            {
                return 5;
            }

            if (timePassed < TimeSpan.FromSeconds(45))
            {
                return 4;
            }

            if (timePassed < TimeSpan.FromSeconds(90))
            {
                return 2;
            }

            if (timePassed < TimeSpan.FromSeconds(900))
            {
                return 1;
            }

            return 0;
        }
        
        private void DecaySpotRateLimits(object o, ElapsedEventArgs agrs)
        {
            try
            {
                lock (RestLocker)
                {
                    RateLimitCounter = Math.Max(0, RateLimitCounter - _rateLimitsDecayDictionary[BitstampRateLimitType.Common]);
                }

                var cancelDecay = _rateLimitsDecayDictionary[BitstampRateLimitType.Cancel];
                lock (CancelLocker)
                {
                    foreach (var key in _rateLimitsCancelPerSymbolDictionary.Keys)
                    {
                        _rateLimitsCancelPerSymbolDictionary[key] = Math.Max(0, _rateLimitsCancelPerSymbolDictionary[key] - cancelDecay);
                    }
                }
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        /// <summary>
        /// Dispose Bitstamp rateLimits
        /// </summary>
        public void Dispose()
        {
            _cancellationTokenSource.Cancel();
            _1sRateLimitTimer?.Stop();
            _1sRateLimitTimer.DisposeSafely();
        }
    }
}