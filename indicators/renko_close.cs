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

        [Parameter(DefaultValue = "BAMM_RENKO_CLOSE")]
        public string cBotLabel { get; set; }

        [Parameter("Rounds", DefaultValue = 1)]
        public int Rounds { get; set; }

        [Parameter()]
        public DataSeries Source { get; set; }

        private StochasticOscillator _STO;
        private MovingAverage _MA1;
        private MovingAverage _MA2;

        private bool _IsBlockGreen;

        private const int PENALTY_CANDLE_WEIGHT = 0;

        private const int PENALTY_STOCHD_WEIGHT = 50;

        private const int PENALTY_STOCHK_WEIGHT = 50;

        private const int PENALTY_MA_CROSS_WEIGHT = 25;

        protected override void Initialize()
        { 
            _STO = Indicators.StochasticOscillator(9, 3, 9, MovingAverageType.Simple);
            _MA1 = Indicators.MovingAverage(Source, 16, MovingAverageType.Simple);
            _MA2 = Indicators.MovingAverage(Source, 8, MovingAverageType.Exponential);
        }

        public override void Calculate(int index)
        {
            _IsBlockGreen = isGreenCandle(Bars.OpenPrices.Last(index), Bars.ClosePrices.Last(index));
            CloseLong[index] = (CalculateScore(TradeType.Buy, index)/Rounds);
            CloseShort[index] = (CalculateScore(TradeType.Sell, index)/Rounds);
        }

        private bool isGreenCandle(double lastBarOpen, double lastBarClose)
        {
            return (lastBarOpen < lastBarClose) ? true : false;
        }

        private int CalculateScore(TradeType tradeType, int index)
        {

            int stochDPenalty = GetStochDPenalty(tradeType, index);
            int stochKPenalty = GetStochDPenalty(tradeType, index);
            int candlePenalty = GetCandlePenalty(tradeType, index);
            int MACrossPenalty = GetMACrossPenalty(tradeType, index);

            return (stochDPenalty + stochKPenalty + candlePenalty + MACrossPenalty);

        }

        private int GetMACrossPenalty(TradeType tradeType, int index) {
       
            int _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA2.Result.Last(index) < _MA1.Result.Last(index)))
            {
                Print("MA2 has crossed below MA1 - I dont like it");
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA2.Result.Last(index) > _MA1.Result.Last(index)))
            {
                Print("MA2 has crossed above MA1 - I dont like it");
                _penalty = _penalty + 1;
            }


            return (_penalty * PENALTY_MA_CROSS_WEIGHT);

        }

        private int GetStochDPenalty(TradeType tradeType, int index) {
       
            int _penalty = 0;

            if (tradeType == TradeType.Buy && (_STO.PercentD.Last(index) < _STO.PercentD.Last(index-1)))
            {
                Print("Stoch D going down - I dont like it");
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_STO.PercentD.Last(index) > _STO.PercentD.Last(index-1)))
            {
                Print("Stoch D going up - I dont like it");
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_STOCHD_WEIGHT);

        }

        private int GetStochKPenalty(TradeType tradeType, int index) {
       
            int _penalty = 0;

            if (tradeType == TradeType.Buy && (_STO.PercentK.Last(index) < _STO.PercentK.Last(index-1)))
            {
                Print("Stoch K going down - smells funny");
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_STO.PercentK.Last(index) < _STO.PercentK.Last(index-1)))
            {
                Print("Stoch K going up - I dont like it");
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_STOCHK_WEIGHT);

        }

        private int GetCandlePenalty(TradeType tradeType, int index)
        {
            int _penalty = 0;

            if (isGreenCandle(Bars.OpenPrices.Last(index), Bars.ClosePrices.Last(index)))
            {
                if (tradeType == TradeType.Sell)
                {
                    Print("Green after red = bad");
                    _penalty = _penalty + 1;
                }
            }
            else if (!isGreenCandle(Bars.OpenPrices.Last(index), Bars.ClosePrices.Last(index)))
            {
                if (tradeType == TradeType.Buy)
                {
                    Print("Red after green = bad");
                    _penalty = _penalty + 1;
                }
            }

            return _penalty * PENALTY_CANDLE_WEIGHT;
        }

    }
}
