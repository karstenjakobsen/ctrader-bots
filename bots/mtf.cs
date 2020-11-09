using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;
 
namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_MTF : Robot
    {
 
        [Parameter(DefaultValue = "BAMM_MTF")]
        public string cBotLabel { get; set; }
 
        [Parameter("Currency pair", DefaultValue = "")]
        public string TradeSymbol { get; set; }

        [Parameter("Initiator", DefaultValue = false)]
        public bool Initiator { get; set; }

        [Parameter("Bit", DefaultValue = 1)]
        public int Bit { get; set; }

        [Parameter("Sell Price", DefaultValue = 100)]
        public double SellPrice { get; set; }

        [Parameter("Buy Price", DefaultValue = 0.1)]
        public double BuyPrice { get; set; }

        Symbol CurrentSymbol;

        private const double ZERO_VALUE = 0.1;
        private const double INCREMENT = 0.01;

        private PendingOrder _buyIndicator;
        private PendingOrder _sellIndicator;
        
        protected override void OnStart()
        {
            // Set currency
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);
            PendingOrders.Created += PendingOrdersOnCreated;            

            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }
            
            if(Initiator)
            {
                Print("Started as Initiator");
                InitOrders();
            }
        }

        private void PendingOrdersOnCreated(PendingOrderCreatedEventArgs args)
        {
            var pendingorder = args.PendingOrder;

            if(pendingorder.Label != cBotLabel || pendingorder.SymbolName != CurrentSymbol.Name)
            {
                return;
            }

            if(pendingorder.TradeType == TradeType.Buy)
            {
                Print("Pending order with id {0} was created as buy indicator", pendingorder.Id);
                _buyIndicator = pendingorder;
            }
            else
            {
                Print("Pending order with id {0} was create as sell indicator", pendingorder.Id);
                _sellIndicator = pendingorder;
            }
        }

        protected override void OnStop()
        {
            Stop();
        }
        
        protected override void OnBar()
        {
            bool looksgood = true;

            // Check 
            if( looksgood )
            {

            }
        }

        private void InitOrders()
        {
            Print("Initializing orders...");
            
            // Smallest quantity
            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits( (ZERO_VALUE - INCREMENT) );
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            // Remove old orders
            var pendingOrders = PendingOrders.Where(order => order.Label == cBotLabel && order.SymbolName == CurrentSymbol.Name);
            foreach (var order in PendingOrders)
            {
                CancelPendingOrder(order);
            }

            // Initialise status orders
            PlaceLimitOrder(TradeType.Buy, CurrentSymbol.Name, volumeInUnits, BuyPrice, cBotLabel);
            PlaceLimitOrder(TradeType.Sell, CurrentSymbol.Name, volumeInUnits, SellPrice, cBotLabel);

        }

    }

}