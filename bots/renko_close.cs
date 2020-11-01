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

        [Parameter("Stop Loss", DefaultValue = 10)]
        public double StopLossPips { get; set; }

        // No positions allowed if max daily loss is reached
        [Parameter("Max Daily Loss - EUR", DefaultValue = 1000)]
        public double MAX_DAILY_LOSS { get; set; }

        [Parameter("Position Size Risk %", DefaultValue = 0.25)]
        public double POSITION_RISK_PERCENT { get; set; }

        // No positions allowed if daily profit is reached for all pairs
        [Parameter("Lock In Daily Profit %", DefaultValue = 2)]
        public double LOCK_IN_DAILY_PROFIT_PERCENT { get; set; }

        // Used to take profits during an open position
        [Parameter("Lock In Profit %", DefaultValue = 0.5)]
        public double LOCK_IN_PROFIT_PERCENT { get; set; }

        // [Parameter("Entry - Use average in?", DefaultValue = false)]
        // public bool UseAverageIn { get; set; }

        // [Parameter("Average In %", DefaultValue = 30)]
        // public double AVERAGE_IN_PERCENT { get; set; }

        [Parameter("STOCH_KPERIODS", DefaultValue = 6)]
        public int STOCH_KPERIODS { get; set; }

        [Parameter("STOCH_KSLOWING", DefaultValue = 3)]
        public int STOCH_KSLOWING { get; set; }

        [Parameter("STOCH_DPERIODS", DefaultValue = 3)]
        public int STOCH_DPERIODS { get; set; }

        [Parameter("ADXLevel", DefaultValue = 30)]
        public int ADXLevel { get; set; }

        [Parameter("ADXPeriod", DefaultValue = 4)]
        public int ADXPeriod { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;
        private BAMMRenkoUgliness _BAMMRenkoUgliness;
        private DirectionalMovementSystem _DMS;

        private bool _IsLastBlockGreen;

        private bool _marketOpen;

        private Random random = new Random();

        protected override void OnStart()
        {

            // Subscribe to events
            Positions.Opened += PositionsOnOpened;

            // Set currency
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);

            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            _BAMMRenkoUgliness = Indicators.GetIndicator<BAMMRenkoUgliness>(1, 0, 20, 20, 20, 20, 20, STOCH_KPERIODS, STOCH_KSLOWING, STOCH_DPERIODS, Source);
            _DMS = Indicators.DirectionalMovementSystem(ADXPeriod);

            SetMarketStatus();

            UpdateDisplay();

            Print("I'm watching you! {0}", FollowLabel);
        }

        private void UpdateDisplay()
        {
            // Show position size
            DisplayPositionSizeRiskOnChart();

            // Market status
            DisplayMarketStatus();
        }

        private void DisplayPositionSizeRiskOnChart()
        {
            string text = POSITION_RISK_PERCENT + "% x " + StopLossPips + "pip = " + GetPositionSizeInLots() + " lots";
            Chart.DrawStaticText("positionRisk", text, VerticalAlignment.Top, HorizontalAlignment.Right, Color.Yellow);
        }

        private double GetPositionSize()
        {
            double positionSizeForRisk = ((Account.Balance * POSITION_RISK_PERCENT) / 100) / (StopLossPips * CurrentSymbol.PipValue);
            return CurrentSymbol.NormalizeVolumeInUnits(positionSizeForRisk, RoundingMode.Up);
        }

        private double GetPositionSizeInLots()
        {
            return Math.Round(CurrentSymbol.VolumeInUnitsToQuantity(GetPositionSize()),2);
        }

        private void SetMarketStatus()
        {
            if( (Server.Time.Hour >= 7 && Server.Time.Hour < 10) || (Server.Time.Hour >= 12 && Server.Time.Hour < 16) ) {       
                _marketOpen = true;
                return;
            }

            _marketOpen = false;

        }

        private void DisplayMarketStatus()
        {
            if(_marketOpen == true)
            {
                Chart.DrawStaticText("marketStatus", "MARKET IS OPEN", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Green);
            }
            else
            {
                Chart.DrawStaticText("marketStatus", "MARKET IS CLOSED", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Red);
            }   
            
        }

        protected override void OnBar()
        {
            _IsLastBlockGreen = IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));

            SetMarketStatus();
            
            // Run all checks
            RunChecks();
            
            // Update display text
            UpdateDisplay();

        }

        private void PositionsOnOpened(PositionOpenedEventArgs args)
        {
            var position = args.Position;

            // Auto size all positions without comment
            if( position.Comment == "" )
            {
                // Check if market is open
                if( _marketOpen )
                {
                    // Check if daily loss/profit limit is hit
                    if( DailyNetProfitLossHit(position) == false )
                    {
                        Print("Cloning position...");

                        // Get new quantity from risk management
                        var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(GetPositionSizeInLots());
                        volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
                        var result = ExecuteMarketOrderAsync(position.TradeType, position.SymbolName, volumeInUnits, cBotLabel, StopLossPips, 0, "auto_size", false);
                    }
                    
                }

                ClosePosition(position);
            }

        }        

        protected override void OnTick()
        {
            SetMarketStatus();

            // Check for profit
            var positions = Positions.Where(x => x.Label == FollowLabel && x.SymbolName == CurrentSymbol.Name);

            foreach (Position position in positions)
            {

                if (UseBreakeven == true && position.Pips >= BreakEvenPips && position.HasTrailingStop == false && PositionHasBreakeven(position) == false)
                {  
                    BreakEven(position);                     
                }

                if(position.HasTrailingStop == false)
                {
                    // Check for profit taking
                    LockInProfits(position);
                }

                
            }

        }

        private bool PositionHasBreakeven(Position position) {
            if ( (position.TradeType == TradeType.Buy && position.EntryPrice > position.StopLoss) || (position.TradeType == TradeType.Sell && position.EntryPrice < position.StopLoss))
            {
                return false;
            }
            return true;
        }

        private bool DailyNetProfitLossHit(Position position)
        {
            return false;
        }

        protected void LockInProfits(Position position)
        {
            
            if (GetNetProfitPercentage(position.NetProfit) >= LOCK_IN_PROFIT_PERCENT)
            {
                var units = CurrentSymbol.QuantityToVolumeInUnits(position.Quantity/3);
                units = CurrentSymbol.NormalizeVolumeInUnits(units, RoundingMode.Up);

                // Take half risk of the table
                ModifyPosition(position, units);
                Print("Taking some risk off the table!");
            }
        }

        private double GetNetProfitPercentage(double netProfit)
        {
            double balance = Account.Balance;
            return Math.Round((netProfit / balance) * 100, 2);
        }

        // private void AverageIntoPosition(Position position)
        // {
        //     var currentvolumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(position.Quantity);
        //     currentvolumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(currentvolumeInUnits, RoundingMode.Up);

        //     var addvolumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(position.Quantity * (AVERAGE_IN_PERCENT/100));
        //     addvolumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(addvolumeInUnits, RoundingMode.Up);

        //     if (_IsLastBlockGreen == false && position.TradeType == TradeType.Buy)
        //     {
        //         Print("Average in here with {0} to {1}", addvolumeInUnits, currentvolumeInUnits);
        //         ModifyPosition(position, (addvolumeInUnits + currentvolumeInUnits));
        //     }

        //     if (_IsLastBlockGreen == true && position.TradeType == TradeType.Sell)
        //     {
        //         Print("Average in here with {0} to {1}", addvolumeInUnits, currentvolumeInUnits);
        //         ModifyPosition(position, (addvolumeInUnits + currentvolumeInUnits));
        //     }
        // }

        private void RunChecks()
        {
            var positions = Positions.Where(x => x.Label == FollowLabel && x.SymbolName == CurrentSymbol.Name);

            foreach (Position position in positions)
            {
                
                // if (UseAverageIn && position.Pips < 0)
                // {
                //     AverageIntoPosition(position);
                // }

                // Dont active TSL before there is a little room
                if (position.Pips >= BreakEvenPips)
                {
                    ActivateTrailingStop(position);
                }

                // Only close when is going oppsit way
                if( position.HasTrailingStop == false && TryToClosePosition(position) == true)
                {
                    Print("Closing position...");
                    ClosePosition(position);
                }
            }
        }

        private bool IsGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private TradeResult BreakEven(Position position)
        {
            // double newPrice = (position.TradeType == TradeType.Buy) ? (position.EntryPrice + (2*CurrentSymbol.Spread)) : (position.EntryPrice - (2*CurrentSymbol.Spread));
            // ModifyPosition(position, newPrice, position.TakeProfit);   
            return position.ModifyStopLossPips((2*CurrentSymbol.Spread));
        }

        private int CountConsecutiveCloseScore(TradeType tradeType, int bars, int index, double score)
        {
            int count = 0;

            for (int i = 1; i <= bars; i++)
            {
                if (GetCloseScore(tradeType, index) == score)
                {
                    count++;
                }
                else
                {
                    return count;
                }
            }

            return count;
        }

        private bool ActivateTrailingStop(Position position)
        {
            // When trending we want more zeroes
            int _closeLong = CountConsecutiveCloseScore(TradeType.Buy, 2, 1, 0);
            int _closeShort = CountConsecutiveCloseScore(TradeType.Sell, 3, 1, 0);

            if (position.TradeType == TradeType.Sell && ( (InRange() && _closeShort == 2) || (InRange() == false && _closeShort == 3) ) && _IsLastBlockGreen == false)
            {
                if (position.HasTrailingStop == false)
                {
                    // Can only move SL on red candles
                    Print("Starting to see a SHORT exit - Moving SL CQ {0} VEL {1}", _closeShort, TrendingVelocity());
                    // Modify SL just above last red open. If trending then give it a little more room
                    ModifyPosition(position, (Bars.OpenPrices.Last(1) + (2 * CurrentSymbol.Spread)), 0, true);
                }
            }
            else if (position.TradeType == TradeType.Buy && ( (InRange() && _closeShort == 1) || (InRange() == false && _closeShort == 2) ) && _IsLastBlockGreen == true)
            {

                if (position.HasTrailingStop == false)
                {
                    // Can only move SL on green candles
                    Print("Starting to see a LONG exit - Moving SL CQ {0} VEL {1}", _closeLong, TrendingVelocity());
                    // Modify SL just below last green open
                    ModifyPosition(position, (Bars.OpenPrices.Last(1) - (2 * CurrentSymbol.Spread)), 0, true);
                }
            }

            return false;
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

        private bool RollForClose(Position position)
        {

            int roll = random.Next(1, 101);
            double closeScore = 100 - GetCloseScore(position.TradeType);

            Print("Rolled a {0}, close score is {1}. {0} > {1} ?", roll, closeScore);

            if (roll > closeScore)
            {
                return true;
            }

            return false;
        }

        private bool InRange()
        {
            if (_DMS.ADX.Last(0) < ADXLevel)
            {
                return true;
            }
            return false;
        }

        private double TrendingVelocity(int period = 3)
        {
            double velocity = 0;

            for(int i = 0; i < period; i++)
            {
                velocity += _DMS.ADX.Last(i);
            }

            return Math.Round(Math.Abs(velocity/period),2);
        }

        // // High roll == Profit
        // private bool RollForProfit(Position position)
        // {

        //     double _originalRoll = random.Next(1, 101);
        //     double _modifiedRoll = _originalRoll;
        //     double _pips = Math.Ceiling(position.Pips);
        //     double _RRRLevel = Math.Floor(_pips / TargetPips);
        //     double _profitPenalty = 100 - (_RRRLevel * 5);

        //     if (_RRRLevel < 1)
        //     {
        //         Print("RRR level too low {0}", _RRRLevel);
        //         return false;
        //     }

        //     // add 10% chance to roll for each RRR above 1
        //     for (int i = 2; i <= _RRRLevel; i++)
        //     {
        //         _modifiedRoll += 10;
        //         Print("Added RRR level +10 to roll - RRR level is {0}", _RRRLevel);
        //     }

        //     if (InRange() == true)
        //     {
        //         Print("Added range +50 to roll");
        //         _modifiedRoll += 50;
        //     }

        //     Print("Rolled a {0} - modified {1}, RRR score is {2}. {1} > {2} ?", _originalRoll, _modifiedRoll, _profitPenalty);
        //     if (_modifiedRoll > _profitPenalty)
        //     {
        //         return true;
        //     }

        //     return false;
        // }

        private bool TryToClosePosition(Position position)
        {
            // Dont close if negative. Let SL do that
            if (position.NetProfit < 0)
            {
                Print("Dont close - profit negative");
                return false;
            }

            if( _IsLastBlockGreen && position.TradeType == TradeType.Buy ) {
                Print("Going the Green mile!");
                return false;
            }

            if( !_IsLastBlockGreen && position.TradeType == TradeType.Sell ) {
                Print("Redrum!");
                return false;
            }

            return RollForClose(position);
        }

    }
}
