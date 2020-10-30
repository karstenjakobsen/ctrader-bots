using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_RENKO_RANDOM : Robot
    {

        [Parameter(DefaultValue = "BAMM_RENKO_RANDOM")]
        public string cBotLabel { get; set; }

        [Parameter("Currency pair", DefaultValue = "")]
        public string TradeSymbol { get; set; }

        [Parameter("Lot Size", DefaultValue = 0.1, MinValue = 0.01, Step = 0.01)]
        public double LotSize { get; set; }

        [Parameter("Take Profit", DefaultValue = 10, MinValue = 0.5, Step = 0.5)]
        public double TakeProfit { get; set; }

        [Parameter("Stop Loss", DefaultValue = 10, MinValue = 0.5, Step = 0.5)]
        public double StopLoss { get; set; }

        [Parameter("Entry - Use average in?", DefaultValue = true)]
        public bool UseAverageIn { get; set; }

        [Parameter("Maximum allowed positions", DefaultValue = 1)]
        public int MaxPositions { get; set; }

        [Parameter("Maximum Long positions", DefaultValue = 1)]
        public int MaxLongPositions { get; set; }

        [Parameter("Maximum Short positions", DefaultValue = 1)]
        public int MaxShortPositions { get; set; }

        [Parameter("ADXPeriod", DefaultValue = 4)]
        public int ADXPeriod { get; set; }

        [Parameter("ADXLevel", DefaultValue = 30)]
        public int ADXLevel { get; set; }

        [Parameter("STOCH_KPERIODS", DefaultValue = 6)]
        public int STOCH_KPERIODS { get; set; }

        [Parameter("STOCH_KSLOWING", DefaultValue = 3)]
        public int STOCH_KSLOWING { get; set; }

        [Parameter("STOCH_DPERIODS", DefaultValue = 3)]
        public int STOCH_DPERIODS { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;

        private Random random = new Random();

        private BAMMRenkoUgliness _BAMMRenkoUgliness;
        private DirectionalMovementSystem _DMS;
        private StochasticOscillator _STO;

        private const string LONG_NAME = "Long";
        private const string SHORT_NAME = "Short";

        protected override void OnStart()
        {
            Print("Lets make some trouble! {0}", cBotLabel);

            //check symbol
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);

            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            _BAMMRenkoUgliness = Indicators.GetIndicator<BAMMRenkoUgliness>(1, 0, 25, 25, 25, 25, 25, STOCH_KPERIODS, STOCH_KSLOWING, STOCH_DPERIODS,
            Source);
            _DMS = Indicators.DirectionalMovementSystem(ADXPeriod);
            _STO = Indicators.StochasticOscillator(STOCH_KPERIODS, STOCH_KSLOWING, STOCH_DPERIODS, MovingAverageType.Simple);

        }

        protected override void OnBar()
        {
        }

        protected override void OnTick()
        {
            EnterTrades();
        }

        private bool InRange()
        {
            if (_DMS.ADX.Last(1) < ADXLevel)
            {
                return true;
            }
            return false;
        }

        private int GetOpenPositions()
        {
            return Positions.FindAll(cBotLabel, TradeSymbol).Length;
        }

        private int GetOpenLongPositions()
        {
            return Positions.FindAll(cBotLabel, TradeSymbol, TradeType.Buy).Length;
        }

        private int GetOpenShortPositions()
        {
            return Positions.FindAll(cBotLabel, TradeSymbol, TradeType.Sell).Length;
        }

        private void EnterTrades()
        {
            try
            {
                if (IsLongPossible(TradeType.Buy) == true)
                {
                    double _modLotSize = UseAverageIn ? (LotSize / 2) : LotSize;
                    OpenMarketOrder(TradeType.Buy, _modLotSize, StopLoss, TakeProfit);
                }

                if (IsShortPossible(TradeType.Sell) == true)
                {
                    double _modLotSize = UseAverageIn ? (LotSize / 2) : LotSize;
                    OpenMarketOrder(TradeType.Sell, LotSize, StopLoss, TakeProfit);
                }

            } catch (Exception e)
            {
                Print("ERROR {0}", e);
            }
        }

        private void OpenMarketOrder(TradeType tradeType, double dLots, double stopLoss, double takeProfitPips)
        {

            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(dLots);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            var result = ExecuteMarketOrder(tradeType, CurrentSymbol.Name, volumeInUnits, cBotLabel, stopLoss, takeProfitPips, null, false);

            if (!result.IsSuccessful)
            {
                Print("Execute Market Order Error: {0}", result.Error.Value);
                OnStop();
            }
            else
            {
                Print(GetMeAQuote(), (tradeType == TradeType.Buy) ? LONG_NAME : SHORT_NAME);
            }
        }

        private double GetCloseScore(TradeType tradeType)
        {
            if (tradeType == TradeType.Buy)
            {
                return _BAMMRenkoUgliness.CloseLong.Last(1);
            }
            else
            {
                return _BAMMRenkoUgliness.CloseShort.Last(1);
            }
        }

        private bool RollForEntry(double penalty, TradeType tradeType)
        {
            // Only trade below 50 and not rising
            if (penalty < 50 && (tradeType == TradeType.Buy && !isLongPenaltyRising()) || (tradeType == TradeType.Sell && !isShortPenaltyRising()))
            {
                if (tradeType == TradeType.Buy && Bars.HighPrices.Last(0) > Bars.ClosePrices.Last(1))
                {
                    Print("I want better prices for my longs");
                    return false;
                }

                if (tradeType == TradeType.Sell && Bars.LowPrices.Last(0) < Bars.ClosePrices.Last(1))
                {
                    Print("I want better prices for my shorts");
                    return false;
                }

                // int roll = random.Next(1, 101);
                // double finalPenalty = 0;

                // finalPenalty = penalty - 15;

                // Print("Rolled a {0}, orignal penalty is {1}, modified {2} for {3}", roll, penalty, finalPenalty, tradeType);

                // if (finalPenalty >= roll)
                // {
                //     Print("No trade! {0} >= {1}", finalPenalty, roll);
                //     return false;
                // }

                return true;
            }

            return false;
        }

        // If penalty is rising, then add a modfier. 
        // Rising penalty = BAD KARMA! So we are adding the level to 100 for it to always be added hen rising
        // private double RisingPenaltyScoreAdjustment(double penalty, TradeType tradeType){

        //     if ( (tradeType == TradeType.Buy && isLongPenaltyRising() ) || (tradeType == TradeType.Sell && isShortPenaltyRising() ) )
        //     {
        //         Print("Penalty rising {0} + {1} for {2} - Worst opportinuty!", penalty, 100, tradeType);
        //         return (penalty + 100);
        //     }

        //     return penalty;
        // }

        // If penalty is falling, then add a modfier. 
        // Falling penalty = GOOD!
        private double FallingPenaltyScoreAdjustment(double penalty, TradeType tradeType)
        {

            if ((tradeType == TradeType.Buy && isLongPenaltyFalling() && isShortPenaltyRising()) || (tradeType == TradeType.Sell && isShortPenaltyFalling() && isLongPenaltyRising()))
            {
                Print("Penalty falling {0} - 10 for {1} - BEST China!", penalty, tradeType);
                return (penalty - 10);
            }

            return penalty;
        }

        private bool isLongPenaltyFalling()
        {
            return (_BAMMRenkoUgliness.CloseLong.Last(1) < _BAMMRenkoUgliness.CloseLong.Last(2)) ? true : false;
        }

        private bool isLongPenaltyRising()
        {
            return (_BAMMRenkoUgliness.CloseLong.Last(1) > _BAMMRenkoUgliness.CloseLong.Last(2)) ? true : false;
        }

        private bool isShortPenaltyFalling()
        {
            return (_BAMMRenkoUgliness.CloseShort.Last(1) < _BAMMRenkoUgliness.CloseShort.Last(2)) ? true : false;
        }

        private bool isShortPenaltyRising()
        {
            return (_BAMMRenkoUgliness.CloseShort.Last(1) > _BAMMRenkoUgliness.CloseShort.Last(2)) ? true : false;
        }

        private bool IsLongPossible(TradeType tradeType)
        {

            if (IsEntryPossible() == false)
            {
                return false;
            }

            if (MaxLongPositions > 0 && GetOpenLongPositions() >= MaxLongPositions)
            {
                return false;
            }

            return RollForEntry(_BAMMRenkoUgliness.CloseLong.Last(1), tradeType);
            // return MaxLongOpportunity();

        }

        private bool IsShortPossible(TradeType tradeType)
        {

            if (IsEntryPossible() == false)
            {
                return false;
            }

            if (MaxShortPositions > 0 && GetOpenShortPositions() >= MaxShortPositions)
            {
                return false;
            }

            return RollForEntry(_BAMMRenkoUgliness.CloseShort.Last(1), tradeType);

        }

        private bool IsEntryPossible()
        {

            if (MaxPositions > 0 && GetOpenPositions() >= MaxPositions)
            {
                return false;
            }

            return true;

        }

        protected override void OnStop()
        {
            Stop();
        }

        private int GetMeARandomNumber(int @from, int to)
        {
            Random rnd = new Random();
            return rnd.Next(@from, to + 1);
        }

        private string GetMeAQuote()
        {

            try
            {

                string[] quotes = 
                {
                    "May the {0} be with you",
                    "There's no place like {0}",
                    "I'm the {0} of the world",
                    "Carpe diem. Seize the day, {0}. Make your lives extraordinary",
                    "Elementary, my dear {0}",
                    "It's alive! It's {0}",
                    "My mama always said life was like a box of chocolates. You never know what you're gonna {0}",
                    "I'll be {0}",
                    "You're gonna need a bigger {0}",
                    "Here's looking at you, {0}",
                    "My precious {0}",
                    "Houston, we have a {0}",
                    "There's no {0} in baseball",
                    "E.T. phone {0}",
                    "You can't handle the {0}",
                    "A {0}. Shaken, not stirred",
                    "Life is a banquet, and most poor suckers are {0} to death",
                    "If you build it, {0} will come",
                    "The {0} that dreams are made of",
                    "Magic {0} on the wall, who is the fairest one of all?",
                    "Keep your friends close, but your {0} closer",
                    "I am your {0}",
                    "Just keep {0}",
                    "Today, I consider myself the luckiest {0} on the face of the earth",
                    "You is kind. You is smart. You is {0}",
                    "What we've got here is failure to {0}",
                    "Hasta la vista, {0}",
                    "You don't understand! I coulda had class. I coulda been a contender. I could've been somebody, instead of a {0}, which is what I am",
                    "{0}. James {0}.",
                    "You talking to me",
                    "{0}? Where we're going we don't need {0}",
                    "That'll do, {0}. That'll do",
                    "I'm {0}'ing here! I'm {0}'ing here",
                    "It was beauty killed the {0}.",
                    "Stella! Hey, {0}",
                    "As if! {0}",
                    "Here's {0}",
                    "Rosebud {0}",
                    "I'll {0} what she's having",
                    "Inconceivable {0}",
                    "All right, Mr. DeMille, I'm ready for my {0}",
                    "Fasten your seatbelts. It's going to be a {0} night",
                    "Nobody puts {0} in a corner",
                    "Well, nobody's {0}",
                    "{0} out of it",
                    "You had me at ‘{0}.’",
                    "They may take our {0}, but they'll never take our freedom!",
                    "To {0} and beyond",
                    "You’re killin’ me, {0}",
                    "Toto, I've a feeling we're not in {0} anymore"
                };

                return quotes[GetMeARandomNumber(0, 49)];

            } catch (Exception e)
            {
                return "NO NO NO NO {0} " + e.Message;
            }

        }
    }
}
