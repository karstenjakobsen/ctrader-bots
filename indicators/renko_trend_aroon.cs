using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(ScalePrecision = 3, IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class BAMMRenkoTrend : Indicator
    {

        [Parameter("Entry - Only above/below major trend", DefaultValue = true)]
        public bool UseMajorTrendMA { get; set; }

        [Parameter("Entry - All MAs must point in trend direction", DefaultValue = true)]
        public bool MATrendDirection { get; set; }

        [Parameter("Entry - Minor MAs must point in trend direction", DefaultValue = true)]
        public bool MinorMATrendDirection { get; set; }

        [Parameter("Entry - Use Aroon", DefaultValue = false)]
        public bool UseAroon { get; set; }

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

        [Parameter("Aroon Period", DefaultValue = 6)]
        public int AroonPeriod { get; set; }

        [Parameter("Show winners/loosers", DefaultValue = true)]
        public bool ShowWinLoose { get; set; }

        [Parameter("Show win/loose ratio", DefaultValue = true)]
        public bool ShowRatio { get; set; }

        [Parameter("Show WR", DefaultValue = true)]
        public bool ShowWR { get; set; }

        [Parameter("Show Steps", DefaultValue = true)]
        public bool ShowSteps { get; set; }

        [Output("Steps", LineColor = "Blue")]
        public IndicatorDataSeries Result { get; set; }

        [Output("Loosers", LineColor = "Red")]
        public IndicatorDataSeries Loosers { get; set; }

        [Output("Winners", LineColor = "Green")]
        public IndicatorDataSeries Winners { get; set; }

        [Output("Ratio", LineColor = "Yellow")]
        public IndicatorDataSeries Ratio { get; set; }  

        [Parameter()]
        public DataSeries Source { get; set; }

        private MovingAverage MA1;
        private MovingAverage MA2;
        private MovingAverage MA3;
        private DirectionalMovementSystem DMS;
        private Bar bar;
        private Aroon ARN;
        private int TrendSignal = 0;
        private double _loosers = 0;
        private double _winners = 0;

        protected override void Initialize()
        {
            TrendSignal = 0;

            MA1 = Indicators.MovingAverage(Source, MAPeriod1, MAType1);
            MA2 = Indicators.MovingAverage(Source, MAPeriod2, MAType2);
            MA3 = Indicators.MovingAverage(Source, MAPeriod3, MAType3);
            ARN = Indicators.Aroon(AroonPeriod);
            DMS = Indicators.DirectionalMovementSystem(ADXPeriod);

        }

        public override void Calculate(int index)
        {

            if (index == 0)
            {
                return;
            }

            bar = Bars[index];

            if (IsEntryPossible(index) == true)
            {
                UpdateDirection(index);
                
                Chart.DrawIcon(bar.OpenTime.ToString(), ChartIconType.Star, index, bar.High, Color.Blue);
                Result[index] = ShowSteps == true ? TrendSignal : 0;
      
            }
            else
            {
                if(Math.Abs(TrendSignal) == 1) {
                    _loosers = _loosers + 2;
                } else if(Math.Abs(TrendSignal) == 2) {
                    _loosers = _loosers + 1;
                } else if(Math.Abs(TrendSignal) >= 3) {
                    _winners = _winners + (Math.Abs(TrendSignal)-3);
                }

                TrendSignal = 0;
                Result[index] = 0;
                Loosers[index] = ShowWinLoose == true ? _loosers : 0;
                Winners[index] = ShowWinLoose == true ? _winners : 0;
                Ratio[index] = ShowRatio == true ? (_winners/_loosers) : 0;
            }

        }
        private bool isGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private void UpdateDirection(int index)
        {

            try
            {
                Bar thisCandle = Bars[index];
                Bar lastCandle = Bars[index + 1];

                bool thisCandleGreen = isGreenCandle(thisCandle.Open, thisCandle.Close);
                bool lastCandleGreen = isGreenCandle(lastCandle.Open, lastCandle.Close);

                if (thisCandleGreen == true)
                {
                    TrendSignal = TrendSignal + 1;
                }
                else if (thisCandleGreen == false)
                {
                    TrendSignal = TrendSignal - 1;
                }                    

            } catch (Exception e)
            {
                Print("Could update direction, due to {0}" + e.StackTrace);
            }

            return;
        }

        private bool IsEntryPossible(int index)
        {
            // Print("Hello im {0} and result is {1} and is {2}", index, ARN.Up[index], ARN.Down[index]);

            if(index == 0)
            {
                return false;
            }

            try
            {

                bool greenCandle = isGreenCandle(bar.Open, bar.Close);

                if (greenCandle == true && (bar.Close < MA1.Result[index] || bar.Close < MA2.Result[index]))
                {
                    return false;
                }

                if (greenCandle == false && (bar.Close > MA1.Result[index] || bar.Close > MA2.Result[index]))
                {
                    return false;
                }

                if( UseAroon == true ) {
                    if(greenCandle == true && ( ARN.Up[index] != 100 || ARN.Down[index] != 0)) {
                        return false;
                    } else if(greenCandle == false && ( ARN.Up[index] != 0 || ARN.Down[index] != 100)) {
                        return false;
                    }
                }
                
                if (UseADX == true && DMS.ADX[index] < ADXLevel)
                {
                    return false;
                }

                if (UseADX == true && UseADXDown == true && (DMS.ADX[index - 1] > DMS.ADX[index]))
                {
                    return false;
                }

                if (UseMajorTrendMA == true)
                {
                    if (greenCandle == true && (bar.Close < MA3.Result[index]))
                    {
                        return false;
                    }
                    else if (greenCandle == false && (bar.Close > MA3.Result[index]))
                    {
                        return false;
                    }

                }

                if (MATrendDirection == true)
                {
                    if( greenCandle == true && ((MA1.Result[index] < MA1.Result[(index-1)]) || (MA2.Result[index] < MA2.Result[(index-1)]) || (MA3.Result[index] < MA3.Result[(index-1)]) ))
                    {
                        return false;
                    }
                    else if( greenCandle == false && ((MA1.Result[index] > MA1.Result[(index-1)]) || (MA2.Result[index] > MA2.Result[(index-1)]) || (MA3.Result[index] > MA3.Result[(index-1)]) ))
                    {        
                        return false;
                    }
                }

                if (MinorMATrendDirection == true)
                {
                    if( greenCandle == true && ((MA1.Result[index] < MA1.Result[(index-1)]) || (MA2.Result[index] < MA2.Result[(index-1)]) ))
                    {
                        return false;
                    }
                    else if( greenCandle == false && ((MA1.Result[index] > MA1.Result[(index-1)]) || (MA2.Result[index] > MA2.Result[(index-1)]) ))
                    {        
                        return false;
                    }
                }

                return true;

            } catch (Exception e)
            {
                Print("Could not check entry, due to {0}" + e.StackTrace);
                return false;
            }
        }

    }
}
