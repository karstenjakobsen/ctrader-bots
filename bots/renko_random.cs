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

        [Parameter(DefaultValue = "K1")]
        public string cBotLabel { get; set; }

        [Parameter("Currency pair", DefaultValue = "")]
        public string TradeSymbol { get; set; }

        [Parameter("Lot Size", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01)]
        public double LotSize { get; set; }

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

        [Parameter("STOCH_KPERIODS", DefaultValue = 6)]
        public int STOCH_KPERIODS { get; set; }

        [Parameter("STOCH_KSLOWING", DefaultValue = 3)]
        public int STOCH_KSLOWING { get; set; }

        [Parameter("STOCH_DPERIODS", DefaultValue = 3)]
        public int STOCH_DPERIODS { get; set; }

        [Parameter("ADXPeriod", DefaultValue = 4)]
        public int ADXPeriod { get; set; }

        [Parameter("ADXLevel", DefaultValue = 28)]
        public int ADXLevel { get; set; }

        [Parameter("Use Market Hours", DefaultValue = true, Group="Behavior")]
        public bool UseMarketHours { get; set; }

        [Parameter("Use Addition", DefaultValue = false, Group="Behavior")]
        public bool UseAddition { get; set; }

        [Parameter("Block Size", DefaultValue = 10)]
        public int BlockSize { get; set; }
        


        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;

        private Random random = new Random();

        private BAMMRenkoUgliness _BAMMRenkoUgliness;
        private DirectionalMovementSystem _DMS;
        private const string LONG_NAME = "Long";
        private const string SHORT_NAME = "Short";
        private bool _IsLastBlockGreen;

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

            _BAMMRenkoUgliness = Indicators.GetIndicator<BAMMRenkoUgliness>(1, 0, 0, 0, 15, 100, 15, 15, 15, ADXLevel, false, STOCH_KPERIODS, STOCH_KSLOWING, STOCH_DPERIODS, Source);
            _DMS = Indicators.DirectionalMovementSystem(ADXPeriod);

        }

        protected override void OnBar()
        {

            CancelPendingOrders();
            
            EnterTrades();

            if( UseAddition )
            {
                 _IsLastBlockGreen = IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));

                //CheckAddition();
            }
        }

        private bool IsGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private bool PositionHasBreakeven(Position position)
        {
            if ( (position.TradeType == TradeType.Buy && position.EntryPrice > position.StopLoss) || (position.TradeType == TradeType.Sell && position.EntryPrice < position.StopLoss))
            {
                return false;
            }
            return true;
        }

        // private void CheckAddition()
        // {
        //     var positions = GetOpenPositions();

        //     foreach (Position position in positions)
        //     {        
        //         double catchPrice = 0;

        //         // Open a limit order lower og higher and penalty is falling again.
        //         if( PositionHasBreakeven(position) == true && position.TradeType == TradeType.Buy && isLongPenaltyFalling() )
        //         {
        //             // Open new limit order
        //             var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(LotSize);
        //             volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

        //             catchPrice = _IsLastBlockGreen ? Bars.OpenPrices.Last(1) : (Bars.ClosePrices.Last(0) - (BlockSize-CurrentSymbol.Spread));
        //             Print("Hoping to catch this UPTREND lower at {0}", catchPrice);
        //             PlaceLimitOrderAsync(TradeType.Sell, CurrentSymbol.Name, volumeInUnits, catchPrice, cBotLabel, StopLoss, TakeProfit, null, cBotLabel, false);
        //         }
        //         else if( PositionHasBreakeven(position) == true && position.TradeType == TradeType.Sell && isShortPenaltyFalling() )
        //         {
        //             // Open new limit order
        //             var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(LotSize);
        //             volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

        //             catchPrice = !_IsLastBlockGreen ? Bars.OpenPrices.Last(1) : (Bars.ClosePrices.Last(0) + (BlockSize-CurrentSymbol.Spread));
        //             Print("Hoping to catch this DOWNTREND higher at {0}", catchPrice);
        //             PlaceLimitOrderAsync(TradeType.Sell, CurrentSymbol.Name, volumeInUnits, catchPrice, cBotLabel, StopLoss, TakeProfit, null, cBotLabel, false);
        //         }
        //     }
             
        // }

        protected override void OnTick()
        {
            
        }

        private Position[] GetOpenPositions()
        {
            return Positions.FindAll("K_AUTO_SIZE", TradeSymbol);
        }

        private int GetOpenPositionsCount()
        {
            return GetOpenPositions().Length;
        }

        private Position[] GetOpenLongPositions()
        {
            return Positions.FindAll("K_AUTO_SIZE", TradeSymbol, TradeType.Buy);
        }

        private int GetOpenLongPositionsCount()
        {
            return GetOpenLongPositions().Length;
        }

        private Position[] GetOpenShortPositions()
        {
            return Positions.FindAll("K_AUTO_SIZE", TradeSymbol, TradeType.Sell);
        }

        private int GetOpenShortPositionsCount()
        {
            return GetOpenShortPositions().Length;
        }

        private void EnterTrades()
        {
            try
            {
                
                if (IsLongPossible(TradeType.Buy) == true)
                {
                    OpenOrder(TradeType.Buy, LotSize, StopLoss, TakeProfit);
                }
                
                if (IsLongPossible(TradeType.Sell) == true)
                {
                    OpenOrder(TradeType.Sell, LotSize, StopLoss, TakeProfit);
                }                

            } catch (Exception e)
            {
                Print("ERROR {0}", e);
            }
        }

        private void CancelPendingOrders()
        {

            // Print("Found {0} pending orders for label {1} {2}", PendingOrders.Count(x => x.Label == cBotLabel && x.SymbolName == CurrentSymbol.Name), cBotLabel, CurrentSymbol.Name);
            var orders = PendingOrders.Where(x => x.Label == cBotLabel && x.SymbolName == CurrentSymbol.Name);

            foreach (var order in orders)
            {
                var result = CancelPendingOrderAsync(order);
            }
        }

        private void OpenOrder(TradeType tradeType, double dLots, double stopLoss, double takeProfitPips)
        {

            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(dLots);
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            ExecuteMarketOrderAsync(tradeType, CurrentSymbol.Name, volumeInUnits, cBotLabel, stopLoss, takeProfitPips, cBotLabel, false);
            Print(GetMeAQuote(), (tradeType == TradeType.Buy) ? LONG_NAME : SHORT_NAME);
            
        }

        private double GetCloseScore(TradeType tradeType, int index = 1)
        {
            if (tradeType == TradeType.Buy)
            {
                return _BAMMRenkoUgliness.CloseLong.Last(index);
            }
            else
            {
                return _BAMMRenkoUgliness.CloseShort.Last(index);
            }
        }

        private bool InRange(int index = 1)
        {
            if (_DMS.ADX.Last(index) < ADXLevel)
            {
                return true;
            }

            return false;
        }

        private bool RollForEntry(double penalty, TradeType tradeType)
        {
            // Only trade when falling
            if ( (tradeType == TradeType.Buy && isLongPenaltyFalling()) || (tradeType == TradeType.Sell && isShortPenaltyFalling()) )
            {

                int roll = random.Next(1, 101);
                double finalPenalty = penalty;

                Print("Rolled a {0}, orignal penalty is {1}, modified {2} for {3}", roll, penalty, finalPenalty, tradeType);

                if (finalPenalty >= roll)
                {
                    Print("No trade! {0} >= {1}", finalPenalty, roll);
                    return false;
                }

                return true;
            }

            return false;
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

            if (MaxLongPositions > 0 && GetOpenLongPositionsCount() >= MaxLongPositions)
            {
                return false;
            }

            return RollForEntry(_BAMMRenkoUgliness.CloseLong.Last(1), tradeType);

        }

        private bool IsShortPossible(TradeType tradeType)
        {

            if (IsEntryPossible() == false)
            {
                return false;
            }

            if (MaxShortPositions > 0 && GetOpenShortPositionsCount() >= MaxShortPositions)
            {
                return false;
            }

            return RollForEntry(_BAMMRenkoUgliness.CloseShort.Last(1), tradeType);

        }

        private bool IsMarketOpen()
        {
            if( (Server.Time.Hour >= 7 && Server.Time.Hour < 10) || (Server.Time.Hour >= 12 && Server.Time.Hour < 16) ) {     
                if( Server.Time.Hour == 7 && Server.Time.Minute < 45 ) {
                    return UseMarketHours ? false : true;
                }
                if( Server.Time.Hour == 12 && Server.Time.Minute < 30 ) {
                    return UseMarketHours ? false : true;
                }
                return true;
            }

            return UseMarketHours ? false : true;

        }

        private bool IsEntryPossible()
        {

            if (!IsMarketOpen())
            {
                Print("Market is closed");
                return false;
            }

            if (MaxPositions > 0 && GetOpenPositionsCount() >= MaxPositions)
            {
                return false;
            }

            return true;

        }

        protected override void OnStop()
        {
            Stop();
        }

        private int GetMeARandomNumber(int from, int to)
        {
            Random rnd = new Random();
            return rnd.Next(from, to + 1);
        }

        private TradeType GetMeARandomTrade()
        {
            return GetMeARandomNumber(1,100) > 50 ? TradeType.Buy : TradeType.Sell;
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
