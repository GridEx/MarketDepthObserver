using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using GridEx.API.MarketStream;

namespace GridEx.MarketDepthObserver.Classes
{
	public sealed class MarketSnapshotBuilder
	{
		public MarketSnapshotBuilder()
		{
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Build(ref MarketChange change)
		{
			Debug.Assert(change.Price != 0, $"Price={change.Price} Volume={change.Volume} Type={change.MarketChangeType.ToString()}");
			if (change.Price == 0)
			{
				return;
			}

			var volume = new PriceVolumePair(change.Price, change.Volume);

			lock (_syncRoot)
			{
				switch (change.MarketChangeType)
				{
					case MarketChangeTypeCode.BidByAddedOrder:
					case MarketChangeTypeCode.BidByExecutedOrder:
					case MarketChangeTypeCode.BidByCancelledOrder:
						Debug.Assert(change.Volume != 0, $"Price={change.Price} Volume={change.Volume} Type={change.MarketChangeType.ToString()}");
						if (!_buys.Add(volume))
						{
							_buys.Remove(volume);
							_buys.Add(volume);
						}
						break;                                           
					case MarketChangeTypeCode.AskByAddedOrder:
					case MarketChangeTypeCode.AskByExecutedOrder:
					case MarketChangeTypeCode.AskByCancelledOrder:
						Debug.Assert(change.Volume != 0, $"Price={change.Price} Volume={change.Volume} Type={change.MarketChangeType.ToString()}");
						if (!_sells.Add(volume))
						{
							_sells.Remove(volume);
							_sells.Add(volume);
						}
						break;
					case MarketChangeTypeCode.BuyVolumeByAddedOrder:
					case MarketChangeTypeCode.BidVolumeByExecutedOrder:
					case MarketChangeTypeCode.BuyVolumeByCancelledOrder:
						if (change.Volume == 0)
						{
							_buys.Remove(volume);
						}
						else
						{
							if (!_buys.Add(volume))
							{
								_buys.Remove(volume);
								_buys.Add(volume);
							}
						}
						break;
					case MarketChangeTypeCode.SellVolumeByAddedOrder:
					case MarketChangeTypeCode.AskVolumeByExecutedOrder:
					case MarketChangeTypeCode.SellVolumeByCancelledOrder:
						if (change.Volume == 0)
						{
							_sells.Remove(volume);
						}
						else
						{
							if (!_sells.Add(volume))
							{
								_sells.Remove(volume);
								_sells.Add(volume);
							}
						}
						break;
					case MarketChangeTypeCode.BuyVolumeInfoAdded:
						if (!_buys.Add(volume))
						{
							_buys.Remove(volume);
							_buys.Add(volume);
						}
						break;
					case MarketChangeTypeCode.SellVolumeInfoAdded:
						if (!_sells.Add(volume))
						{
							_sells.Remove(volume);
							_sells.Add(volume);
						}
						break;
					default:
						throw new NotSupportedException($"Not supported type: '{change.MarketChangeType}'.");
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void ReBuild(ref MarketSnapshot marketSnapshot)
		{
			lock (_syncRoot)
			{
				_buys.Clear();
				_sells.Clear();

				for (int i = 0; i < MarketSnapshot.Depth; i++)
				{
					var buyPrice = marketSnapshot.BuyPrices[i];
					var buyVolume = marketSnapshot.BuyVolumes[i];
					if (buyPrice > 0 && buyVolume > 0)
					{
						_buys.Add(new PriceVolumePair(buyPrice, buyVolume));
					}

					var sellPrice = marketSnapshot.SellPrices[i];
					var sellvolume = marketSnapshot.SellVolumes[i];
					if (sellPrice > 0 && sellvolume > 0)
					{
						_sells.Add(new PriceVolumePair(sellPrice, sellvolume));
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CreateSnapshot(PriceVolumePair[] buysArray, out int buyAmount, PriceVolumePair[] sellsArray, out int sellAmount)
		{
			lock (_syncRoot)
			{
				buyAmount = 0;
				foreach (var buy in _buys.Take(Math.Min(_buys.Count, MarketSnapshot.Depth)))
				{
					buysArray[buyAmount++] = buy;
				}

				sellAmount = 0;
				foreach (var sell in _sells.Take(Math.Min(_sells.Count, MarketSnapshot.Depth)))
				{
					sellsArray[sellAmount++] = sell;
				}
			}
		}

		private sealed class PriceVolumePairComparer : IComparer<PriceVolumePair>
		{
			public int Compare(PriceVolumePair x, PriceVolumePair y)
			{
				if (x.Price > y.Price)
				{
					return 1;
				}

				if (x.Price < y.Price)
				{
					return -1;
				}

				return 0;
			}
		}

		private sealed class PriceVolumePairComparerReverse : IComparer<PriceVolumePair>
		{
			public int Compare(PriceVolumePair x, PriceVolumePair y)
			{
				if (x.Price > y.Price)
				{
					return -1;
				}

				if (x.Price < y.Price)
				{
					return 1;
				}

				return 0;
			}
		}

		private readonly SortedSet<PriceVolumePair> _sells = new SortedSet<PriceVolumePair>(new PriceVolumePairComparer());
		private readonly SortedSet<PriceVolumePair> _buys = new SortedSet<PriceVolumePair>(new PriceVolumePairComparerReverse());
		private readonly object _syncRoot = new object();
	}

	public readonly struct PriceVolumePair
	{
		public PriceVolumePair(double price, double volume)
		{
			Price = price;
			Volume = volume;
		}

		public readonly double Price;
		public readonly double Volume;
	}
}
