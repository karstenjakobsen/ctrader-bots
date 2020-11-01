using System;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
 
//calculate position size for current balance and specified risk (stop loss) per position; 
//does not take not account commission or spread though, simply adjust risk % to accommodate those
 
namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class PositionRiskkrp : Indicator
    {
 
        //percentage of current balance that can be risked (stop loss size in pips) on one position
        [Parameter("Stop Loss Risk %", DefaultValue = 5)]
        public double stopLossRiskPercent { get; set; }
 
        [Parameter("Stop Loss in Pips", DefaultValue = 10)]
        public double stopLossInPips { get; set; }
 
        public override void Calculate(int index)
        {
            if (index == 0)
                DisplayPositionSizeRiskOnChart();
        }
 
        private void DisplayPositionSizeRiskOnChart()
        {
 
            
            // var units = Symbol.QuantityToVolumeInUnits((stopLossInPips*Symbol.PipValue));
            var units = Symbol.NormalizeVolumeInUnits((stopLossInPips*Symbol.PipValue), RoundingMode.Up);
            double positionSizeForRisk = (Account.Balance * stopLossRiskPercent / 100) / units;
 
            string text = stopLossRiskPercent + "% x " + stopLossInPips + "pip = " + Math.Round(positionSizeForRisk,2) + " lot";
 
            Chart.DrawStaticText("positionRisk", text, VerticalAlignment.Top, HorizontalAlignment.Right, Color.Goldenrod);
            Chart.DrawStaticText("mid", "MARKET IS OPEN", VerticalAlignment.Top, HorizontalAlignment.Center, Color.Green);
 
        }
    }
}