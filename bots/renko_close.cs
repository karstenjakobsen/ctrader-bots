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

        [Parameter(DefaultValue = "BAMM_RENKO_CLOSE", Group="ID")]
        public string cBotLabel { get; set; }

        [Parameter("Currency pair", DefaultValue = "", Group="ID")]
        public string TradeSymbol { get; set; }

        [Parameter("Follow Comment", DefaultValue = "", Group="ID")]
        public string FollowComment { get; set; }

        [Parameter("Breakeven", DefaultValue = 10, Group="Pips")]
        public double BreakEvenPips { get; set; }

        [Parameter("Half Breakeven", DefaultValue = 3, Group="Pips")]
        public double HalfBreakEvenPips { get; set; }

        [Parameter("Stop Loss", DefaultValue = 10, Group="Pips")]
        public double StopLossPips { get; set; }

        [Parameter("Trailing Stop Pips", DefaultValue = 0, Group="Pips")]
        public double TrailingStopPips { get; set; }

        // No positions allowed if max daily loss is reached
        [Parameter("Max Daily Drawdown %", DefaultValue = 2, Group="Risk")]
        public double MaxDDDPercent { get; set; }

        [Parameter("Position Size Risk %", DefaultValue = 0.25, Group="Risk")]
        public double PositionRiskPercent { get; set; }

        [Parameter("Account Size - EUR", DefaultValue = 80000, Group="Risk")]
        public double AccountStartSize { get; set; }

        // No positions allowed if daily profit is reached for all pairs
        [Parameter("Lock In Daily Profit %", DefaultValue = 2, Group="Risk")]
        public double LockInDailyProfitPercent { get; set; }

        // Used to take profits during an open position
        [Parameter("Lock In Profit %", DefaultValue = 0.5, Group="Risk")]
        public double LockInProfitPercent { get; set; }

        [Parameter("Cooldown seconds", DefaultValue = 360, Group="Risk")]
        public int CoolDownSeconds { get; set; }

        [Parameter("Close Negative When Ugly", DefaultValue = false, Group="Behavior")]
        public bool AllowCloseWhenUglyNegative { get; set; }

        [Parameter("Use Trailing Stops On Profit", DefaultValue = true, Group="Behavior")]
        public bool UseTrailingStopsOnProfit { get; set; }

        [Parameter("Use Trailing Stops On Beakeven", DefaultValue = true, Group="Behavior")]
        public bool UseTrailingStopsOnBreakeven { get; set; }

        [Parameter("Use Breakeven On Profit", DefaultValue = true, Group="Behavior")]
        public bool UseBreakevenOnProfit { get; set; }

        [Parameter("Use Addition", DefaultValue = false, Group="Behavior")]
        public bool UseAddition { get; set; }

        [Parameter("Addition Close Score", DefaultValue = 100, Group="Behavior")]
        public int AddPositionCloseScore { get; set; }

        [Parameter("Use Auto Sizing", DefaultValue = true, Group="Behavior")]
        public bool UseAutoSizing { get; set; }

        [Parameter("Use Market Hours", DefaultValue = true, Group="Behavior")]
        public bool UseMarketHours { get; set; }

        [Parameter("STOCH_KPERIODS", DefaultValue = 6, Group="Advanced")]
        public int STOCH_KPERIODS { get; set; }

        [Parameter("STOCH_KSLOWING", DefaultValue = 3, Group="Advanced")]
        public int STOCH_KSLOWING { get; set; }

        [Parameter("STOCH_DPERIODS", DefaultValue = 3, Group="Advanced")]
        public int STOCH_DPERIODS { get; set; }

        [Parameter("ADXLevel", DefaultValue = 28, Group="Advanced")]
        public int ADXLevel { get; set; }

        [Parameter("ADXPeriod", DefaultValue = 4, Group="Advanced")]
        public int ADXPeriod { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;
        private BAMMRenkoUgliness _BAMMRenkoUgliness;
        private DirectionalMovementSystem _DMS;

        private bool _IsLastBlockGreen;
        private DateTime _LongInCoolDownUntil;
        private DateTime _ShortInCoolDownUntil;
        // daily drawdown
        private double _DDD;

        private string _searchID;

        private const string MORTEN_ID = "M_AUTO_SIZE";
        private const string GENERAL_ID = "GENERAL_AUTO_SIZE";

        private const string BUTTON_RED = "#BD2709";
        private const string BUTTON_GREEN = "#0E9247";
        private const string BUTTON_BLUE = "#0D6EC4";

        private Random random = new Random();

        protected override void OnStart()
        {

            // Subscribe to events
            Positions.Opened += PositionsOnOpened;
            Positions.Closed += PositionsOnClosed;
            // Positions.Modified += PositionsOnModified;

            // Set currency
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);

            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            if (FollowComment == "")
            {
                Print("FollowComment must not be empty");
                OnStop();
            }

            _searchID = FollowComment.ToLower().StartsWith("m") ? MORTEN_ID : GENERAL_ID;

            _BAMMRenkoUgliness = Indicators.GetIndicator<BAMMRenkoUgliness>(1, 0, 0, 0, 0, 35, 35, 35, 28, false, STOCH_KPERIODS, STOCH_KSLOWING, STOCH_DPERIODS, Source);
            _DMS = Indicators.DirectionalMovementSystem(ADXPeriod);

            SetDDDFromHistory();

            CreateDisplay();

            Print("I'm watching you! {0}", FollowComment);

        }

        private void CreateDisplay()
        {

            var wrapPanel = new WrapPanel();

            wrapPanel.Orientation = Orientation.Horizontal;   
            wrapPanel.AddChild(GetPositionSizeRiskButton());
            wrapPanel.AddChild(IsMarketOpenButton());
            wrapPanel.AddChild(GetDDDButton());

            if(IsLongInCoolDown())
            {
                wrapPanel.AddChild(LongInCoolDownButton());
            }

            if(IsShortInCoolDown())
            {
                wrapPanel.AddChild(ShortInCoolDownButton());
            }

            Chart.AddControl(wrapPanel);
        }

        private void SetDDDFromHistory()
        {

            DateTime startDateTime = DateTime.Today; //Today at 00:00:00
            DateTime endDateTime = DateTime.Today.AddDays(1).AddTicks(-1); //Today at 23:59:59

            // Bind all trades from search ID
            _DDD = Math.Round(History.Where(trade => trade.EntryTime > startDateTime && trade.Label == _searchID).Sum(c => c.NetProfit),2);

        }

        private bool IsLongInCoolDown() {
            if ( _LongInCoolDownUntil == null || DateTime.Now > _LongInCoolDownUntil)
            {
                return false;
            }

            return true;
        }

        private bool IsShortInCoolDown() {
            if ( _ShortInCoolDownUntil == null || DateTime.Now > _ShortInCoolDownUntil )
            {
                return false;
            }

            return true;
        }

        private TextBlock GetPositionSizeRiskButton()
        {
            var textblock =  new TextBlock 
            {
                BackgroundColor = BUTTON_BLUE,
                ForegroundColor = Color.White,
                Text = GetPositionSizeRisk(),
                Padding = "8 4",
                Margin = 5
            };
            // textblock.Hover += e => Print("Play button clicked");
            return textblock;
        }

        private Button GetDDDButton()
        {
            var button = new Button 
            {
                Text = "DDD: " + _DDD,
                BackgroundColor = _DDD < 0 ? BUTTON_RED : BUTTON_GREEN,
                Margin = 5
            };
            return button;
        }

        private bool IsDDDLowPassing()
        {
            // Is Daily drawdown lower than max allowed?
            if( _DDD < -(GetMaxDDD()) )
            {
                return false;
            }

            return true;
        }

        private bool IsDDDMaxPassing()
        {
            // Is Daily drawdown larger than lock in profit?
            if( _DDD > GetLockInDailyProfit() )
            {
                return true;
            }

            return false;
        }

        private double GetLockInDailyProfit() {
            return Math.Round(((Account.Balance * LockInDailyProfitPercent) / 100), 0);
        }

        private double GetMaxDDD() {
            return Math.Round(((AccountStartSize * MaxDDDPercent) / 100), 0);
        }

        private string GetPositionSizeRisk()
        {      
            return PositionRiskPercent + "% x " + StopLossPips + "pip = " + GetPositionSizeInLots() + " lots";
        }

        private double GetPositionSize()
        {
            double positionSizeForRisk = ((Account.Balance * PositionRiskPercent) / 100) / ((StopLossPips+(2*CurrentSymbol.Spread)) * CurrentSymbol.PipValue);
            return CurrentSymbol.NormalizeVolumeInUnits(positionSizeForRisk, RoundingMode.Up);
        }

        private double GetPositionSizeInLots()
        {
            return Math.Round(CurrentSymbol.VolumeInUnitsToQuantity(GetPositionSize()), 2);
        }

        private bool IsMarketOpen()
        {
            if( (Server.Time.Hour >= 7 && Server.Time.Hour < 10) || (Server.Time.Hour >= 12 && Server.Time.Hour < 16) ) {       
                return true;
            }

            return UseMarketHours ? false : true;

        }

        private Button IsMarketOpenButton()
        {
            
            if(IsMarketOpen() == true)
            {
                return new Button 
                {
                    Text = "MARKET IS OPEN",
                    BackgroundColor = BUTTON_GREEN,
                    Margin = 5
                };
            }
            else
            {
                return new Button 
                {
                    Text = "MARKET IS CLOSED",
                    BackgroundColor = BUTTON_RED,
                    Margin = 5
                };
            }   
            
        }

        private Button LongInCoolDownButton()
        {
            
            return new Button 
            {
                Text = "Long CD " + _LongInCoolDownUntil.ToString("HH:mm:ss"),
                BackgroundColor = BUTTON_RED,
                Margin = 5
            };
              
            
        }

        private Button ShortInCoolDownButton()
        {
            
            return new Button 
            {
                Text = "Short CD " + _ShortInCoolDownUntil.ToString("HH:mm:ss"),
                BackgroundColor = BUTTON_RED,
                Margin = 5
            };
              
            
        }

        protected override void OnBar()
        {
            _IsLastBlockGreen = IsGreenCandle(Bars.OpenPrices.Last(1), Bars.ClosePrices.Last(1));
      
            // Run all checks
            RunChecks();

        }

        private void PositionsOnClosed(PositionClosedEventArgs args)
        {
            var position = args.Position;

            if( FollowComment != position.Comment )
            {
                return;
            }

            SetDDDFromHistory();

            if( position.Pips < -(StopLossPips*0.9) && CurrentSymbol.Name == position.SymbolName )
            {
                Print("COOLDOWN until {0} lost more than {1} pips", DateTime.Now.AddSeconds(CoolDownSeconds), -(StopLossPips*0.9));
                _LongInCoolDownUntil = DateTime.Now.AddSeconds(CoolDownSeconds);
                _ShortInCoolDownUntil = DateTime.Now.AddSeconds(CoolDownSeconds);
            }
        }

        private void PositionsOnOpened(PositionOpenedEventArgs args)
        {
            var position = args.Position;

            if( (position.SymbolName != CurrentSymbol.Name) || (FollowComment != position.Comment) )
            {
                return;
            }
            
            // Check if market is open
            if( !IsMarketOpen() )
            {
                Print("Out of time!");
                ClosePosition(position);
                return;
            }

            // Check DDD low
            if( IsDDDLowPassing() == false )
            {
                Print("Out of funds! {0} < {1}", _DDD, GetMaxDDD());
                ClosePosition(position);
                return;
            }

            // Check DDD max
            if( IsDDDMaxPassing() == true )
            {
                Print("Go outside and play!");
                ClosePosition(position);
                return;
            }

            if( position.TradeType == TradeType.Buy && IsLongInCoolDown() == true ) {
                Print("NO LONG FOR YOU!");
                ClosePosition(position);
                return;
            }

            if( position.TradeType == TradeType.Sell && IsShortInCoolDown() == true ) {
                Print("No shorting allowed!");
                ClosePosition(position);
                return;
            }

            // Auto size
            if( UseAutoSizing && position.Label == "" )
            {

                if( position.Quantity > 0.02 )
                {
                    Print("Position is too large! {0}", position.Quantity);
                    ClosePosition(position);   
                    return; 
                }
                
                // Get new quantity from risk management
                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits( UseAutoSizing ? GetPositionSizeInLots(): position.Quantity );
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);
                
                // Clone into correct size and SL
                ExecuteMarketOrderAsync(position.TradeType, position.SymbolName, volumeInUnits, _searchID, StopLossPips, 0, FollowComment, false);
                
                // Close original
                ClosePosition(position);                   

            }

            return;

        }        

        protected override void OnTick()
        {

            var positions = Positions.Where(x => x.Comment == FollowComment && x.SymbolName == CurrentSymbol.Name);

            foreach (Position position in positions)
            {

                if (BreakEvenPips > 0 && position.Pips >= BreakEvenPips)
                { 
                    // Set breakeven
                    BreakEven(position);
                }
                
                if (HalfBreakEvenPips > 0 && position.Pips >= HalfBreakEvenPips)
                { 
                    // Cut half breakeven pips
                    HalfBreakEven(position);
                }


                if ( TrailingStopPips > 0 && TrailingStopPips >= position.Pips ) {
                    position.ModifyTrailingStop(true);
                }

            }

        }

        private bool PositionHasBreakeven(Position position)
        {
            if ( (position.TradeType == TradeType.Buy && position.EntryPrice > position.StopLoss) || (position.TradeType == TradeType.Sell && position.EntryPrice < position.StopLoss))
            {
                return false;
            }
            return true;
        }

        // Take half 
        protected void LockInProfits(Position position)
        {
            
            var units = CurrentSymbol.QuantityToVolumeInUnits(position.Quantity/2);
            units = CurrentSymbol.NormalizeVolumeInUnits(units, RoundingMode.Up);
            
            Print("Taking some risk off the table! {0} >= {1}", GetNetProfitPercentage(position.NetProfit), LockInProfitPercent);

            // Take profits
            ModifyPositionAsync(position, units);

            if(UseTrailingStopsOnProfit == true)
            {
                position.ModifyTrailingStop(true);
            }            

            if(UseBreakevenOnProfit == true)
            {
                // Set breakeven
                BreakEven(position);
            }

        }

        private double GetNetProfitPercentage(double netProfit)
        {
            double balance = Account.Balance;

            // Dobbel the percentage when we are ranging
            // return Math.Round((netProfit / balance) * 100, 2) * (InRange() && UseRangeProfitHalving ? 2 : 1);
            return Math.Round((netProfit / balance) * 100, 2);
        }

        private bool InRange(int index = 1)
        {
            if (_DMS.ADX.Last(index) < ADXLevel)
            {
                return true;
            }

            return false;
        }

        private void AddToPosition(Position position)
        {
            if( ( (position.TradeType == TradeType.Buy && !_IsLastBlockGreen) || ( position.TradeType == TradeType.Sell && _IsLastBlockGreen) ) && GetCloseScore(position.TradeType) < AddPositionCloseScore )
            {
                Print("Adding 50% to position");
                var units = CurrentSymbol.QuantityToVolumeInUnits(position.Quantity*1.5);
                units = CurrentSymbol.NormalizeVolumeInUnits(units, RoundingMode.Up);
                
                ModifyPosition(position, units);

            }
        }

        private void RunChecks()
        {
            var positions = Positions.Where(x => x.Comment == FollowComment && x.SymbolName == CurrentSymbol.Name);

            foreach (Position position in positions)
            {
                // Only set on positions with breakeven
                if( UseAddition && PositionHasBreakeven(position) == true )
                {
                    AddToPosition(position);
                }

                if( TryToClosePosition(position) == true)
                {
                    ClosePosition(position);
                }

                // Take half risk of the table
                if (position.Pips > 0 && position.HasTrailingStop == false && GetNetProfitPercentage(position.NetProfit) >= LockInProfitPercent )
                {
                    // Check for profit taking and activate breakeven + TSL
                    LockInProfits(position);
                }

            }

        }

        private bool IsGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private void BreakEven(Position position)
        {
            if( PositionHasBreakeven(position) == false ) 
            {
                // Move to positive
                position.ModifyStopLossPips(-(2*CurrentSymbol.Spread));

                if(UseTrailingStopsOnBreakeven)
                {
                    position.ModifyTrailingStop(UseTrailingStopsOnBreakeven);
                }
            }
            
        }

        private void HalfBreakEven(Position position)
        {
            if( PositionHasHalfBreakeven(position) == false ) 
            {
                // Cut SL in half
                position.ModifyStopLossPips((StopLossPips/2));

            }
            
        }

        private bool PositionHasHalfBreakeven(Position position)
        {
            double _SLDiff = (double) (position.EntryPrice-position.StopLoss);
            double diffpipvalue = Math.Abs(Math.Round(_SLDiff, 2)/CurrentSymbol.PipSize);

            if ( diffpipvalue == StopLossPips )
            {
                return false;
            }
            return true;
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

        private bool TryToClosePosition(Position position)
        {
            // Dont close if negative. Let SL do that
            if (position.NetProfit < 0 && AllowCloseWhenUglyNegative == false)
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
