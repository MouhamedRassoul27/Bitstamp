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
using NUnit.Framework;
using QuantConnect.Brokerages.Bitstamp;

namespace QuantConnect.Tests.Brokerages.Bitstamp
{

    public class BitstampBrokerageSymbolMapperTests
    {
        private readonly BitstampSymbolMapper _symbolMapper = new BitstampSymbolMapper();
        private static TestCaseData[] CorrectBrokerageSymbolTestParameters
        {
            get
            {
                return new[]
                {
                    new TestCaseData(Symbol.Create("EURUSD", SecurityType.Crypto, Market.Bitstamp), "ZEURZUSD", false),
                    new TestCaseData(Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Bitstamp), "XXBTZUSD", false),
                    new TestCaseData(Symbol.Create("ETHUSDT", SecurityType.Crypto, Market.Bitstamp), "ETHUSDT", false),
                    new TestCaseData(Symbol.Create("XRPBTC", SecurityType.Crypto, Market.Bitstamp), "XXRPXXBT", false),
                    new TestCaseData(Symbol.Create("ETHBTC", SecurityType.Crypto, Market.Bitstamp), "XETHXXBT", false),
                    new TestCaseData(Symbol.Create("EURBTC", SecurityType.Crypto, Market.Bitstamp), "", true), // no such a ticker on Bitstamp
                    new TestCaseData(Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Binance), "XXBTZUSD", true), // wrong Market
                    new TestCaseData(Symbol.Create("ETHUSDT", SecurityType.Future, Market.Bitstamp), "ETHUSDT", true), // wrong SecurityType
                };
            }
        }
        
        private static TestCaseData[] CorrectLeanSymbolTestParameters
        {
            get
            {
                return new[]
                {
                    new TestCaseData("ZEURZUSD", Symbol.Create("EURUSD", SecurityType.Crypto, Market.Bitstamp), false),
                    new TestCaseData("XXBTZUSD", Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Bitstamp), false),
                    new TestCaseData("ETHUSDT", Symbol.Create("ETHUSDT", SecurityType.Crypto, Market.Bitstamp), false),
                    new TestCaseData("XXRPXXBT", Symbol.Create("XRPBTC", SecurityType.Crypto, Market.Bitstamp), false),
                    new TestCaseData("XETHXXBT", Symbol.Create("ETHBTC", SecurityType.Crypto, Market.Bitstamp), false),
                    new TestCaseData("", Symbol.Create("EURBTC", SecurityType.Crypto, Market.Bitstamp), true), // no such a ticker on Bitstamp
                };
            }
        }
        
        private static TestCaseData[] WsSymbolTestParameters
        {
            get
            {
                return new[]
                {
                    new TestCaseData(Symbol.Create("EURUSD", SecurityType.Crypto, Market.Bitstamp), "EUR/USD", false),
                    new TestCaseData(Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Bitstamp), "XBT/USD", false),
                    new TestCaseData(Symbol.Create("ETHUSDT", SecurityType.Crypto, Market.Bitstamp), "ETH/USDT", false),
                    new TestCaseData(Symbol.Create("XRPBTC", SecurityType.Crypto, Market.Bitstamp), "XRP/XBT", false),
                    new TestCaseData(Symbol.Create("ETHBTC", SecurityType.Crypto, Market.Bitstamp), "ETH/XBT", false),
                };
            }
        }
        
        
        [Test]
        [TestCaseSource(nameof(CorrectLeanSymbolTestParameters))]
        public void ReturnsCorrectLeanSymbol(string marketTicker, Symbol leanSymbol, bool shouldThrow)
        {
            TestDelegate test = () =>
            {
                var symbol = _symbolMapper.GetLeanSymbol(marketTicker);
                
                Assert.AreEqual(symbol, leanSymbol, "Converted Lean Symbol not the same with passed symbol");
            };
            
            if (shouldThrow)
            {
                Assert.Throws<ArgumentException>(test);
            }
            else
            {
                Assert.DoesNotThrow(test);
            }
        }

        [Test]
        [TestCaseSource(nameof(CorrectBrokerageSymbolTestParameters))]
        public void ReturnsCorrectBrokerageSymbol(Symbol leanSymbol, string marketTicker, bool shouldThrow)
        {
            TestDelegate test = () =>
            {
                var symbol = _symbolMapper.GetBrokerageSymbol(leanSymbol);
                
                Assert.AreEqual(symbol, marketTicker, "Converted Brokerage Symbol not the same with passed Brokerage symbol");
            };
            
            if (shouldThrow)
            {
                Assert.Throws<ArgumentException>(test);
            }
            else
            {
                Assert.DoesNotThrow(test);
            }
        }
        
        [Test]
        [TestCaseSource(nameof(WsSymbolTestParameters))]
        public void ReturnsCorrectWsSymbol(Symbol marketTicker, string wsTicker, bool shouldThrow)
        {
            TestDelegate test = () =>
            {
                var symbol = _symbolMapper.GetWebsocketSymbol(marketTicker);
                
                Assert.AreEqual(symbol, wsTicker, "Converted Websocket Symbol not the same with passed Websocket symbol");
            };
            
            if (shouldThrow)
            {
                Assert.Throws<ArgumentException>(test);
            }
            else
            {
                Assert.DoesNotThrow(test);
            }
        }
    }
}