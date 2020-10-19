// -------------------------------------------------------------------------------------------------
//
//    This code is a cTrader Automate API example.
//
//    This cBot is intended to be used as a sample and does not guarantee any particular outcome or
//    profit of any kind. Use it at your own risk.
//    
//    All changes to this file might be lost on the next application update.
//    If you are going to modify this file please make a copy using the "Duplicate" command.
//
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_BREAKEVEN : Robot
    {
        private SymbolInfo _symbolInfo;
        
        [Parameter("Search label")]
        public string PositionLabelSearch { get; set; }

        [Parameter("Add Pips", DefaultValue = 0.0, MinValue = 0.0)]
        public double AddPips { get; set; }

        [Parameter("Trigger Pips", DefaultValue = 10, MinValue = 0.5)]
        public double TriggerPips { get; set; }

        [Parameter("Currency pair", DefaultValue = "")]
        public string TradeSymbol { get; set; }

        protected override void OnStart()
        {

            if (PositionLabelSearch == "")
                PrintErrorAndStop("\"PositionLabelSearch\" cannot be empty");

            if (TriggerPips < AddPips + 0.5)
                PrintErrorAndStop("\"Trigger Pips\" must be greater or equal to \"Add Pips\" + 0.5");

            _symbolInfo = Symbols.GetSymbolInfo(TradeSymbol);

            Print("Started BAMM_BREAKEVEN searching for positions in {0}", PositionLabelSearch);
            RunBreakEvenCheck();
            
        }

        private void RunBreakEvenCheck() {
            // Search for matching labels, symbols and stops that are not modified
            Print("Searching for positions for {0} {1}...", PositionLabelSearch, _symbolInfo.Name);
            var positions = Positions.Where(x => x.Label == PositionLabelSearch && x.StopLoss < 0 && x.SymbolName == _symbolInfo.Name);
            foreach(Position position in positions) {
                BreakEvenIfNeeded(position);
            }
        }
        private void PrintErrorAndStop(string errorMessage)
        {
            Print(errorMessage);
            Stop();

            throw new Exception(errorMessage);
        }

        protected override void OnTick()
        {
            RunBreakEvenCheck();
        }

        private void BreakEvenIfNeeded(Position position)
        {

            Print("Running breakeven for id {0}, pips {1}, triggerpips {2}", position.Id, position.Pips, TriggerPips);
            if (position.Pips < TriggerPips)
                return;

            var desiredNetProfitInDepositAsset = AddPips * _symbolInfo.PipValue * position.VolumeInUnits;
            var desiredGrossProfitInDepositAsset = desiredNetProfitInDepositAsset - position.Commissions * 2 - position.Swap;
            var quoteToDepositRate = _symbolInfo.PipValue / _symbolInfo.PipSize;
            var priceDifference = desiredGrossProfitInDepositAsset / (position.VolumeInUnits * quoteToDepositRate);

            var priceAdjustment = GetPriceAdjustmentByTradeType(position.TradeType, priceDifference);
            var breakEvenLevel = position.EntryPrice + priceAdjustment;
            var roundedBreakEvenLevel = RoundPrice(breakEvenLevel, position.TradeType);

            ModifyPosition(position, roundedBreakEvenLevel, position.TakeProfit);
            Print("Stop loss for position PID" + position.Id + " has been moved to break even.");

        }

        private double RoundPrice(double price, TradeType tradeType)
        {
            var multiplier = Math.Pow(10, _symbolInfo.Digits);
            return (tradeType == TradeType.Buy) ? (Math.Ceiling(price * multiplier) / multiplier) : ( Math.Floor(price * multiplier) / multiplier);
        }

        private static double GetPriceAdjustmentByTradeType(TradeType tradeType, double priceDifference)
        {
            return (tradeType == TradeType.Buy) ? priceDifference : -priceDifference;
        }
    }
}
