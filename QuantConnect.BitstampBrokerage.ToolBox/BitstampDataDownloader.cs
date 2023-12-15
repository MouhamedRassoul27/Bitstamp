﻿/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2017 QuantConnect Corporation.
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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using QuantConnect.Data;
using System.Net;
using Newtonsoft.Json;
using NodaTime;
using QuantConnect.Brokerages.Bitstamp;
using QuantConnect.Configuration;
using QuantConnect.Data.Market;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.ToolBox.BitstampDownloader
{
    /// <summary>
    /// Bitstamp Data Downloader class
    /// </summary>
    public class BitstampDataDownloader : IDataDownloader
    {
        private readonly BitstampBrokerage _brokerage;
        private readonly BitstampSymbolMapper _symbolMapper = new ();

        public BitstampDataDownloader()
        {
            var tier = Config.Get("Bitstamp-verification-tier", "Starter");
            _brokerage = new BitstampBrokerage(null, null, tier, 10, null, null, null);
        }

        /// <summary>
        /// Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="dataDownloaderGetParameters">model class for passing in parameters for historical data</param>
        /// <returns>Enumerable of base data for this symbol</returns>
        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            var symbol = dataDownloaderGetParameters.Symbol;
            var resolution = dataDownloaderGetParameters.Resolution;
            var startUtc = dataDownloaderGetParameters.StartUtc;
            var endUtc = dataDownloaderGetParameters.EndUtc;
            var tickType = dataDownloaderGetParameters.TickType;

            if (tickType != TickType.Trade)
            {
                yield break;
            }

            if (endUtc < startUtc)
            {
                throw new ArgumentException("The end date must be greater or equal than the start date.");
            }

            if (!_symbolMapper.IsKnownLeanSymbol(symbol))
            {
                throw new ArgumentException($"The ticker {symbol.Value} is not available in Bitstamp. Use Lean symbols for downloader (i.e BTCUSD, not XXBTZUSD)");
            }

            var historyRequest = new HistoryRequest(
                startUtc,
                endUtc,
                typeof(TradeBar),
                symbol,
                resolution,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Utc),
                DateTimeZone.Utc,
                resolution,
                false,
                false,
                DataNormalizationMode.Adjusted,
                TickType.Trade);

            foreach (var baseData in _brokerage.GetHistory(historyRequest))
            {
                yield return baseData;
            }
        }

    }
}
