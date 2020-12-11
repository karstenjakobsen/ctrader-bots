using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class BAMM_MTF_SIGNAL : Robot
    {
        [Parameter(DefaultValue = "K1", Group="ID")]
        public string cBotLabel { get; set; }

        [Parameter("MTF Label", DefaultValue = "BAMM_MTF", Group="ID")]
        public string MTFLabel { get; set; }

        [Parameter("Currency pair", DefaultValue = "", Group="ID")]
        public string TradeSymbol { get; set; }

        [Parameter("Lot Size", DefaultValue = 0.01, MinValue = 0.01, Step = 0.01, Group="Entry")]
        public double LotSize { get; set; }

        [Parameter("Step Pips", DefaultValue = 10, MinValue = 0.5, Step = 0.5, Group="Entry")]
        public double StepPips { get; set; }

        [Parameter("Use Market Hours", DefaultValue = true, Group="Behavior")]
        public bool UseMarketHours { get; set; }
        
        [Parameter()]
        public DataSeries Source { get; set; }

        Symbol CurrentSymbol;

        private double _LongProtocolSizeBits;
        private double _LongProtocolZeroValue;
        private double _LongProtocolIncrement;

        private double _ShortProtocolSizeBits;
        private double _ShortProtocolZeroValue;
        private double _ShortProtocolIncrement;

        protected override void OnStart()
        {
            //check symbol
            CurrentSymbol = Symbols.GetSymbol(TradeSymbol);

            if (CurrentSymbol == null)
            {
                Print("Currency pair is not supported, please check!");
                OnStop();
            }

            PendingOrders.Modified += PendingOrdersOnModified;

        }

        private void PendingOrdersOnModified(PendingOrderModifiedEventArgs args)
        {
            var pendingorder = args.PendingOrder;

            if(pendingorder.Label != MTFLabel || pendingorder.SymbolName != CurrentSymbol.Name)
            {
                return;
            }

            if(pendingorder.TradeType == TradeType.Buy)
            {
                SetLongProtocolHeaders(pendingorder.Comment);       
            }
            else
            {
                SetShortProtocolHeaders(pendingorder.Comment);
            }

            CheckOrder(pendingorder);

        }

        private bool AreAllBitsSet(string s, string value)
        {
            return s.Length == 0 || s.All(ch => ch == value[0] );
        }

        private void CheckOrder(PendingOrder pendingOrder)
        {
            string binary =  GetPositionSizeAsBinary(pendingOrder);

            Print("{0} {1}", (pendingOrder.TradeType == TradeType.Buy) ? "Long" : "Short", binary);

            if( AreAllBitsSet(binary, "1") )
            {
                Print("GO {0}", (pendingOrder.TradeType == TradeType.Buy) ? "LONG" : "SHORT");

                var volumeInUnits = CurrentSymbol.QuantityToVolumeInUnits(LotSize);
                volumeInUnits = CurrentSymbol.NormalizeVolumeInUnits(volumeInUnits, RoundingMode.Down);

                ExecuteMarketOrderAsync(pendingOrder.TradeType, pendingOrder.SymbolName, volumeInUnits, null, null, null, cBotLabel, false);

                double catchPrice = (pendingOrder.TradeType == TradeType.Buy) ? CurrentSymbol.Bid - ( StepPips * CurrentSymbol.PipSize) : CurrentSymbol.Bid + ( StepPips * CurrentSymbol.PipSize);
                PlaceLimitOrderAsync(pendingOrder.TradeType, pendingOrder.SymbolName, volumeInUnits, catchPrice, null, null, null, null, cBotLabel, false);

            }
            else if( AreAllBitsSet(binary, "0") )
            {
                Print("Cancel {0}s binary {1}", (pendingOrder.TradeType == TradeType.Buy) ? "LONG" : "SHORT", binary);
                CancelPendingOrders(pendingOrder.TradeType);
            }

        }

        private void CancelPendingOrders(TradeType tradeType)
        {
            var orders = PendingOrders.Where(x => x.Label == cBotLabel && x.SymbolName == CurrentSymbol.Name && x.TradeType == tradeType);

            foreach (var order in orders)
            {
                var result = CancelPendingOrderAsync(order);
            }
        }

        private string ToBinary(int size, int val)
        {

            char[] buff = new char[size];
    
            for (int i = (size-1); i >= 0 ; i--) {
                int mask = 1 << i;
                buff[(size-1) - i] = (val & mask) != 0 ? '1' : '0';
            }
    
            return new string(buff);
        }
        
        private string GetPositionSizeAsBinary(PendingOrder pendingOrder)
        {
            if(pendingOrder.TradeType == TradeType.Buy)
            {
                double realSize = (pendingOrder.Quantity - _LongProtocolZeroValue);
                int bits = (int) Math.Round(realSize/_LongProtocolIncrement, 0);
                Print("bits long {0}", bits);

                return ToBinary((int) _LongProtocolSizeBits, bits);
            }
            else
            {
                double realSize = (pendingOrder.Quantity - _ShortProtocolZeroValue);
                int bits = (int) Math.Round(realSize/_ShortProtocolIncrement, 0);

                Print("bits short {0}", bits);
                return ToBinary((int) _ShortProtocolSizeBits, bits);
            }
            
        }

        private void SetLongProtocolHeaders(string comment)
        {
            string[] array = comment.Split('|');

            _LongProtocolSizeBits = Convert.ToDouble(array[0]);
            _LongProtocolZeroValue = Convert.ToDouble(array[1]);
            _LongProtocolIncrement = Convert.ToDouble(array[2]);
        }

        private void SetShortProtocolHeaders(string comment)
        {
            string[] array = comment.Split('|');

            _ShortProtocolSizeBits = Convert.ToDouble(array[0]);
            _ShortProtocolZeroValue = Convert.ToDouble(array[1]);
            _ShortProtocolIncrement = Convert.ToDouble(array[2]);
        }


        protected override void OnBar()
        {

        }

        protected override void OnTick()
        {
 
        }

        protected override void OnStop()
        {
            Stop();
        }
    }
}
