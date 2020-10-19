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
 
        [Parameter("Entry - Green/red candle", DefaultValue = true)]
        public bool UseCandle { get; set; }
 
        [Parameter("Entry - Candle above/below MAs", DefaultValue = true)]
        public bool UseCandleMAs { get; set; }

        [Parameter("Entry - Only above/below major trend", DefaultValue = true)]
        public bool UseTrendMA { get; set; }

        [Parameter("Entry - MAs must point in trend direction", DefaultValue = true)]
        public bool MATrendDirection { get; set; }

        [Parameter("Entry - Use ADX", DefaultValue = true)]
        public bool UseADX { get; set; }

        [Parameter("Entry - Use ADX Down", DefaultValue = true)]
        public bool UseADXDown { get; set; }

        [Parameter("Exit - Close positions on reversal", DefaultValue = true)]
        public bool CloseOnTrendReversal { get; set; }

        [Parameter("Max allowed positions", DefaultValue = 4)]
        public int MaxPositions { get; set; }

        [Parameter("Limit orders per block", DefaultValue = 2, MinValue = 1, Step = 1)]
        public int LimitOrdersBlock { get; set; }

        [Parameter("Limit order step pips", DefaultValue = 5, MinValue = 1, Step = 1)]
        public double LimitOrderStep { get; set; }
 
        [Parameter("MA01 Type", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType MAType1 { get; set; }
 
        [Parameter("MA01 Period", DefaultValue = 16)]
        public int MAPeriod1 { get; set; }
 
        [Parameter("MA02 Type", DefaultValue = MovingAverageType.Exponential)]
        public MovingAverageType MAType2 { get; set; }
 
        [Parameter("MA02 Period", DefaultValue = 8)]
        public int MAPeriod2 { get; set; }

        [Parameter("MA03 Type (Trend)", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType MAType3 { get; set; }
 
        [Parameter("MA03 Period", DefaultValue = 64)]
        public int MAPeriod3 { get; set; }
 
        [Parameter("ADX Period", DefaultValue = 6)]
        public int ADXPeriod { get; set; }
 
        [Parameter("ADX Level", DefaultValue = 32)]
        public int ADXLevel { get; set; }
    
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

        [Parameter("Draw entry signal", DefaultValue = false)]
        public bool DrawEntrySignal { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }
 
        Symbol CurrentSymbol;
        private MovingAverage MA1;
        private MovingAverage MA2;
        private MovingAverage MA3;
        private DirectionalMovementSystem DMS;
        private bool IsGreenCandle = false;
 
        protected override void OnStart()
        {
            //check symbol
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);
 
            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            MA1 = Indicators.MovingAverage(Source, MAPeriod1, MAType1);
            MA2 = Indicators.MovingAverage(Source, MAPeriod2, MAType2);
            MA3 = Indicators.MovingAverage(Source, MAPeriod3, MAType3);
            DMS = Indicators.DirectionalMovementSystem(ADXPeriod);

            RunBreakEvenCheck();

        }

        private void RunBreakEvenCheck() {
            var positions = Positions.Where(x => x.Label == cBotLabel && x.StopLoss < 0 && x.SymbolName == CurrentSymbol.Name);
            foreach(Position position in positions) {
                BreakEvenIfNeeded(position);
            }
        }
 
        protected override void OnBar()
        {
                IsGreenCandle = isGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));
                ExitPositions();
                EntryTrades();
        }
 
        protected override void OnTick()
        {
            RunBreakEvenCheck();
        }

        private int GetOpenPositions() {
            return Positions.FindAll(cBotLabel, TradeSymbol).Length;
        }

        private bool ShouldClosePositions() {
            return true;
        }

        private void ExitPositions() {

            // Close on trend change
            if ((UseCandle == true && IsGreenCandle == false) || CloseOnTrendReversal == true)
                ClosePositions(TradeType.Buy);
            else if ((UseCandle == true && IsGreenCandle == true) || CloseOnTrendReversal == true)
                ClosePositions(TradeType.Sell);

        }
        private void EntryTrades()
        {
            double lastBarClose = Bars.ClosePrices.Last(1);

            if (IsEntryPossible() == true)
            {
                if (((UseCandle == true && IsGreenCandle == true) || UseCandle == false) && ((UseCandleMAs == true && lastBarClose > MA1.Result.Last(1) && lastBarClose > MA2.Result.Last(1)) || (UseCandleMAs == false)))
                {          
                    CancelPendingOrders();

                    // // Check if we are allowed to market buy
                    // if(MarketBuy == true && MarketBuyLimit > openPositions) {
                    //     Print("BUY more lots!");
                    //     OpenMarketOrder(TradeType.Buy, LotSize);
                    //     openPositions = GetOpenPositions();
                    // } else {
                    //     Print("Cant open market order {0} {1} {2}", MarketBuy, MarketBuyLimit, openPositions);
                    // }
                    double pipDistance = 0;
                    for(int i = 1; i <= LimitOrdersBlock; i++) {
                        pipDistance = pipDistance + LimitOrderStep;
                        OpenLimitOrder(TradeType.Buy, lastBarClose, LotSize, pipDistance);
                    }
                    
                }
                else if (((UseCandle == true && IsGreenCandle == false) || UseCandle == false) && ((UseCandleMAs == true && lastBarClose < MA1.Result.Last(1) && lastBarClose < MA2.Result.Last(1)) || (UseCandleMAs == false)))
                {
                    CancelPendingOrders();

                    // // Check if we are allowed to market buy
                    // if(MarketBuy == true && MarketBuyLimit > openPositions) {
                    //     Print("SELL more lots!");
                    //     OpenMarketOrder(TradeType.BuySell, LotSize);
                    //     openPositions = GetOpenPositions();
                    // } else {
                    //     Print("Cant open market order {0} {1} {2}", MarketBuy, MarketBuyLimit, openPositions);
                    // }
                    double pipDistance = 0;
                    for(int i = 1; i <= LimitOrdersBlock; i++) {
                        pipDistance = pipDistance + LimitOrderStep;
                        OpenLimitOrder(TradeType.Sell, lastBarClose, LotSize, pipDistance);
                    }
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
 
        private void OpenMarketOrder(TradeType tradeType, double dLots)
        {
            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(dLots);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
 
            string Comment = "";

            // Market order does not have a Target. Let this baby ride until TSL i reached
            var result = ExecuteMarketOrder(tradeType, CurrentSymbol.Name, volumeInUnits, cBotLabel, StopLoss, 0, Comment, HasTrailingStop);
            if (!result.IsSuccessful)
            {
                Print("Execute Market Order Error: {0}", result.Error.Value);
                OnStop();
            }
        }
 
        // private int GetConsecutiveCandles() {
        //     bool thisCandle = isGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));
        //     for(int i = 2; i < ConsecutiveCandles; i++) {
        //        bool previousCandle = isGreenCandle(Bars.OpenPrices.Last(i), Bars.ClosePrices.Last(i));
        //        if(thisCandle != previousCandle){
        //            return i - 1;
        //        }
        //     }
        //     return i;
        // }

        private bool IsEntryPossible()
        {
            if (UseADX == true && DMS.ADX.Last(1) < ADXLevel)
            {
                Print("No trade - ADX is low - {0}", DMS.ADX.Last(1));
                return false;
            }

            if (UseTrendMA == true && UseCandle == true)
            {
                if(IsGreenCandle == true && (Bars.ClosePrices.Last(1) < MA3.Result.Last(1))) 
                {
                    Print("No trade - Green candle is below major trend - MA {0}, candle {1}", MA3.Result.Last(1));
                    return false;
                }
                else if(IsGreenCandle == false && (Bars.ClosePrices.Last(1) > MA3.Result.Last(1)))
                {
                    Print("No trade - red candle is above major trend - MA {0}, candle {1}", MA3.Result.Last(1));
                    return false;
                }
               
            }

            if (UseADX == true && UseADXDown == true && (DMS.ADX.Last(2) > DMS.ADX.Last(1)))
            {
                Print("No trade - ADX is going down - current {0} previous {1} ", DMS.ADX.Last(1), DMS.ADX.Last(2));
                return false;
            }

            if (MATrendDirection == true)
            {
                if(IsGreenCandle == true && (MA1.Result.Last(1) > MA1.Result.Last(2) && MA2.Result.Last(1) > MA2.Result.Last(2)))
                {
                    Print("No trade - Both MAs are not pointing in UP direction - MA1, current {0} previous {1} - MA2, current {2} previous {3} ", MA1.Result.Last(1), MA1.Result.Last(2), MA2.Result.Last(1), MA2.Result.Last(2));
                    return false;
                } else if(IsGreenCandle == false && (MA1.Result.Last(0) < MA1.Result.Last(1) && MA2.Result.Last(0) < MA2.Result.Last(1)))
                {
                    Print("No trade - Both MAs are not pointing in DOWN direction - MA1, current {0} previous {1} - MA2, current {2} previous {3} ", MA1.Result.Last(1), MA1.Result.Last(2), MA2.Result.Last(1), MA2.Result.Last(2));
                    return false;
                }
            }

            if(GetOpenPositions() >= MaxPositions) {
                Print("Max allowed positions met - {0}", MaxPositions);
                return false;
            }

            if (AlreadyInProfitForTrend() == true){
                Print("Thanks for all the fish!...");
                return false;
            }

            return true;
        }

        private bool AlreadyInProfitForTrend() {
            return false;
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