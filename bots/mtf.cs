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
 
        [Parameter(DefaultValue = "BAMM_MTF", Group="ID")]
        public string cBotLabel { get; set; }
 
        [Parameter("Currency pair", DefaultValue = "", Group="ID")]
        public string TradeSymbol { get; set; }

        [Parameter("Initiator", DefaultValue = false, Group="Initiator")]
        public bool Initiator { get; set; }

        [Parameter("Size Bits", DefaultValue = 4)]
        public double SizeBits { get; set; }

        [Parameter("Bit", DefaultValue = 1)]
        public int Bit { get; set; }

        [Parameter("Sell Price", DefaultValue = 100, Group="Initiator")]
        public double SellPrice { get; set; }

        [Parameter("Buy Price", DefaultValue = 0.1, Group="Initiator")]
        public double BuyPrice { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;
        
        // private BAMMRenkoUgliness _BAMMRenkoUgliness;

        private const double ZERO_VALUE = 0.1;
        private const double INCREMENT = 0.01;

        private PendingOrder _buyIndicator;
        private PendingOrder _sellIndicator;
        private StochasticOscillator _STO;

        private string ToBinary(int x)
        {
            int size = (int) SizeBits;

            char[] buff = new char[size];
    
            for (int i = (size-1); i >= 0 ; i--) {
                int mask = 1 << i;
                buff[(size-1) - i] = (x & mask) != 0 ? '1' : '0';
            }
    
            return new string(buff);
        }
	
        private bool CheckBitIsSet(int val, string binary)
        {
            int i = (int) SizeBits;
            
            foreach (char c in binary)
            {
                if( val == i && c == '1')
                {
                    Print("Bit already set {0} in {1}", val, binary);
                    return true;
                }
                i--;
                
            }
        
            return false;
        }

        private string GetPositionSizeAsBinary(PendingOrder pendingOrder)
        {
            double realSize = (pendingOrder.Quantity - ZERO_VALUE);
            int bits = (int) Math.Round(realSize/INCREMENT, 0);
            return ToBinary(bits);
        }
        
        protected override void OnStart()
        {
            // Set currency
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);

            PendingOrders.Created += PendingOrdersOnCreated;
            PendingOrders.Modified += PendingOrdersOnModified;

            // _BAMMRenkoUgliness = Indicators.GetIndicator<BAMMRenkoUgliness>(1, 0, 0, 100, 100, 0, 0, 0, 0, 30, false, 9, 3, 9, Source);
            _STO = Indicators.StochasticOscillator(9, 3, 9, MovingAverageType.Simple);

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
                _buyIndicator = pendingorder;
            }
            else
            {
                _sellIndicator = pendingorder;
            }

        }

        private void PendingOrdersOnModified(PendingOrderModifiedEventArgs args)
        {
            var pendingorder = args.PendingOrder;

            if(pendingorder.Label != cBotLabel || pendingorder.SymbolName != CurrentSymbol.Name)
            {
                return;
            }

            if(pendingorder.TradeType == TradeType.Buy)
            {
                _buyIndicator = pendingorder;
            }
            else
            {
                _sellIndicator = pendingorder;
            }

        }

        protected override void OnStop()
        {
            Stop();
        }
        
        protected override void OnBar()
        {
            if( _buyIndicator != null )
            {
                CheckLong();
            }

            if( _sellIndicator != null )
            {
                CheckShort();
            }
            
        }

        private void CheckShort()
        {
            if( CheckShortLooksNice() == true && CheckBitIsSet(Bit, GetPositionSizeAsBinary(_sellIndicator)) == false )
            {
                // Modify order here
                Print("Looks good - Modify Short order - turn ON bit {0} - binary {1}", Bit, GetPositionSizeAsBinary(_sellIndicator));

                // Smallest quantity
                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits( (_sellIndicator.Quantity + GetBitQuantity()) );
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

                _sellIndicator.ModifyVolume(volumeInUnits);
            }
            else if ( CheckShortLooksNice() == false && CheckBitIsSet(Bit, GetPositionSizeAsBinary(_sellIndicator)) == true )
            {
                // Modify order here
                Print("Looks bad - Modify Short order - turn OFF bit {0} - binary {1}", Bit, GetPositionSizeAsBinary(_sellIndicator));

                // Smallest quantity
                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits( (_sellIndicator.Quantity - GetBitQuantity()) );
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

                _sellIndicator.ModifyVolume(volumeInUnits);
            }
        }

        private void CheckLong()
        {
            if( CheckLongLooksNice() == true && CheckBitIsSet(Bit, GetPositionSizeAsBinary(_buyIndicator)) == false )
            {
                // Modify order here
                Print("Looks good - Modify Long order - turn ON bit {0} - binary {1}", Bit, GetPositionSizeAsBinary(_buyIndicator));

                // Smallest quantity
                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits( (_buyIndicator.Quantity + GetBitQuantity()) );
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

                _buyIndicator.ModifyVolume(volumeInUnits);
            }
            else if ( CheckLongLooksNice() == false && CheckBitIsSet(Bit, GetPositionSizeAsBinary(_buyIndicator)) == true )
            {
                // Modify order here
                Print("Looks bad - Modify Long order - turn OFF bit {0} - binary {1}", Bit, GetPositionSizeAsBinary(_buyIndicator));

                // Smallest quantity
                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits( (_buyIndicator.Quantity - GetBitQuantity()) );
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

                _buyIndicator.ModifyVolume(volumeInUnits);
            }
        }

        private double GetBitQuantity()
        {
            return (INCREMENT*Bit);
        }

        // private double GetCloseScore(TradeType tradeType, int index = 1)
        // {
        //     if (tradeType == TradeType.Buy)
        //     {
        //         return _BAMMRenkoUgliness.CloseLong.Last(index);
        //     }
        //     else
        //     {
        //         return _BAMMRenkoUgliness.CloseShort.Last(index);
        //     }
        // }

        private bool CheckLongLooksNice()
        {

            Print("{0} > {1}", _STO.PercentK.Last(1), _STO.PercentK.Last(2));

            // Pointing up from low-ish point
            if ( _STO.PercentK.Last(1) > _STO.PercentK.Last(2) && _STO.PercentK.Last(1) <= 30 )
            {
                return true;
            }

            return false;
        }

        private bool CheckShortLooksNice()
        {

            // Pointing down from high-ish point
            if ( _STO.PercentK.Last(1) < _STO.PercentK.Last(2) && _STO.PercentK.Last(1) >= 70 )
            {
                return true;
            }

            return false;
        }

        private void InitOrders()
        {
            Print("Initializing orders...");
            
            // Smallest quantity
            var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits( ZERO_VALUE );
            volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

            // Remove old orders
            // Print("{0}, {1}", cBotLabel, CurrentSymbol.Name);
            var pendingOrders = PendingOrders.Where(order =>  order.SymbolName == CurrentSymbol.Name && order.Label == cBotLabel);
            foreach (var order in pendingOrders)
            {
                CancelPendingOrder(order);
            }

            // Initialise status orders
            PlaceLimitOrder(TradeType.Buy, CurrentSymbol.Name, volumeInUnits, BuyPrice, cBotLabel, null, null, null, GetProtocolHeader());
            PlaceLimitOrder(TradeType.Sell, CurrentSymbol.Name, volumeInUnits, SellPrice, cBotLabel, null, null, null, GetProtocolHeader());

        }

        private string GetProtocolHeader()
        {
            return SizeBits + "|" + ZERO_VALUE + "|" + INCREMENT;
        }

    }

}