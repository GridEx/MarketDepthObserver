using System.Runtime.CompilerServices;

namespace GridEx.MarketDepthObserver.Classes
{
	class MarketSnapshotVisual
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe MarketSnapshotVisual(ref PriceVolumePair[] buyArray, ref PriceVolumePair[] sellArray)
		{
			var MaxVolumeAsk = 0d;
			var MaxVolumeBid = 0d;

			MarketValueVisualsAsk = new MarketValueVisual[sellArray.Length];
			MarketValueVisualsBid = new MarketValueVisual[buyArray.Length];

			for (int i = 0; i < sellArray.Length; i++)
			{
				MarketValueVisualsAsk[i] = new MarketValueVisual(sellArray[i]);
				MaxVolumeAsk += sellArray[i].Volume;
			}
			for (int i = 0; i < buyArray.Length; i++)
			{
				MarketValueVisualsBid[i] = new MarketValueVisual(buyArray[i]);
				MaxVolumeBid += buyArray[i].Volume;
			}

			for (int i = 0; i < MarketValueVisualsAsk.Length; i++)
			{
				MarketValueVisualsAsk[i].CalculatePercentOfTotalSum(
					i == 0
					? 0
					: MarketValueVisualsAsk[i - 1].CurrentSum,
					MaxVolumeAsk);
			}
			for (int i = 0; i < MarketValueVisualsBid.Length; i++)
			{
				MarketValueVisualsBid[i].CalculatePercentOfTotalSum(
					i == 0
					? 0
					: MarketValueVisualsBid[i - 1].CurrentSum,
					MaxVolumeBid);
			}
		}

		public readonly MarketValueVisual[] MarketValueVisualsAsk;
		public readonly MarketValueVisual[] MarketValueVisualsBid;
	}
}
