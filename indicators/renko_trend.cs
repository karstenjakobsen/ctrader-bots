
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(ScalePrecision = 1, IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class RenkoTrend : Indicator
    {

        [Parameter("Entry - Only above/below major trend", DefaultValue = true)]
        public bool UseTrendMA { get; set; }

        [Parameter("Entry - MAs must point in trend direction", DefaultValue = true)]
        public bool MATrendDirection { get; set; }

        [Parameter("Entry - Use ADX", DefaultValue = true)]
        public bool UseADX { get; set; }

        [Parameter("Entry - Use ADX Down", DefaultValue = true)]
        public bool UseADXDown { get; set; }
 
        [Parameter("MA01 Type", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType MAType1 { get; set; }
 
        [Parameter("MA01 Period", DefaultValue = 16)]
        public int MAPeriod1 { get; set; }
 
        [Parameter("MA02 Type", DefaultValue = MovingAverageType.Exponential)]
        public MovingAverageType MAType2 { get; set; }
 
        [Parameter("MA02 Period", DefaultValue = 8)]
        public int MAPeriod2 { get; set; }

        [Parameter("MA03 Type (Trend)", DefaultValue = MovingAverageType.Simple)]
        public MovingAverageType MAType3 { get; set; }
 
        [Parameter("MA03 Period", DefaultValue = 64)]
        public int MAPeriod3 { get; set; }
 
        [Parameter("ADX Period", DefaultValue = 6)]
        public int ADXPeriod { get; set; }
 
        [Parameter("ADX Level", DefaultValue = 32)]
        public int ADXLevel { get; set; }
        
        [Output("Main", LineColor = "Blue")]
        public IndicatorDataSeries Result { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        private MovingAverage MA1;
        private MovingAverage MA2;
        private MovingAverage MA3;
        private DirectionalMovementSystem DMS;
        private Bar bar;
        private int TrendSignal = 0;

        protected override void Initialize()
        {
            TrendSignal = 0;
            MA1 = Indicators.MovingAverage(Source, MAPeriod1, MAType1);
            MA2 = Indicators.MovingAverage(Source, MAPeriod2, MAType2);
            MA3 = Indicators.MovingAverage(Source, MAPeriod3, MAType3);
            DMS = Indicators.DirectionalMovementSystem(ADXPeriod);
            
        }

        public override void Calculate(int index)
        {      
            bar = Bars[index];
            if (IsEntryPossible(index) == true)
            {
                UpdateDirection(index);              
                Chart.DrawIcon(bar.OpenTime.ToString(), ChartIconType.Star, index, bar.High, Color.Blue);
                Result[index] = TrendSignal;
            }
            else 
            {
                TrendSignal = 0;
                Result[index] = 0;
            }
            
        }
        private bool isGreenCandle(double lastBarOpen, double lastBarClose) {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private void UpdateDirection(int index) {

            Bar thisCandle = Bars[index];
            Bar lastCandle = Bars[index+1];

            bool thisCandleGreen = isGreenCandle(thisCandle.Open, thisCandle.Close);
            bool lastCandleGreen = isGreenCandle(lastCandle.Open, lastCandle.Close);

            if (thisCandleGreen == true) {
                TrendSignal=TrendSignal+1;
            } else if(thisCandleGreen == false){
                TrendSignal=TrendSignal-1;
            }

            return;
        }

        private bool IsEntryPossible(int index)
        {       
            bool greenCandle = isGreenCandle(bar.Open, bar.Close);

            if (greenCandle == true && (bar.Close < MA1.Result[index] || bar.Close < MA2.Result[index])) {
                return false;
            }

            if (greenCandle == false && (bar.Close > MA1.Result[index] || bar.Close > MA2.Result[index])) {
                return false;
            }

            if (UseADX == true && DMS.ADX[index] < ADXLevel)
            {
                return false;
            }

            if (UseADX == true && UseADXDown == true && (DMS.ADX[index+1] > DMS.ADX[index]))
            {
                return false;
            }

            if (UseTrendMA == true)
            {
                if(greenCandle == true && (bar.Close < MA3.Result[index])) 
                {
                    return false;
                }
                else if(greenCandle == false && (bar.Close > MA3.Result[index]))
                {
                    return false;
                }
               
            }

            if (MATrendDirection == true)
            {
                if(greenCandle == true && (MA1.Result[index] < MA1.Result[index+1] && MA2.Result[index] < MA2.Result[index+1]))
                {
                    return false;
                } else if(greenCandle == false && (MA1.Result[index] < MA1.Result[index+1] && MA2.Result[index] < MA2.Result[index+1]))
                {
                    return false;
                }
            }

            return true;
        }

    }
}
