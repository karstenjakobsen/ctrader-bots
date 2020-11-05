# Desc

## Risk Management

* We want dynamic lot sizes in regards to the equity

### Trend change

When aa trend changes use indicators to check:

* weather to close postiions or not

* Dont market buy on first candle use limit orders - wicks are very prone in reversal and trend areas.

* Move all open position stop losses near the new trend candle

* One market order and one limit order as standard

ingen køb unde major trend og vice versa
Maybe use 20 day EMA for selling buiying
begge MA skal pege i samme renting
NO MAKRET BUY. Altid targets
move sl til 0 hvis pofit > 0.7 * blocksize

Close scire = Stochscore + lastCandlescore

Only points when velocity is high and score is falling

TODO 
allow close in negative if 2 or moreblocks have passed
use stoch to enter insted og close score
no trading in ranges


in range sell on candle change
in range take profits sooner
fix buttons update