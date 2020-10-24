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

        [Parameter("Entry - Use rising penalty", DefaultValue = true)]
        public bool UseRisingPenalty { get; set; }

        [Parameter("Entry - Rising penalty", DefaultValue = 100)]
        public double RisingPenaltyWeight { get; set; }

        [Parameter("Take Profit", DefaultValue = 10, MinValue = 0.5, Step = 0.5)]
        public double TakeProfit { get; set; }

        [Parameter("Stop Loss", DefaultValue = 10, MinValue = 0.5, Step = 0.5)]
        public double StopLoss { get; set; }

        [Parameter("Maximum allowed positions", DefaultValue = 1)]
        public int MaxPositions { get; set; }

        [Parameter("Maximum Long positions", DefaultValue = 1)]
        public int MaxLongPositions { get; set; }

        [Parameter("Maximum Short positions", DefaultValue = 1)]
        public int MaxShortPositions { get; set; }

        [Parameter("Max Spread", DefaultValue = 2, Step = 0.5)]
        public double MaxSpread { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;

        private Random random = new Random();

        private BAMMRenkoClose _BAMMRenkoClose;

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

            _BAMMRenkoClose = Indicators.GetIndicator<BAMMRenkoClose>(1, 5, 25, 25, 25, 25, 25, 25, 65, 35, 75, 75, 25, 100, Source);

        }

        protected override void OnBar()
        {
            EnterTrades();
        }

        protected override void OnTick()
        {

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
                    OpenMarketOrder(TradeType.Buy, LotSize, StopLoss, TakeProfit);
                }

                if (IsShortPossible(TradeType.Sell) == true)
                {
                    OpenMarketOrder(TradeType.Sell, LotSize, StopLoss, TakeProfit);
                }
            } catch(Exception e) {
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

        private TradeType GetRandomTradeType()
        {
            return random.Next(2) == 0 ? TradeType.Buy : TradeType.Sell;
        }

        private double GetCloseScore(TradeType tradeType)
        {
            if (tradeType == TradeType.Buy)
            {
                return _BAMMRenkoClose.CloseLong.Last(1);
            }
            else
            {
                return _BAMMRenkoClose.CloseShort.Last(1);
            }
        }

        private bool RollDice(double penalty, TradeType tradeType)
        {

            int roll = random.Next(1, 101);
            double finalPenalty = 0;

            finalPenalty = RisingPenaltyScoreAdjustment(penalty, tradeType);

            Print("Rolled a {0}, orignal penalty is {1}, modified {2}", roll, penalty, finalPenalty);

            if (finalPenalty >= roll)
            {
                Print("No trade! {0} >= {1}", finalPenalty, roll);
                return false;
            }

            return true;
        }

        // If penalty is rising from the RisingPenaltyLevel (the bottom), then add a modfier. 
        // Rising penalty = BAD KARMA! So we are adding the level to 100 for it to always be added hen rising
        private double RisingPenaltyScoreAdjustment(double penalty, TradeType tradeType){
            if (UseRisingPenalty == true)
            {
                if ( (tradeType == TradeType.Buy && isLongPenaltyRising() ) || (tradeType == TradeType.Sell && isShortPenaltyRising() ) )
                {
                    Print("Penalty rising {0} + {1} for {2} - Worst opportinuty!", penalty, RisingPenaltyWeight, tradeType);
                    return (penalty + RisingPenaltyWeight);
                }
            }
            return penalty;
        }

        private bool isLongPenaltyFalling() {
            return (_BAMMRenkoClose.CloseLong.Last(1) < _BAMMRenkoClose.CloseLong.Last(2)) ? true : false;
        }

        private bool isLongPenaltyRising() {
            return (_BAMMRenkoClose.CloseLong.Last(1) > _BAMMRenkoClose.CloseLong.Last(2)) ? true : false;
        }

        private bool isShortPenaltyFalling() {
            return (_BAMMRenkoClose.CloseShort.Last(1) < _BAMMRenkoClose.CloseShort.Last(2)) ? true : false;
        }

        private bool isShortPenaltyRising() {
            return (_BAMMRenkoClose.CloseShort.Last(1) > _BAMMRenkoClose.CloseShort.Last(2)) ? true : false;
        }

        private bool IsLongPossible(TradeType tradeType)
        {
            
            if (IsEntryPossible() == false)
            {
                return false;
            }

            if (MaxLongPositions > 0 && GetOpenLongPositions() >= MaxLongPositions)
            {
                Print("Max long positions met - {0}", MaxLongPositions);
                return false;
            }

            // If long score is at the bottom and the short penalty is falling, that means that long opporunity has passed
            if (isShortPenaltyFalling() && _BAMMRenkoClose.CloseLong.Last(1) <= 10)
            {
                Print("Long opportunity gone - {0}", _BAMMRenkoClose.CloseLong.Last(1));
                return false;
            }

            return RollDice(_BAMMRenkoClose.CloseLong.Last(1), tradeType);

        }

        private bool IsShortPossible(TradeType tradeType)
        {

            if (IsEntryPossible() == false)
            {
                return false;
            }

            if (MaxShortPositions > 0 && GetOpenShortPositions() >= MaxShortPositions)
            {
                Print("Max short positions met - {0}", MaxShortPositions);
                return false;
            }

            // If short score is at the bottom and the long score is falling, that means that short opporunity has passed
            if (isLongPenaltyFalling() && _BAMMRenkoClose.CloseShort.Last(1) <= 10)
            {
                Print("Short opportunity gone - {0}", _BAMMRenkoClose.CloseShort.Last(1));
                return false;
            }

            return RollDice(_BAMMRenkoClose.CloseShort.Last(1), tradeType);

        }

        private bool IsEntryPossible()
        {

            // Print("Spread {0} < {1}", (CurrentSymbol.Spread / CurrentSymbol.PipValue), MaxSpread);

            if (MaxPositions > 0 && GetOpenPositions() >= MaxPositions)
            {
                Print("Max allowed positions met - {0}", MaxPositions);
                return false;
            }

            if ((CurrentSymbol.Spread / CurrentSymbol.PipValue) > MaxSpread)
            {
                Print("Spread is too large {0} {1}", MaxSpread, (CurrentSymbol.Spread / CurrentSymbol.PipValue));
                return false;
            }

            return true;

        }

        protected override void OnStop()
        {

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
