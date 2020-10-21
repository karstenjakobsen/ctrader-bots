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
        
        [Parameter("Maximum allowed positions", DefaultValue = 1)]
        public int MaxPositions { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }
 
        Symbol CurrentSymbol;

        private Random random = new Random();

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

        private void EnterTrades()
        {
            if(IsEntryPossible() == true) {
                OpenMarketOrder(GetRandomTradeType(), LotSize, StopLoss, TakeProfit);                
            }
        }

        private void OpenMarketOrder(TradeType tradeType, double dLots, double stopLoss, double takeProfitPips)
        {
            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(dLots);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            var result = ExecuteMarketOrder(tradeType, CurrentSymbol.Name, volumeInUnits, cBotLabel, stopLoss, takeProfitPips, null, false);
            if (!result.IsSuccessful) {
                Print("Execute Market Order Error: {0}", result.Error.Value);
                OnStop();
            } else {
                Print(GetMeAQuote(), (tradeType == TradeType.Buy) ? LONG_NAME : SHORT_NAME);
            }
        }

        private TradeType GetRandomTradeType() { 
            return random.Next(2) == 0 ? TradeType.Buy : TradeType.Sell;
        }

        private bool TimeForATrade() {
            double rand = random.Next(2);
            return random.Next(2) == 0 ? true : false;
        }

        private bool IsEntryPossible() {

            if(TimeForATrade() == false) {
                Print("It's not your time yet");
                return false;
            }

            if(MaxPositions > 0 && GetOpenPositions() >= MaxPositions) {
                Print("Max allowed positions met - {0}", MaxPositions);
                return false;
            }

            return true;
        }

        protected override void OnStop() {
 
        }

        private int GetMeARandomNumber(int from, int to) {
            Random rnd = new Random();
            return rnd.Next(from, to+1);
        }

        private string GetMeAQuote(){

            try {

                string[] quotes = {
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

                return quotes[GetMeARandomNumber(0,49)];

            } catch (Exception e)
            {
                return "NO NO NO NO {0} " + e.Message;
            }

        }
    }
}