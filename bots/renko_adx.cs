using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
 
namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_RENKO_ADX : Robot
    {
 
        [Parameter(DefaultValue = "BAMM_RENKO_ADX")]
        public string cBotLabel { get; set; }
 
        [Parameter("Currency pair", DefaultValue = "")]
        public string TradeSymbol { get; set; }
 
        [Parameter("Lot Size", DefaultValue = 0.1, MinValue = 0.01, Step = 0.01)]
        public double LotSize { get; set; }

        [Parameter("Entry - Use halving mode", DefaultValue = true)]
        public bool UseHalvingMode { get; set; }

        [Parameter("Max allowed positions", DefaultValue = 4)]
        public int MaxPositions { get; set; }

        [Parameter("Limit orders per block", DefaultValue = 2, MinValue = 1, Step = 1)]
        public int LimitOrdersBlock { get; set; }

        [Parameter("Limit order step pips", DefaultValue = 5, MinValue = 0.1, Step = 0.1)]
        public double LimitOrderStep { get; set; }
    
        // Should be 3.5 x Block size
        [Parameter("StopLoss in pips", DefaultValue = 15)]
        public double StopLoss { get; set; }

        [Parameter("Trailing stops", DefaultValue = true)]
        public bool HasTrailingStop { get; set; }
 
        [Parameter("TakeProfit in pips", DefaultValue = 5)]
        public double TakeProfit { get; set; }

        [Parameter("Breakeven - Add Pips", DefaultValue = 0.0, MinValue = 0.0)]
        public double BreakevenAddPips { get; set; }

        [Parameter("Breakeven - Trigger Pips", DefaultValue = 10, MinValue = 0.5)]
        public double BreakevenTriggerPips { get; set; }

        [Parameter("Renko block size", DefaultValue = 5)]
        public int RenkoBlockSize { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }
 
        Symbol CurrentSymbol;
        private BAMMRenkoTrend BAMMTrend;
        private bool IsGreenCandle;
 
        protected override void OnStart()
        {
            //check symbol
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);
 
            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            BAMMTrend = Indicators.GetIndicator<BAMMRenkoTrend>(true, true, true, false, true, true, MovingAverageType.Simple, 16, MovingAverageType.Exponential, 8, MovingAverageType.Simple, 64, 6, 35, 6, Source);

            RunBreakEvenCheck();

        }

        private void RunBreakEvenCheck()
        {
            var positions = Positions.Where(x => x.Label == cBotLabel && x.StopLoss < 0 && x.SymbolName == CurrentSymbol.Name);
            foreach(Position position in positions)
            {
                BreakEvenIfNeeded(position);
            }
        }
 
        protected override void OnBar()
        {
                IsGreenCandle = isGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));
                CheckPositions();
                EnterTrades();
        }
 
        protected override void OnTick()
        {
            RunBreakEvenCheck();
        }

        private int GetOpenPositions()
        {
            return Positions.FindAll(cBotLabel, TradeSymbol).Length;
        }

        private bool ShouldClosePositions() {
            return true;
        }

        private void CheckPositions() {

            // Close on trend change
            if (BAMMTrend.Result.Last(1) == 0) {
                ClosePositions(TradeType.Buy);
                ClosePositions(TradeType.Sell);
            }

        }
        private void EnterTrades()
        {

            double lastBarClose = Bars.ClosePrices.Last(1);

            if (IsEntryPossible() == true)
            {
                double lotSize = 0;
                double pipDistance = 0;

                if ( BAMMTrend.Result.Last(1) > 0)
                {          
                    CancelPendingOrders();
                    for(int i = 1; i <= LimitOrdersBlock; i++) {
                        pipDistance = pipDistance + LimitOrderStep;
                        lotSize = (UseHalvingMode == true) ? (LotSize/Math.Abs(BAMMTrend.Result.Last(1))) : LotSize;
                        OpenLimitOrder(TradeType.Buy, lastBarClose, lotSize, pipDistance);
                    }
                    // lotSize = (UseHalvingMode == true) ? (LotSize/Math.Abs(BAMMTrend.Result.Last(1))) : LotSize;
                    // OpenMarketOrder(TradeType.Buy, lotSize);
                    
                }
                else if ( BAMMTrend.Result.Last(1) < 0)
                {
                    CancelPendingOrders();
                    for(int i = 1; i <= LimitOrdersBlock; i++) {
                        pipDistance = pipDistance + LimitOrderStep;
                        lotSize = (UseHalvingMode == true) ? (LotSize/Math.Abs(BAMMTrend.Result.Last(1))) : LotSize;
                        OpenLimitOrder(TradeType.Sell, lastBarClose, lotSize, pipDistance);
                    }
                    // lotSize = (UseHalvingMode == true) ? (LotSize/Math.Abs(BAMMTrend.Result.Last(1))) : LotSize;
                    // OpenMarketOrder(TradeType.Sell, lotSize);
                }
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

        private bool isGreenCandle(double lastBarOpen, double lastBarClose) {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private void OpenLimitOrder(TradeType tradeType, double lastBarClose, double dLots, double pipDistance)
        {

            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(dLots);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            string Comment = null;
            double targetPrice = 0;

            if (tradeType == TradeType.Buy)
            {
                targetPrice = (lastBarClose - ( pipDistance * CurrentSymbol.PipSize) );
                Print("Hoping to catch this UPTREND {0} lower at {1}", lastBarClose, targetPrice);
                PlaceLimitOrderAsync(tradeType, CurrentSymbol.Name, volumeInUnits, targetPrice, cBotLabel, StopLoss, TakeProfit, null, Comment, HasTrailingStop);
                
            }
            else if (tradeType == TradeType.Sell)
            {
                targetPrice = (lastBarClose + ( pipDistance * CurrentSymbol.PipSize) );
                Print("Hoping to catch this DOWNTREND {0} higher at {1}", lastBarClose, targetPrice);
                PlaceLimitOrderAsync(tradeType, CurrentSymbol.Name, volumeInUnits, targetPrice, cBotLabel, StopLoss, TakeProfit, null, Comment, HasTrailingStop);
            }

        }

        private void ClosePositions(TradeType tradeType)
        {
            Print("Closing all {0} positions", (tradeType == TradeType.Buy) ? "BUY" : "SELL");
            foreach (var position in Positions.FindAll(cBotLabel, TradeSymbol, tradeType))
            {
                var result = ClosePosition(position);
                if (!result.IsSuccessful)
                {
                    Print("Closing position error: {0}", result.Error);
                    OnStop();
                }
            }
        }

        private bool IsEntryPossible()
        {
            if (BAMMTrend.Result.Last(1) == 0)
            {
                Print("Trade not allowed by Renko indikator - {0}", BAMMTrend.Result.Last(1));
                return false;
            }

            if(GetOpenPositions() >= MaxPositions) {
                Print("Max allowed positions met - {0}", MaxPositions);
                return false;
            }


            return true;
        }

        protected override void OnStop()
        {
 
        }

        private void BreakEvenIfNeeded(Position position)
        {

            Print("Running breakeven for id {0}, pips {1}, triggerpips {2}", position.Id, position.Pips, BreakevenTriggerPips);
            if (position.Pips < BreakevenTriggerPips)
                return;

            var desiredNetProfitInDepositAsset = BreakevenAddPips * CurrentSymbol.PipValue * position.VolumeInUnits;
            var desiredGrossProfitInDepositAsset = desiredNetProfitInDepositAsset - position.Commissions * 2 - position.Swap;
            var quoteToDepositRate = CurrentSymbol.PipValue / CurrentSymbol.PipSize;
            var priceDifference = desiredGrossProfitInDepositAsset / (position.VolumeInUnits * quoteToDepositRate);

            var priceAdjustment = GetPriceAdjustmentByTradeType(position.TradeType, priceDifference);
            var breakEvenLevel = position.EntryPrice + priceAdjustment;
            var roundedBreakEvenLevel = RoundPrice(breakEvenLevel, position.TradeType);

            ModifyPosition(position, roundedBreakEvenLevel, position.TakeProfit);
            Print("Stop loss for position PID" + position.Id + " has been moved to break even.");

        }

        private double RoundPrice(double price, TradeType tradeType)
        {
            var multiplier = Math.Pow(10, CurrentSymbol.Digits);
            return (tradeType == TradeType.Buy) ? (Math.Ceiling(price * multiplier) / multiplier) : ( Math.Floor(price * multiplier) / multiplier);
        }

        private static double GetPriceAdjustmentByTradeType(TradeType tradeType, double priceDifference)
        {
            return (tradeType == TradeType.Buy) ? priceDifference : -priceDifference;
        }
    }
}