using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(ScalePrecision = 3, IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class BAMMRenkoClose : Indicator
    {
        [Output("CloseLong", LineColor = "Green")]
        public IndicatorDataSeries CloseLong { get; set; }

        [Output("CloseShort", LineColor = "Red")]
        public IndicatorDataSeries CloseShort { get; set; }

        [Output("CloseShortVelocity", LineColor = "Blue")]
        public IndicatorDataSeries CloseShortVelocity { get; set; }

        [Output("CloseLongVelocity", LineColor = "Yellow")]
        public IndicatorDataSeries CloseLongVelocity { get; set; }

        [Parameter("Rounds", DefaultValue = 1)]
        public int Rounds { get; set; }

        [Parameter("PENALTY_CANDLE_WEIGHT", DefaultValue = 5)]
        public int PENALTY_CANDLE_WEIGHT { get; set; }

        [Parameter("PENALTY_STOCHD_WEIGHT", DefaultValue = 20)]
        public int PENALTY_STOCHD_WEIGHT { get; set; }

        [Parameter("PENALTY_STOCHK_WEIGHT", DefaultValue = 20)]
        public int PENALTY_STOCHK_WEIGHT { get; set; }

        [Parameter("PENALTY_MA_CROSS_WEIGHT", DefaultValue = 20)]
        public int PENALTY_MA_CROSS_WEIGHT { get; set; }

        [Parameter("PENALTY_MA1_POINT_WEIGHT", DefaultValue = 20)]
        public int PENALTY_MA1_POINT_WEIGHT { get; set; }

        [Parameter("PENALTY_MA2_POINT_WEIGHT", DefaultValue = 20)]
        public int PENALTY_MA2_POINT_WEIGHT { get; set; }

        // [Parameter("PENALTY_RSI1_WEIGHT", DefaultValue = 25)]
        // public int PENALTY_RSI1_WEIGHT { get; set; }

        // [Parameter("PENALTY_RSI1_HIGH_LEVEL", DefaultValue = 65)]
        // public int PENALTY_RSI1_HIGH_LEVEL { get; set; }

        // [Parameter("PENALTY_RSI1_LOW_LEVEL", DefaultValue = 35)]
        // public int PENALTY_RSI1_LOW_LEVEL { get; set; }

        // [Parameter("PENALTY_RSI2_WEIGHT", DefaultValue = 75)]
        // public int PENALTY_RSI2_WEIGHT { get; set; }

        // [Parameter("PENALTY_RSI2_HIGH_LEVEL", DefaultValue = 75)]
        // public int PENALTY_RSI2_HIGH_LEVEL { get; set; }

        // [Parameter("PENALTY_RSI2_LOW_LEVEL", DefaultValue = 25)]
        // public int PENALTY_RSI2_LOW_LEVEL { get; set; }

        // [Parameter("PENALTY_ENTERING_RANGE_WEIGHT", DefaultValue = 100)]
        // public int PENALTY_ENTERING_RANGE_WEIGHT { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        private StochasticOscillator _STO;
        private RainbowOscillator _RAIN;
        private MovingAverage _MA1;
        private MovingAverage _MA2;
        private RelativeStrengthIndex _RSI;

        private const int MAX_PENALTY_VALUE = 100;

        private const int MIN_PENALTY_VALUE = 0;

        private bool _IsBlockGreen;

        protected override void Initialize()
        { 
            _STO = Indicators.StochasticOscillator(9, 3, 9, MovingAverageType.Simple);
            _MA1 = Indicators.MovingAverage(Source, 16, MovingAverageType.Simple);
            _MA2 = Indicators.MovingAverage(Source, 8, MovingAverageType.Exponential);
            // _RSI = Indicators.RelativeStrengthIndex(Source, 14);
            _RAIN = Indicators.RainbowOscillator(Source, 9, MovingAverageType.Simple);
        }

        public override void Calculate(int index)
        {
            try {

                if(index == 0)
                    return;

                _IsBlockGreen = isGreenCandle(Bars[index].Open, Bars[index].Close);

                double _roundLongScore = 0;
                double _roundShortScore = 0;
                double _roundShortVelocityScore = 0;
                double _roundLongVelocityScore = 0;
                double _diffLong = 0;

                for(int i = 0; i < Rounds; i++) {
                    _roundLongScore += CalculateScore(TradeType.Buy, (index-i));
                    _roundShortScore += CalculateScore(TradeType.Sell, (index-i));
                    _roundShortVelocityScore += Math.Abs( CalculateScore(TradeType.Sell, (index-i)) - CalculateScore(TradeType.Sell, (index-(1+i))));
                    _roundLongVelocityScore += Math.Abs( CalculateScore(TradeType.Buy, (index-i)) - CalculateScore(TradeType.Buy, (index-(1+i))));
                    _diffLong = _roundLongScore - _roundShortScore;
                }       
                
                CloseLong[index] = (_roundLongScore/Rounds);
                CloseShort[index] = (_roundShortScore/Rounds);
                CloseShortVelocity[index] = (_roundShortVelocityScore/Rounds);
                CloseLongVelocity[index] = (_roundLongVelocityScore/Rounds);

                ShowMaxShortOpportunity(index);

            } catch(Exception) {
                return;
            }
        }

        private void ShowMaxShortOpportunity(int index) {

            double thisShortScore = CalculateScore(TradeType.Sell, (index));
            double lastShortScore = CalculateScore(TradeType.Sell, (index-1));
            double lastShortScore2 = CalculateScore(TradeType.Sell, (index-2));
            double thisLongScore = CalculateScore(TradeType.Buy, (index));
            double lastLongScore = CalculateScore(TradeType.Buy, (index-1));
            double thislongvel = Math.Abs( CalculateScore(TradeType.Buy, (index)) - CalculateScore(TradeType.Buy, (index-1)));
            double thisshortvel = Math.Abs( CalculateScore(TradeType.Sell, (index)) - CalculateScore(TradeType.Sell, (index-1)));
            double thisdiffLong = thisLongScore - thisShortScore;
            double lastdiffLong = lastShortScore - lastLongScore;

            bool lastblockgreen = isGreenCandle(Bars[index-1].Open, Bars[index-1].Close);
            bool lastblockgreen2 = isGreenCandle(Bars[index-2].Open, Bars[index-2].Close);
            bool lastblockgreen3 = isGreenCandle(Bars[index-3].Open, Bars[index-3].Close);

            // if(-10 <= thisdiffLong <= 10 ) {
            //     Chart.DrawIcon(Bars[index].OpenTime.ToString(), ChartIconType.Star, index, Bars[index].High, Color.Blue);
            // }

            if(thisLongScore < thisShortScore && lastLongScore > lastShortScore ) {
                Chart.DrawIcon(Bars[index].OpenTime.ToString(), ChartIconType.Star, index, Bars[index].High, Color.Blue);
            }
            // if( (thisShortScore < lastShortScore && thisLongScore > lastLongScore && thisLongScore >= 50 && thisShortScore >= 50) || (thisShortScore <= 75 && lastLongScore == 100 && thisLongScore == 100)) {
            //     Chart.DrawIcon(Bars[index].OpenTime.ToString(), ChartIconType.Star, index, Bars[index].High, Color.Blue);
            // }

            // if( thisLongScore > lastLongScore && _roundLongVelocityScore >= 50 && lastShortScore == 100 && thisLongScore > thisShortScore) {
            //     Chart.DrawIcon(Bars[index].OpenTime.ToString(), ChartIconType.Star, index, Bars[index].High, Color.Blue);
            // }

            // if( thisShortScore > lastShortScore && lastLongScore == 100 && ( thisLongScore <= lastLongScore ) && lastShortScore < 35 && thisshortvel > 50 ) {
            //     Chart.DrawIcon(Bars[index].OpenTime.ToString(), ChartIconType.Star, index, Bars[index].High, Color.Blue);
            // }

            // if( thisShortScore > lastShortScore && _IsBlockGreen && lastblockgreen && thisLongScore < lastLongScore ) {
            //     Chart.DrawIcon(Bars[index].OpenTime.ToString(), ChartIconType.Star, index, Bars[index].High, Color.Blue);
            // }

            // if(!_IsBlockGreen && thisShortScore > lastShortScore) {
            //     Chart.DrawIcon(Bars[index].OpenTime.ToString(), ChartIconType.Star, index, Bars[index].High, Color.Yellow);
            // }

        }

        private bool isGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private double CalculateScore(TradeType tradeType, int index)
        {

            double stochDPenalty = GetStochDPenalty(tradeType, index);
            double stochKPenalty = GetStochKPenalty(tradeType, index);
            double candlePenalty = GetCandlePenalty(tradeType, index);
            double MACrossPenalty = GetMACrossPenalty(tradeType, index);
            double MA1PointPenalty = GetMA1PointPenalty(tradeType, index);
            double MA2PointPenalty = GetMA2PointPenalty(tradeType, index);     
            // double RSI1Penalty = GetRSI1Penalty(tradeType, index);
            // double RSI2Penalty = GetRSI2Penalty(tradeType, index);
            // int EnteringRangePenalty = GetEnteringRangePenalty(index);

            double _penalty = (stochDPenalty + stochKPenalty + candlePenalty + MACrossPenalty + MA1PointPenalty + MA2PointPenalty);

            if(_penalty > MAX_PENALTY_VALUE) {
                return MAX_PENALTY_VALUE;
            } else if(_penalty < MIN_PENALTY_VALUE) {
                return MIN_PENALTY_VALUE;
            }

            return _penalty;

        }

        // private double GetEnteringRangePenalty(int index) {
       
        //     double _penalty = 0;

        //     // falling for two rounds
        //     if (_DMS.ADX[index] < _DMS.ADX[index-1] && _DMS.ADX[index-1] < _DMS.ADX[index-2])
        //     {
        //         _penalty = _penalty + 1;
        //     }

        //     return (_penalty * PENALTY_ENTERING_RANGE_WEIGHT);

        // }

        

        // private double GetRSI1Penalty(TradeType tradeType, int index) {
       
        //     double _penalty = 0;

        //     if (tradeType == TradeType.Buy && (_RSI.Result[index] >= PENALTY_RSI1_HIGH_LEVEL))
        //     {
        //         _penalty = _penalty + 1;
        //     }

        //     if (tradeType == TradeType.Sell && (_RSI.Result[index] <= PENALTY_RSI1_LOW_LEVEL))
        //     {
        //         _penalty = _penalty + 1;
        //     }

        //     return (_penalty * PENALTY_RSI1_WEIGHT);

        // }

        // private double GetRSI2Penalty(TradeType tradeType, int index) {
       
        //     double _penalty = 0;

        //     if (tradeType == TradeType.Buy && (_RSI.Result[index] >= PENALTY_RSI2_HIGH_LEVEL))
        //     {
        //         _penalty = _penalty + 1;
        //     }

        //     if (tradeType == TradeType.Sell && (_RSI.Result[index] <= PENALTY_RSI2_LOW_LEVEL))
        //     {
        //         _penalty = _penalty + 1;
        //     }

        //     return (_penalty * PENALTY_RSI2_WEIGHT);

        // }

        private double GetMACrossPenalty(TradeType tradeType, int index) {
       
            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA2.Result[index] < _MA1.Result[index]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA2.Result[index] > _MA1.Result[index]))
            {
                _penalty = _penalty + 1;
            }


            return (_penalty * PENALTY_MA_CROSS_WEIGHT);

        }

        private double GetMA1PointPenalty(TradeType tradeType, int index) {
       
            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA1.Result[index] < _MA1.Result[index-1]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA1.Result[index] > _MA1.Result[index-1]))
            {
                _penalty = _penalty + 1;
            }


            return (_penalty * PENALTY_MA1_POINT_WEIGHT);

        }

        private double GetMA2PointPenalty(TradeType tradeType, int index) {
       
            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA2.Result[index] < _MA2.Result[index-1]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA2.Result[index] > _MA2.Result[index-1]))
            {
                _penalty = _penalty + 1;
            }


            return (_penalty * PENALTY_MA2_POINT_WEIGHT);

        }

        

        private double GetStochDPenalty(TradeType tradeType, int index) {
       
            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_STO.PercentD[index] < _STO.PercentD[index-1]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_STO.PercentD[index] > _STO.PercentD[index-1]))
            {
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_STOCHD_WEIGHT);

        }

        private double GetStochKPenalty(TradeType tradeType, int index) {
       
            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_STO.PercentK[index] < _STO.PercentK[index-1]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_STO.PercentK[index] < _STO.PercentK[index-1]))
            {
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_STOCHK_WEIGHT);

        }

        private double GetCandlePenalty(TradeType tradeType, int index)
        {
            double _penalty = 0;

            if (isGreenCandle(Bars[index].Open, Bars[index].Close))
            {
                if (tradeType == TradeType.Sell)
                {
                    _penalty = _penalty + 1;
                }
            }
            else if (!isGreenCandle(Bars[index].Open, Bars[index].Close))
            {
                if (tradeType == TradeType.Buy)
                {
                    _penalty = _penalty + 1;
                }
            }

            return _penalty * PENALTY_CANDLE_WEIGHT;
        }

    }
}
