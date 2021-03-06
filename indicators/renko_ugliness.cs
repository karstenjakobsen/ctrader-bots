using System;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;
using cAlgo.API.Indicators;
using cAlgo.Indicators;

namespace cAlgo
{
    [Indicator(ScalePrecision = 2, IsOverlay = false, TimeZone = TimeZones.UTC, AccessRights = AccessRights.FullAccess)]
    public class BAMMRenkoUgliness : Indicator
    {
        [Output("CloseLong", LineColor = "Green")]
        public IndicatorDataSeries CloseLong { get; set; }

        [Output("CloseShort", LineColor = "Red")]
        public IndicatorDataSeries CloseShort { get; set; }

        [Parameter("Rounds", DefaultValue = 1)]
        public int Rounds { get; set; }

        [Parameter("PENALTY_CANDLE_WEIGHT", DefaultValue = 0)]
        public int PENALTY_CANDLE_WEIGHT { get; set; }

        [Parameter("PENALTY_STOCHD_WEIGHT", DefaultValue = 25)]
        public int PENALTY_STOCHD_WEIGHT { get; set; }

        [Parameter("PENALTY_STOCHK_WEIGHT", DefaultValue = 25)]
        public int PENALTY_STOCHK_WEIGHT { get; set; }

        [Parameter("PENALTY_MA12_CROSS_WEIGHT", DefaultValue = 20)]
        public int PENALTY_MA12_CROSS_WEIGHT { get; set; }

        [Parameter("PENALTY_MA123_CROSS_WEIGHT", DefaultValue = 20)]
        public int PENALTY_MA123_CROSS_WEIGHT { get; set; }

        [Parameter("PENALTY_MA1_POINT_WEIGHT", DefaultValue = 20)]
        public int PENALTY_MA1_POINT_WEIGHT { get; set; }

        [Parameter("PENALTY_MA2_POINT_WEIGHT", DefaultValue = 20)]
        public int PENALTY_MA2_POINT_WEIGHT { get; set; }

        [Parameter("PENALTY_MA3_POINT_WEIGHT", DefaultValue = 40)]
        public int PENALTY_MA3_POINT_WEIGHT { get; set; }

        [Parameter("ADXLevel", DefaultValue = 30)]
        public int ADXLevel { get; set; }        

        [Parameter("Use Range Method", DefaultValue = false)]
        public bool UseRange { get; set; }   

        // [Parameter("PENALTY_RAINBOW_WEIGHT", DefaultValue = 25)]
        // public int PENALTY_RAINBOW_WEIGHT { get; set; }

        // [Parameter("PENALTY_RAINBOW_HIGH_LEVEL", DefaultValue = 47)]
        // public int PENALTY_RAINBOW_HIGH_LEVEL { get; set; }

        // [Parameter("PENALTY_RAINBOW_LOW_LEVEL", DefaultValue = -53)]
        // public int PENALTY_RAINBOW_LOW_LEVEL { get; set; }

        [Parameter("STOCH_KPERIODS", DefaultValue = 6)]
        public int STOCH_KPERIODS { get; set; }

        [Parameter("STOCH_KSLOWING", DefaultValue = 3)]
        public int STOCH_KSLOWING { get; set; }

        [Parameter("STOCH_DPERIODS", DefaultValue = 3)]
        public int STOCH_DPERIODS { get; set; }


        [Parameter()]
        public DataSeries Source { get; set; }

        private StochasticOscillator _STO;
        private RainbowOscillator _RAIN;
        private MovingAverage _MA1;
        private MovingAverage _MA2;
        private MovingAverage _MA3;
        private DirectionalMovementSystem _DMS;
        private Random random = new Random();

        private const int MAX_PENALTY_VALUE = 100;

        private const int MIN_PENALTY_VALUE = 0;

        private bool _IsBlockGreen;

        protected override void Initialize()
        {
            _STO = Indicators.StochasticOscillator(STOCH_KPERIODS, STOCH_KSLOWING, STOCH_DPERIODS, MovingAverageType.Simple);
            _MA1 = Indicators.MovingAverage(Source, 16, MovingAverageType.Simple);
            _MA2 = Indicators.MovingAverage(Source, 8, MovingAverageType.Exponential);
            _MA3 = Indicators.MovingAverage(Source, 64, MovingAverageType.Simple);
            _RAIN = Indicators.RainbowOscillator(Source, 9, MovingAverageType.Simple);
            _DMS = Indicators.DirectionalMovementSystem(6);
        }

        public override void Calculate(int index)
        {
            if (index == 0)
                return;

            try
            {
                _IsBlockGreen = isGreenCandle(Bars[index].Open, Bars[index].Close);

                double _roundLongScore = 0;
                double _roundShortScore = 0;
                double _diffLong = 0;

                for (int i = 0; i < Rounds; i++)
                {
                    _roundLongScore += CalculateScore(TradeType.Buy, (index - i));
                    _roundShortScore += CalculateScore(TradeType.Sell, (index - i));
                    _diffLong = _roundLongScore - _roundShortScore;                    
                }

                CloseLong[index] = (_roundLongScore / Rounds);
                CloseShort[index] = (_roundShortScore / Rounds);

                // DrawRange(index);
                // DrawDobbeltBottom1(index);
                // StairsDown(index);
                

            } catch (Exception)
            {
                return;
            }
        }

        private void DrawDobbeltBottom1(int index) {

            string[] check = { "green", "red", "red", "red", "red", "green", "green", "green", "green", "red", "red", "red" };
            int[] blockindexlist = CheckPattern(check, index);
            foreach(int block in blockindexlist)
            {   
                Chart.DrawIcon(Bars[block].OpenTime.ToString(), ChartIconType.Star, block, Bars[block].High, Color.Blue);
            }

            
        }

        private void StairsDown(int index) {
            
            string[] check = { "red", "green", "red", "red", "green", "red", "red", "green", "red", "red", "green" };
            int[] blockindexlist = CheckPattern(check, index);
            foreach(int block in blockindexlist)
            {   
                Chart.DrawIcon(Bars[block].OpenTime.ToString(), ChartIconType.Star, block, Bars[block].High, Color.Yellow);
            }

            string[] check2 = { "red", "red", "green", "red", "red", "green", "red", "green" };
            int[] blockindexlist2 = CheckPattern(check2, index);
            foreach(int block in blockindexlist2)
            {   
                Chart.DrawIcon(Bars[block].OpenTime.ToString(), ChartIconType.Star, block, Bars[block].High, Color.Yellow);
            }

            string[] check3 = { "red", "green", "green", "red", "red", "red", "green", "red", "red" };
            int[] blockindexlist3 = CheckPattern(check3, index);
            foreach(int block in blockindexlist3)
            {   
                Chart.DrawIcon(Bars[block].OpenTime.ToString(), ChartIconType.Star, block, Bars[block].High, Color.Yellow);
            }

            string[] check4 = { "red", "green", "red", "green", "red", "red" };
            int[] blockindexlist4 = CheckPattern(check4, index);
            foreach(int block in blockindexlist4)
            {   
                Chart.DrawIcon(Bars[block].OpenTime.ToString(), ChartIconType.Star, block, Bars[block].High, Color.Yellow);
            }

            string[] check5 = { "green", "red", "red", "red", "green", "red", "red", "green"  };
            int[] blockindexlist5 = CheckPattern(check5, index);
            foreach(int block in blockindexlist5)
            {   
                Chart.DrawIcon(Bars[block].OpenTime.ToString(), ChartIconType.Star, block, Bars[block].High, Color.Yellow);
            }

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
            
            double MA12CrossPenalty = GetMA12CrossPenalty(tradeType, index);
            double MA123CrossPenalty = GetMA123CrossPenalty(tradeType, index);
            double MA1PointPenalty = GetMA1PointPenalty(tradeType, index);
            double MA2PointPenalty = GetMA2PointPenalty(tradeType, index);
            double MA3PointPenalty = GetMA3PointPenalty(tradeType, index);

            if ( UseRange )
            {
                if( InRange(index) == true )
                {
                    // when in range only use stoch
                    MA1PointPenalty = 0;
                    MA2PointPenalty = 0;
                    MA12CrossPenalty = 0;
                    MA123CrossPenalty = 0;
                    MA3PointPenalty = 0;
                }
                else
                {
                    // When out of range only use MAs
                    stochDPenalty = 0;
                    stochKPenalty = 0;
                }
            }

            double _penalty = (stochDPenalty + stochKPenalty + candlePenalty + MA12CrossPenalty + MA123CrossPenalty + MA1PointPenalty + MA2PointPenalty + MA3PointPenalty);

            if (_penalty > MAX_PENALTY_VALUE)
            {
                return MAX_PENALTY_VALUE;
            }
            else if (_penalty < MIN_PENALTY_VALUE)
            {
                return MIN_PENALTY_VALUE;
            }

            return _penalty;

        }

        private bool InRange(int index)
        {
            if (_DMS.ADX[index] < ADXLevel)
            {
                return true;
            }
            return false;
        }

        private double GetMA12CrossPenalty(TradeType tradeType, int index)
        {

            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA2.Result[index] < _MA1.Result[index]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA2.Result[index] > _MA1.Result[index]))
            {
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_MA12_CROSS_WEIGHT);

        }

        private double GetMA123CrossPenalty(TradeType tradeType, int index)
        {

            double _penalty = 1;

            if (tradeType == TradeType.Buy && (_MA2.Result[index] > _MA3.Result[index] && _MA1.Result[index] > _MA3.Result[index] ))
            {
                _penalty = 0;
            }

            if (tradeType == TradeType.Sell && (_MA2.Result[index] < _MA3.Result[index] && _MA1.Result[index] < _MA3.Result[index] ))
            {
                _penalty = 0;
            }

            return (_penalty * PENALTY_MA123_CROSS_WEIGHT);

        }

        private double GetMA1PointPenalty(TradeType tradeType, int index)
        {

            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA1.Result[index] < _MA1.Result[index - 1]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA1.Result[index] > _MA1.Result[index - 1]))
            {
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_MA1_POINT_WEIGHT);

        }

        private double GetMA2PointPenalty(TradeType tradeType, int index)
        {

            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA2.Result[index] < _MA2.Result[index - 1]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA2.Result[index] > _MA2.Result[index - 1]))
            {
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_MA2_POINT_WEIGHT);


        }

        private double GetMA3PointPenalty(TradeType tradeType, int index)
        {

            double _penalty = 0;

            if (tradeType == TradeType.Buy && (_MA3.Result[index] < _MA3.Result[index - 1]))
            {
                _penalty = _penalty + 1;
            }

            if (tradeType == TradeType.Sell && (_MA3.Result[index] > _MA3.Result[index - 1]))
            {
                _penalty = _penalty + 1;
            }

            return (_penalty * PENALTY_MA3_POINT_WEIGHT);


        }

        private double GetStochKPenalty(TradeType tradeType, int index)
        {

            double _penalty = 0;

            if (tradeType == TradeType.Sell && ( (_STO.PercentK[index] > _STO.PercentK[index - 1] ) || ( _STO.PercentK[index] > 95 ) ))
            {
                _penalty = 1;
            }

            if (tradeType == TradeType.Buy && ( (_STO.PercentK[index] < _STO.PercentK[index - 1] ) || ( _STO.PercentK[index] < 5 )))
            {
                _penalty = 1;
            }

            return (_penalty * PENALTY_STOCHK_WEIGHT);

        }

        private double GetStochDPenalty(TradeType tradeType, int index)
        {

            double _penalty = 0;


            if (tradeType == TradeType.Sell && (_STO.PercentD[index] > _STO.PercentD[index - 1] ))
            {
                _penalty = 1;
            }

            if (tradeType == TradeType.Buy && (_STO.PercentD[index] < _STO.PercentD[index - 1] ))
            {
                _penalty = 1;
            }

            return (_penalty * PENALTY_STOCHD_WEIGHT);

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

        // private void ShowExits(TradeType tradeType)
        // {

        //     if (tradeType == TradeType.Sell && (CountConsecutiveCloseScore(tradeType, 2, 1, 0) == 2))
        //     {
        //         Print("Draw this bitch");
        //         Chart.DrawIcon(Bars[1].OpenTime.ToString(), ChartIconType.UpArrow, 1, Bars[1].Low, Color.Green);
        //     }

        //     if (tradeType == TradeType.Buy && (CountConsecutiveCloseScore(tradeType, 1, 1, 0) == 1))
        //     {
        //         Print("snoop dooog");
        //         Chart.DrawIcon(Bars[1].OpenTime.ToString(), ChartIconType.DownArrow, 1, Bars[1].High, Color.Red);
        //     }

        // }

        private int[] CheckPattern(string[] blocks, int startIndex) {

            int[] blockIndexlist = new int [] {};
            var tempList = blockIndexlist.ToList();

            if(startIndex==0)
            {
                return tempList.ToArray();
            }

            // string[] blocks = { "green", "red", "green", "red", "green" };
            foreach(string block in blocks)
            {
                string blockresult = (isGreenCandle(Bars[startIndex].Open, Bars[startIndex].Close)) ? "green" : "red";
                if( blockresult == block )
                {
                    tempList.Add(startIndex);
                }
                else
                {
                    return new int [] {};
                }
                startIndex--;
            }

            return tempList.ToArray();

        }

    }
    
}
