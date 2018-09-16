using System.Runtime.CompilerServices;

namespace GridEx.MarketDepthObserver.Classes
{
	public static class MarketSnapshotVisual
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static void FillVisualPrices(
			PriceVolumePair[] buyArray, 
			int buyAmount, 
			PriceVolumePair[] sellArray, 
			int sellAmount,
			MarketValueVisual[] asks,
			MarketValueVisual[] bids)
		{
			var MaxVolumeAsk = 0d;
			var MaxVolumeBid = 0d;

			for (int i = 0; i < sellAmount; i++)
			{
				asks[i] = new MarketValueVisual(sellArray[i]);
				MaxVolumeAsk += sellArray[i].Volume;
			}
			for (int i = 0; i < buyAmount; i++)
			{
				bids[i] = new MarketValueVisual(buyArray[i]);
				MaxVolumeBid += buyArray[i].Volume;
			}

			for (int i = 0; i < sellAmount; i++)
			{
				asks[i].CalculatePercentOfTotalSum(
					i == 0
					? 0
					: asks[i - 1].CurrentSum,
					MaxVolumeAsk);
			}
			for (int i = 0; i < buyAmount; i++)
			{
				bids[i].CalculatePercentOfTotalSum(
					i == 0
					? 0
					: bids[i - 1].CurrentSum,
					MaxVolumeBid);
			}
		}
	}
}
