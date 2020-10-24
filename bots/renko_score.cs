using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_RENKO_SCORE : Robot
    {

        [Parameter(DefaultValue = "BAMM_RENKO_SCORE")]
        public string cBotLabel { get; set; }

        [Parameter("Currency pair", DefaultValue = "")]
        public string TradeSymbol { get; set; }

        [Parameter("Follow Label", DefaultValue = "")]
        public string FollowLabel { get; set; }

        [Parameter("Use Breakeven", DefaultValue = true)]
        public bool UseBreakeven { get; set; }

        [Parameter("Breakeven Pips", DefaultValue = 10)]
        public double BreakEvenPips { get; set; }

        [Parameter("Target EUR", DefaultValue = 100)]
        public double TargetEUR { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;
        private BAMMRenkoClose _BAMMRenkoClose;

        private bool _IsBlockGreen;

        private Random random = new Random();

        protected override void OnStart()
        {
            if (FollowLabel == "")
            {
                Print("FollowLabel is empty, please check!");
                OnStop();
            }

            //check symbol
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);

            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            _BAMMRenkoClose = Indicators.GetIndicator<BAMMRenkoClose>(1, 5, 25, 25, 25, 25, 25, 25, 65, 35, 75, 75, 25, 100, Source);

            Print("I'm watching you! {0}", FollowLabel);
        }

        protected override void OnBar()
        {
            _IsBlockGreen = IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));
            RunChecks();
        }

        private void RunChecks()
        {
            var positions = Positions.Where(x => x.Label == FollowLabel && x.SymbolName == CurrentSymbol.Name );
            foreach (Position position in positions)
            {
                Print("Checking id {0} {1}", position.Id, position.NetProfit);

                if (UseBreakeven == true && position.NetProfit > 0 && position.Pips >= BreakEvenPips)
                {
                    BreakEven(position);  
                }

                if (TryToClosePosition(position) == true)
                {
                    var result = ClosePosition(position);
                    if (!result.IsSuccessful)
                    {
                        Print("Could not close!");
                    }
                }
            }
        }

        private bool IsGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private void BreakEven(Position position)
        {
            if (MoveStop(position.TradeType) == true )
            {
                Print("Check if SL {0} != {1} - pips {2} {3}", (position.EntryPrice + CurrentSymbol.Spread), position.StopLoss, BreakEvenPips, position.Pips);
                if( (position.EntryPrice + CurrentSymbol.Spread) != position.StopLoss ) {
                    ModifyPosition(position, (position.EntryPrice + CurrentSymbol.Spread), position.TakeProfit);
                }                
            }
        }

        private bool MoveStop(TradeType tradeType)
        {
            if (tradeType == TradeType.Buy && _IsBlockGreen)
            {
                return true;
            }
            else if (tradeType == TradeType.Sell && !_IsBlockGreen)
            {
                return true;
            }

            return false;
        }

        private double GetCloseScore(Position position)
        {
            if (position.TradeType == TradeType.Buy) {
                return _BAMMRenkoClose.CloseLong.Last(1);
            } else {
                return _BAMMRenkoClose.CloseShort.Last(1);
            }
        }

        private bool ShouldClosePosition(double penalty) { 
            int roll =  random.Next(1,101);
            Print("Rolled a {0}, penalty is {1}. {1} >= {0} ?", roll, penalty);
            if( penalty >= roll) {
                return true;
            }
            return false;
        }

        private bool RollForProfit(Position position) {
            int roll =  random.Next(1,101);

            var currentNetProfitInDepositAsset = position.Pips * CurrentSymbol.PipValue * position.VolumeInUnits;

            double _rrr = Math.Ceil((currentNetProfitInDepositAsset/TargetEUR)*10)*1.25;

            Print("Rolled a {0}, RRR is {1}. {1} >= {0} ?", roll, _rrr);
            if( _rrr >= roll) {
                return true;
            }
            return false;
        }

        private bool TryToClosePosition(Position position)
        {

            // Dont close if trade is going the right way
            if(position.TradeType == TradeType.Buy && IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1)) == true) {
                Print("Going the GREEN mile!");
                if (RollForProfit(position) == true) {
                    Print("BLING BLING!");
                    return true;
                }
                return false;
            }

            // Dont close if trade is going the right way
            if(position.TradeType == TradeType.Sell && !IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1)) == true) {
                Print("REDRUM!");
                if (RollForProfit(position) == true) {
                    Print("I like them moneiiies!");
                    return true;
                }
                return false;
            }

            // Dont close if negative. Let SL do that
            if( position.NetProfit < 0 ) {
                Print("Dont close - profit negative");
                return false;
            }

            if (ShouldClosePosition(GetCloseScore(position)) == true)
            {

                Print("Yes close");
                return true;
            }
            else
            {
                Print("No - keep");
                return false;
            }
        }

        protected override void OnTick()
        {

        }

    }
}
