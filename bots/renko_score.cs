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

        [Parameter("Close Position Score", DefaultValue = 100)]
        public int CloseScore { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;

        private StochasticOscillator _STO;
        private MovingAverage _MA1;
        private MovingAverage _MA2;

        private bool _IsBlockGreen;

        private const int PENALTY_CANDLE_WEIGHT = 0;

        private const int PENALTY_STOCHD_HISTORY = 1;
        private const int PENALTY_STOCHD_WEIGHT = 50;

        private const int PENALTY_STOCHK_WEIGHT = 50;

        private const int PENALTY_MA1_HISTORY = 1;
        private const int PENALTY_MA1_WEIGHT = 25;

        private const int PENALTY_MA2_HISTORY = 1;
        private const int PENALTY_MA2_WEIGHT = 25;

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

            _STO = Indicators.StochasticOscillator(9, 3, 9, MovingAverageType.Simple);
            _MA1 = Indicators.MovingAverage(Source, 16, MovingAverageType.Simple);
            _MA2 = Indicators.MovingAverage(Source, 8, MovingAverageType.Exponential);

            Print("I'm watching you! {0}", FollowLabel);
        }

        protected override void OnBar()
        {
            _IsBlockGreen = isGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));
            RunChecks();
        }

        private void RunChecks()
        {
            var positions = Positions.Where(x => x.Label == FollowLabel && x.SymbolName == CurrentSymbol.Name );
            foreach (Position position in positions)
            {
                Print("Checking id {0}", position.Id, position.NetProfit);

                if (UseBreakeven == true && position.NetProfit > 0)
                {
                    BreakEven(position);  
                }

                if (ShouldClosePosition(position) == true)
                {
                    var result = ClosePosition(position);
                    if (!result.IsSuccessful)
                    {
                        Print("Could not close!");
                    }
                }
            }
        }

        private bool isGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private void BreakEven(Position position)
        {
            if (MoveStop(position.TradeType) == true )
            {
                Print("MOVE STOP TO {0} SL: {1}", position.EntryPrice + CurrentSymbol.Spread, position.StopLoss);
                ModifyPosition(position, (position.EntryPrice + CurrentSymbol.Spread), position.TakeProfit);
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

        private int GetCloseScore(Position position)
        {

            int stochDPenalty = GetStochDPenalty(position);
            int stochKPenalty = GetStochDPenalty(position);
            int candlePenalty = GetCandlePenalty(position);
            int MA1Penalty = GetMA1Penalty(position);
            int MA2Penalty = GetMA2Penalty(position);

            return (stochDPenalty + stochKPenalty + candlePenalty + MA1Penalty + MA2Penalty);

        }

        private int GetMA2Penalty(Position position) {
       
            int _penalty = 0;

            for(int i = 1; i <= PENALTY_MA2_HISTORY; i++) {
                if (position.TradeType == TradeType.Buy && (_MA2.Result.Last(i) < _MA2.Result.Last(i+1)))
                {
                    Print("MA2 going down - I dont like it");
                    _penalty = _penalty + 1;
                }

                if (position.TradeType == TradeType.Sell && (_MA2.Result.Last(i) > _MA2.Result.Last(i+1)))
                {
                    Print("MA2 going up - I dont like it");
                    _penalty = _penalty + 1;
                }
            }

            return (_penalty * PENALTY_MA2_WEIGHT);

        }

        private int GetMA1Penalty(Position position) {
       
            int _penalty = 0;

            for(int i = 1; i <= PENALTY_MA1_HISTORY; i++) {
                if (position.TradeType == TradeType.Buy && (_MA1.Result.Last(i) < _MA1.Result.Last(i+1)))
                {
                    Print("MA1 going down - I dont like it");
                    _penalty = _penalty + 1;
                }

                if (position.TradeType == TradeType.Sell && (_MA1.Result.Last(i) > _MA1.Result.Last(i+1)))
                {
                    Print("MA1 going up - I dont like it");
                    _penalty = _penalty + 1;
                }
            }

            return (_penalty * PENALTY_MA1_WEIGHT);

        }

        private int GetStochDPenalty(Position position) {
       
            int _penalty = 0;

            for(int i = 1; i <= PENALTY_STOCHD_HISTORY; i++) {
                if (position.TradeType == TradeType.Buy && (_STO.PercentD.Last(i) < _STO.PercentD.Last(i+1)))
                {
                    Print("Stoch D going down - I dont like it");
                    _penalty = _penalty + 1;
                }

                if (position.TradeType == TradeType.Sell && (_STO.PercentD.Last(i) > _STO.PercentD.Last(i+1)))
                {
                    Print("Stoch D going up - I dont like it");
                    _penalty = _penalty + 1;
                }
            }

            return (_penalty * PENALTY_STOCHD_WEIGHT);

        }

        private int GetStochKPenalty(Position position) {
       
            int _penalty = 0;

            Print("{} > {} ? ", _STO.PercentK.Last(1), _STO.PercentK.Last(2));
            if (position.TradeType == TradeType.Buy && (_STO.PercentK.Last(1) < _STO.PercentK.Last(2)))
            {
                Print("Stoch K going down - smells funny");
                _penalty = _penalty + 1;
            }

            if (position.TradeType == TradeType.Sell && (_STO.PercentK.Last(1) > _STO.PercentK.Last(2)))
            {
                Print("Stoch K going up - I dont like it");
                _penalty = _penalty + 1;
            }


            return (_penalty * PENALTY_STOCHK_WEIGHT);

        }

        private int GetCandlePenalty(Position position)
        {
            int _penalty = 0;
            bool _useWeight = true;

            if (isGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1)))
            {
                if (position.TradeType == TradeType.Sell)
                {
                    Print("Green after red = bad");
                    _penalty = _penalty + 1;
                } else {
                    Print("Going the GREEN mile!");
                    _useWeight = false;
                    _penalty = 0;
                }
            }
            else if (!isGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1)))
            {
                if (position.TradeType == TradeType.Buy)
                {
                    Print("Green after green = bad");
                        _penalty = _penalty + 1;
                } else {
                    Print("REDRUM!");
                    _useWeight = false;
                    _penalty = 0;
                }
            }

            // Only weigh candles when going in the oposite direction
            return (_useWeight == true) ? (_penalty * PENALTY_CANDLE_WEIGHT) : _penalty;
        }

        private bool ShouldClosePosition(Position position)
        {

            int currentCloseScore = GetCloseScore(position);

            Print("{0} >= {1} ?", currentCloseScore, CloseScore);
            if (currentCloseScore >= CloseScore)
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
