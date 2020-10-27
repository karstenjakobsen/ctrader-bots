using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_RENKO_CLOSE : Robot
    {

        [Parameter(DefaultValue = "BAMM_RENKO_CLOSE")]
        public string cBotLabel { get; set; }

        [Parameter("Currency pair", DefaultValue = "")]
        public string TradeSymbol { get; set; }

        [Parameter("Follow Label", DefaultValue = "")]
        public string FollowLabel { get; set; }

        [Parameter("Use Breakeven", DefaultValue = true)]
        public bool UseBreakeven { get; set; }

        [Parameter("Breakeven Pips", DefaultValue = 10)]
        public double BreakEvenPips { get; set; }

        // Around breakeven pips
        [Parameter("Target Pips", DefaultValue = 10)]
        public double TargetPips { get; set; }

        [Parameter("STOCH_KPERIODS", DefaultValue = 9)]
        public int STOCH_KPERIODS { get; set; }

        [Parameter("STOCH_KSLOWING", DefaultValue = 3)]
        public int STOCH_KSLOWING { get; set; }

        [Parameter("STOCH_DPERIODS", DefaultValue = 9)]
        public int STOCH_DPERIODS { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;
        private BAMMRenkoUgliness _BAMMRenkoUgliness;

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

            _BAMMRenkoUgliness = Indicators.GetIndicator<BAMMRenkoUgliness>(1, 0, 25, 25, 25, 25, 25, STOCH_KPERIODS, STOCH_KSLOWING, STOCH_DPERIODS, Source);

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
                return _BAMMRenkoUgliness.CloseLong.Last(1);
            } else {
                return _BAMMRenkoUgliness.CloseShort.Last(1);
            }
        }

        private bool RollForClose(Position position) {

            int roll = random.Next(1,101);
            double closeScore = GetCloseScore(position);

            Print("Rolled a {0}, close score is {1}. {0} <= {1} ?", roll, closeScore);

            if( roll <= closeScore ) {
                return true;
            }

            return false;
        }

        // High roll == move SL
        private bool RollForTrailingSL(Position position) {

            double roll =  random.Next(1,101);

            double _closeScore = GetCloseScore(position);
            double _RRRLevel = Math.Ceiling(position.Pips/TargetPips);
            double _rrr = 100-(_RRRLevel*10);

            // add 5% chance to roll foreach RRR level above 1
            for(int i = 2; i <= _RRRLevel; i++) {
                roll += 5;
                Print("Added rrr level +5 to roll - rrr is {0}", _rrr);
            }    

            // Add close score to roll above 25
            if(_closeScore > 0) {
                roll += (_closeScore/2);
                Print("Added close score +{0}/2 to roll", (_closeScore/2));
            }      

            Print("Rolled a {0}, RRR score is {1}. {0} > {1} ?", roll, _rrr);
            if(roll > _rrr)  {
                return true;
            }

            return false;
        }

        private bool TryToClosePosition(Position position)
        {

            // Dont close if trade is going the right way
            if(position.TradeType == TradeType.Buy && IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1)) == true) {
                Print("Going the GREEN mile!");
                // if (RollForTrailingSL(position) == true) {
                //     Print("BLING BLING!");
                //     ModifyPosition(position, (Bars.OpenPrices.Last(1) - CurrentSymbol.Spread), position.TakeProfit, true);
                //     return false;
                // }
                return false;
            }

            // Dont close if trade is going the right way
            if(position.TradeType == TradeType.Sell && !IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1)) == true) {
                Print("REDRUM!");
                // if (RollForTrailingSL(position) == true) {
                //     Print("I like them moneiiies!");
                //     ModifyPosition(position, (Bars.OpenPrices.Last(1) + CurrentSymbol.Spread), position.TakeProfit, true);
                //     return false;
                // }
                return false;
            }

            // Dont close if negative. Let SL do that
            if( position.NetProfit < 0 ) {
                Print("Dont close - profit negative");
                return false;
            }

            if (RollForClose(position) == true)
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
