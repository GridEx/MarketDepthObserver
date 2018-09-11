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
			var volume = new PriceVolumePair(change.Price, change.Volume);

			lock (_syncRoot)
			{
				switch (change.MarketChangeType)
				{
					case MarketChangeTypeCode.BidByAddedOrder:
					case MarketChangeTypeCode.BidByExecutedOrder:
					case MarketChangeTypeCode.BidByCanceledOrder:
						Debug.Assert(change.Volume != 0, $"Price={change.Price} Volume={change.Volume} Type={change.MarketChangeType.ToString()}");
						if (!_bids.Add(volume))
						{
							_bids.Remove(volume);
							_bids.Add(volume);
						}
						break;                                           
					case MarketChangeTypeCode.AskByAddedOrder:
					case MarketChangeTypeCode.AskByExecutedOrder:
					case MarketChangeTypeCode.AskByCanceledOrder:
						Debug.Assert(change.Volume != 0, $"Price={change.Price} Volume={change.Volume} Type={change.MarketChangeType.ToString()}");
						if (!_asks.Add(volume))
						{
							_asks.Remove(volume);
							_asks.Add(volume);
						}
						break;
					case MarketChangeTypeCode.BuyVolumeByAddedOrder:
					case MarketChangeTypeCode.BidVolumeByExecutedOrder:
					case MarketChangeTypeCode.BuyVolumeByCanceledOrder:
						if (change.Volume == 0)
						{
							_bids.Remove(volume);
						}
						else
						{
							if (!_bids.Add(volume))
							{
								_bids.Remove(volume);
								_bids.Add(volume);
							}
						}
						break;
					case MarketChangeTypeCode.SellVolumeByAddedOrder:
					case MarketChangeTypeCode.AskVolumeByExecutedOrder:
					case MarketChangeTypeCode.SellVolumeByCanceledOrder:
						if (change.Volume == 0)
						{
							_asks.Remove(volume);
						}
						else
						{
							if (!_asks.Add(volume))
							{
								_asks.Remove(volume);
								_asks.Add(volume);
							}
						}
						break;
					case MarketChangeTypeCode.BuyVolumeInfoAdded:
						if (!_bids.Add(volume))
						{
							_bids.Remove(volume);
							_bids.Add(volume);
						}
						break;
					case MarketChangeTypeCode.SellVolumeInfoAdded:
						if (!_asks.Add(volume))
						{
							_asks.Remove(volume);
							_asks.Add(volume);
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
				_bids.Clear();
				_asks.Clear();

				for (int i = 0; i < MarketSnapshot.MaxDepth; i++)
				{
					var bidPrice = marketSnapshot.BidPrices[i];
					var bidVolume = marketSnapshot.BidVolumes[i];
					if (bidPrice > 0 && bidVolume > 0)
					{
						_bids.Add(new PriceVolumePair(bidPrice, bidVolume));
					}

					var askPrice = marketSnapshot.AskPrices[i];
					var askVolume = marketSnapshot.AskVolumes[i];
					if (askPrice > 0 && askVolume > 0)
					{
						_asks.Add(new PriceVolumePair(askPrice, askVolume));
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CreateSnapshot(PriceVolumePair[] bidArray, out int bidIndex, PriceVolumePair[] askArray, out int askIndex)
		{
			lock (_syncRoot)
			{
				bidIndex = 0;
				foreach (var bid in _bids.Take(Math.Min(_bids.Count, MarketSnapshot.MaxDepth)))
				{
					bidArray[bidIndex++] = bid;
				}

				askIndex = 0;
				foreach (var ask in _asks.Take(Math.Min(_asks.Count, MarketSnapshot.MaxDepth)))
				{
					askArray[askIndex++] = ask;
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

		private readonly SortedSet<PriceVolumePair> _asks = new SortedSet<PriceVolumePair>(new PriceVolumePairComparer());
		private readonly SortedSet<PriceVolumePair> _bids = new SortedSet<PriceVolumePair>(new PriceVolumePairComparerReverse());
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
