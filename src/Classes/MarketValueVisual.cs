namespace GridEx.MarketDepthObserver.Classes
{
	public struct MarketValueVisual
	{
		public MarketValueVisual(PriceVolumePair value)
		{
			Price = value.Price;
			Value = value.Volume;
			CurrentSum = 0;
			PercentOfTotalSum = 0;
		}

		public MarketValueVisual(double price, double value)
		{
			Price = price;
			Value = value;
			CurrentSum = 0;
			PercentOfTotalSum = 0;
		}

		public void CalculatePercentOfTotalSum(double prevoiusSum, double maxValue)
		{
			CurrentSum = prevoiusSum + Value;
			PercentOfTotalSum = maxValue <= 0 ? 0 : (CurrentSum / maxValue);
		}

		public double Price { get; private set; }
		public double Value { get; private set; }
		public double CurrentSum { get; private set; }
		public double PercentOfTotalSum { get; private set; }
	}
}
