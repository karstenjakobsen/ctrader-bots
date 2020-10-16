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
 
        [Parameter("Currency pair", DefaultValue = "EURUSD")]
        public string TradeSymbol { get; set; }
 
        [Parameter("Lot Size", DefaultValue = 0.5, MinValue = 0.01, Step = 0.01)]
        public double LotSize { get; set; }
 
        [Parameter("Trade entry - Use green/red candle", DefaultValue = true)]
        public bool UseCandle { get; set; }
 
        [Parameter("Trade entry - Use candle above/below MAs", DefaultValue = true)]
        public bool UseCandleMAs { get; set; }
 
        [Parameter("MA01 Type", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType MAType1 { get; set; }
 
        [Parameter("MA01 Period", DefaultValue = 16)]
        public int MAPeriod1 { get; set; }
 
        [Parameter("MA02 Type", DefaultValue = MovingAverageType.Exponential)]
        public MovingAverageType MAType2 { get; set; }
 
        [Parameter("MA02 Period", DefaultValue = 8)]
        public int MAPeriod2 { get; set; }
 
        [Parameter("Trade entry - Use ADX", DefaultValue = true)]
        public bool UseADX { get; set; }

        [Parameter("Trade entry - Use ADX Down", DefaultValue = false)]
        public bool UseADXDown { get; set; }
 
        [Parameter("ADX Period", DefaultValue = 6)]
        public int ADXPeriod { get; set; }
 
        [Parameter("ADX Level", DefaultValue = 32)]
        public int ADXLevel { get; set; }
 
        [Parameter("Multiply trades", DefaultValue = false)]
        public bool UseMT { get; set; }
    
        // Should be 3.5 x Block size
        [Parameter("StopLoss in pips", DefaultValue = 40.0)]
        public double StopLoss { get; set; }
 
        [Parameter("TakeProfit in pips", DefaultValue = 0)]
        public double TakeProfit { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }
 
        Symbol CurrentSymbol;
        private MovingAverage MA1;
        private MovingAverage MA2;
        private DirectionalMovementSystem DMS;
 
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
            DMS = Indicators.DirectionalMovementSystem(ADXPeriod);

        }
 
        protected override void OnBar()
        {
                DoTrade(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));
        }
 
        protected override void OnTick()
        {

        }
 
        private void OpenLimitOrder(TradeType tradeType, double dLots, double lastBarClose) {

            
        }

        private void DoTrade(double lastBarOpen, double lastBarClose)
        {
            if (IsTradePossible() == true && ((UseMT == false && Positions.FindAll(cBotLabel, TradeSymbol).Length == 0) || (UseMT == true)))
            {
                if (((UseCandle == true && lastBarClose > lastBarOpen) || UseCandle == false) && ((UseCandleMAs == true && lastBarClose > MA1.Result.Last(1) && lastBarClose > MA2.Result.Last(1)) || (UseCandleMAs == false)))
                {
                    Print("BUY more lots!");
                    ClosePositions(TradeType.Sell);
                    OpenMarketOrder(TradeType.Buy, LotSize);               
                    
                }
                else if (((UseCandle == true && lastBarClose < lastBarOpen) || UseCandle == false) && ((UseCandleMAs == true && lastBarClose < MA1.Result.Last(1) && lastBarClose < MA2.Result.Last(1)) || (UseCandleMAs == false)))
                {
                    Print("SELL more lots!");
                    ClosePositions(TradeType.Buy);
                    OpenMarketOrder(TradeType.Sell, LotSize);
                    
                }
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
                    Print("Closing market order error: {0}", result.Error);
                    OnStop();
                }
            }
        }
 
        private void OpenMarketOrder(TradeType tradeType, double dLots)
        {
            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(dLots);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
 
            string Comment = "";
            bool HasTrailingStop = true;

            //in final version need add attempts counter
            var result = ExecuteMarketOrder(tradeType, CurrentSymbol.Name, volumeInUnits, cBotLabel, StopLoss, TakeProfit, Comment, HasTrailingStop);
            if (!result.IsSuccessful)
            {
                Print("Execute Market Order Error: {0}", result.Error.Value);
                OnStop();
            }
        }
 
        private bool IsTradePossible()
        {
            if (UseADX == true && DMS.ADX.LastValue < ADXLevel)
            {
                Print("No trade - ADX is low - {0}", DMS.ADX.LastValue);
                return false;
            }
 

            if (UseADX == true && UseADXDown == true && (DMS.ADX.Last(1) > DMS.ADX.Last(0)))
            {
                Print("No trade - ADX is going down - current {0} previous {1} ", DMS.ADX.Last(0), DMS.ADX.Last(1));
                return false;
            }

            return true;
        }
 
        protected override void OnStop()
        {
 
        }
    }
}