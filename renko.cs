using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_RENKO : Robot
    {

        [Parameter(DefaultValue = "BAMM_RENKO")]
        public string cBotLabel { get; set; }

        [Parameter("Currency pair", DefaultValue = "XAUUSD")]
        public string TradeSymbol { get; set; }

        [Parameter("Market buy?", DefaultValue = true)]
        public bool MarketBuy { get; set; }

        // How far should the limit price be i regards to RenkoBlockSize on a new block
        [Parameter("MarketFactor", DefaultValue = 0.5, MinValue = 0.1, Step = 0.05)]
        public double MarketFactor { get; set; }

        [Parameter("Lot Size", DefaultValue = 0.5, MinValue = 0.01, Step = 0.01)]
        public double LotSize { get; set; }

        [Parameter("Spread in pips", DefaultValue = 2)]
        public double Spread { get; set; }

        [Parameter("TakeProfit in pips", DefaultValue = 0)]
        public double TakeProfit { get; set; }

        [Parameter("Open Position", DefaultValue = false)]
        public bool OpenPosition { get; set; }

        [Parameter("Open Limit Position", DefaultValue = false)]
        public bool OpenLimitPosition { get; set; }

        [Parameter("Limit Lot Size", DefaultValue = 1, MinValue = 0.01, Step = 0.05)]
        public double LimitPositionLotSize { get; set; }

        // How far should the limit price be i regards to RenkoBlockSize
        [Parameter("Limit Factor", DefaultValue = 1.2, MinValue = 0.1, Step = 0.05)]
        public double LimitPositionFactor { get; set; }

        [Parameter("Limit TakeProfit", DefaultValue = 30, MinValue = 0, Step = 1)]
        public double LimitPositionTakeProfit { get; set; }

        [Parameter("Renko Block Size", DefaultValue = 10, MinValue = 1, Step = 1)]
        public double RenkoBlockSize { get; set; }

        [Parameter("MA Type", DefaultValue = MovingAverageType.Exponential)]
        public MovingAverageType MAType { get; set; }

        [Parameter("MA Period", DefaultValue = 6)]
        public int MAPeriod { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;
        private MovingAverage MA;


        protected override void OnStart()
        {

            // Check symbol
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);
            MA = Indicators.MovingAverage(Source, MAPeriod, MAType);

            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            Print("This is the B.A.M.M Bot {0} {1}!", cBotLabel, CurrentSymbol.Name);
        }

        private bool IsTrendingUp()
        {
            return ((Bars.ClosePrices.Last(1) > Bars.OpenPrices.Last(1)) && (Bars.ClosePrices.Last(2) < Bars.OpenPrices.Last(2))) ? true : false;
        }

        private bool IsTrendingDown()
        {
            return ((Bars.ClosePrices.Last(1) < Bars.OpenPrices.Last(1)) && (Bars.ClosePrices.Last(2) > Bars.OpenPrices.Last(2))) ? true : false;
        }

        private bool ShouldClosePositions(TradeType tradeType, double lastBarClose)
        {
            Print("Close {0}, EMA({1}) {2}", MAPeriod, lastBarClose, MA.Result.Last(1));

            // Are we checking buy or sell positions
            if (tradeType == TradeType.Buy)
            {
                // Checking buy positons. Did the sell candle close above EMA?
                // return (lastBarClose > MA.Result.Last(1)) ? false : true;
                return true;

            }
            else
            {
                // Checking sell positons. Did the buy candle close below EMA?
                // return (lastBarClose < MA.Result.Last(1)) ? false : true;
                return true;
            }
        }
        protected override void OnBar()
        {
            // When a trend change happens only open limit orders on this first candle
            bool trendChange = false;
            double closePrice = Bars.ClosePrices.Last(1);

            if (IsTrendingUp())
            {
                Print("Trending UP!");
                trendChange = true;

                // Check if we should close open positions yet
                if (ShouldClosePositions(TradeType.Sell, closePrice))
                {
                    Print("Check says to close sell positons");
                    CloseOpenSellPositions();
                }
            }

            if (IsTrendingDown())
            {
                Print("Trending DOWN!");
                trendChange = true;

                // Check if we should close open positions yet
                if (ShouldClosePositions(TradeType.Buy, closePrice))
                {
                    Print("Check says to close buy positons");
                    CloseOpenBuyPositions();
                }
            }

            if (closePrice > Bars.ClosePrices.Last(2))
            {
                // Cancel all pending orders
                CancelPendingOrders();

                // Dont market buy on trend change, only limit orders
                if (trendChange == false)
                {
                    CloseOpenSellPositions();
                    TradeUp(closePrice);
                }

                OpenLimitOrder(TradeType.Buy, closePrice);
            }
            else if (closePrice < Bars.ClosePrices.Last(2))
            {
                // Cancel all pending orders
                CancelPendingOrders();

                // Dont market buy on trend change, only limit orders
                if (trendChange == false)
                {
                    CloseOpenBuyPositions();
                    TradeDown(closePrice);
                }

                OpenLimitOrder(TradeType.Sell, closePrice);
            }
        }

        private void OpenLimitOrder(TradeType tradeType, double barPrice)
        {

            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(LimitPositionLotSize);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            string Comment = null;
            bool HasTrailingStop = true;
            double SL = Spread + (RenkoBlockSize * 4);
            double targetPrice = 0;

            // Check buying or selling
            if (tradeType == TradeType.Buy)
            {
                targetPrice = (barPrice - ((RenkoBlockSize * LimitPositionFactor) * CurrentSymbol.PipSize));
                Print("Hoping to catch BUY orders lower at {0}", targetPrice);
                PlaceLimitOrderAsync(tradeType, CurrentSymbol.Name, volumeInUnits, targetPrice, cBotLabel, SL, LimitPositionTakeProfit, null, Comment, HasTrailingStop);
            }
            else
            {
                targetPrice = (barPrice + ((RenkoBlockSize * LimitPositionFactor) * CurrentSymbol.PipSize));
                Print("Hoping to catch SELL order higher at {0}", targetPrice);
                PlaceLimitOrderAsync(tradeType, CurrentSymbol.Name, volumeInUnits, targetPrice, cBotLabel, SL, LimitPositionTakeProfit, null, Comment, HasTrailingStop);
            }

        }

        private void CloseOpenSellPositions()
        {
            Print("Close all open SELL positions!");
            ClosePositions(TradeType.Sell);
        }

        private void CloseOpenBuyPositions()
        {
            Print("Close all open BUY positions!");
            ClosePositions(TradeType.Buy);
        }

        private void TradeUp(double barPrice)
        {
            Print("BUY more lots!");
            if (OpenPosition == true && MarketBuy == true)
            {
                OpenMarketOrder(TradeType.Buy, LotSize);
            } else if (OpenPosition == true && MarketBuy == false)
            {
                // Open Limit order instead in new block
                string Comment = null;
                bool HasTrailingStop = true;
                double SL = Spread + (RenkoBlockSize * 4);
                double targetPrice = 0;

                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(LotSize);
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
                targetPrice = (barPrice - ((RenkoBlockSize * MarketFactor) * CurrentSymbol.PipSize));

                PlaceLimitOrderAsync(TradeType.Buy, CurrentSymbol.Name, volumeInUnits, targetPrice, cBotLabel, SL, TakeProfit, null, Comment, HasTrailingStop);
            }


        }

        private void TradeDown(double barPrice)
        {
            Print("SELL more lots!");
            if (OpenPosition == true && MarketBuy == true)
            {
                OpenMarketOrder(TradeType.Sell, LotSize);
            }else if (OpenPosition == true && MarketBuy == false)
            {
                // Open Limit order instead in new block
                string Comment = null;
                bool HasTrailingStop = true;
                double SL = Spread + (RenkoBlockSize * 4);

                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(LotSize);
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
                double targetPrice = (barPrice + ((RenkoBlockSize * MarketFactor) * CurrentSymbol.PipSize));

                PlaceLimitOrderAsync(TradeType.Sell, CurrentSymbol.Name, volumeInUnits, targetPrice, cBotLabel, SL, TakeProfit, null, Comment, HasTrailingStop);
            }
        }


        private void OpenMarketOrder(TradeType tradeType, double dLots)
        {
            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(dLots);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            string Comment = null;
            bool HasTrailingStop = true;
            double SL = Spread + (RenkoBlockSize * 4);

            //in final version need add attempts counter
            var result = ExecuteMarketOrder(tradeType, CurrentSymbol.Name, volumeInUnits, cBotLabel, SL, TakeProfit, Comment, HasTrailingStop);
            if (!result.IsSuccessful)
            {
                Print("Execute Market Order Error: {0}", result.Error.Value);
                OnStop();
            }
        }

        private void CancelPendingOrders()
        {

            Print("Found {0} pending orders for label {1} {2}", PendingOrders.Count(x => x.Label == cBotLabel && x.SymbolName == CurrentSymbol.Name), cBotLabel, CurrentSymbol.Name);
            var orders = PendingOrders.Where(x => x.Label == cBotLabel && x.SymbolName == CurrentSymbol.Name);

            foreach (var order in orders)
            {
                var result = CancelPendingOrderAsync(order);
            }
        }

        private void ClosePositions(TradeType tradeType)
        {
            foreach (var position in Positions.FindAll(cBotLabel, TradeSymbol, tradeType))
            {
                var result = ClosePosition(position);
                if (!result.IsSuccessful)
                {
                    Print("Closing market order error: {0}", result.Error);
                    OnStop();
                }
                else
                {
                    Print("Closed positions {0}, {1}, {2}", cBotLabel, TradeSymbol, tradeType);
                }
            }
        }

        protected override void OnStop()
        {

        }
    }
}
