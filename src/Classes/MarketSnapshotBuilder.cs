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
			_marketSnapshot = new MarketSnapshot(DateTimeOffset.UtcNow.Ticks);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Build(ref MarketChange change)
		{
			Debug.Assert(change.Price != 0, $"Price={change.Price} Volume={change.Volume} Type={change.MarketChangeType.ToString()}");

			if (change.Price == 0)
				return;

			lock (snapshotLocker)
			{
				var volume = new PriceVolumePair(change.Price, change.Volume);

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
						_bid = change.Price;
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
						_ask = change.Price;
						break;
					case MarketChangeTypeCode.VolumeByAddedOrder:
					case MarketChangeTypeCode.VolumeByExecutedOrder:
					case MarketChangeTypeCode.VolumeByCancelledOrder:
						if (change.Volume == 0)
						{
							if (IsBuy(change))
							{
								_buys.Remove(volume);
							}
							else
							{
								_sells.Remove(volume);
							}
						}
						else
						{
							if (IsBuy(change))
							{
								if (!_buys.Add(volume))
								{
									_buys.Remove(volume);
									_buys.Add(volume);
								}
							}
							else
							{
								if (!_buys.Add(volume))
								{
									_sells.Remove(volume);
									_sells.Add(volume);
								}
							}
						}
						break;
					case MarketChangeTypeCode.InfoAboutVolumeAdded:
						if (IsBuy(change))
						{
							if (!_buys.Add(volume))
							{
								_buys.Remove(volume);
								_buys.Add(volume);
							}
						}
						else
						{
							if (!_buys.Add(volume))
							{
								_sells.Remove(volume);
								_sells.Add(volume);
							}
						}
						break;
					default:
						throw new NotSupportedException($"Not supported type: '{change.MarketChangeType}'.");
				}

				#region Reset _bid and _ask
				if (_buys.Count == 0)
				{
					_bid = double.MinValue;
				}

				if (_sells.Count == 0)
				{
					_ask = double.MaxValue;
				}
				#endregion
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public unsafe void ReBuild(ref MarketSnapshot marketSnapshot)
		{
			lock (snapshotLocker)
			{
				_bid = double.MinValue;
				_ask = double.MaxValue;
				_buys.Clear();
				_sells.Clear();

				for (int i = 0; i < MarketSnapshot.Depth; i++)
				{
					ref var price = ref marketSnapshot.BuyPrices[i];
					ref var volume = ref marketSnapshot.BuyVolumes[i];
					if (price > 0 && volume > 0)
					{
						_buys.Add(new PriceVolumePair(price, volume));
						_bid = price;
					}

					price = ref marketSnapshot.SellPrices[i];
					volume = ref marketSnapshot.SellVolumes[i];
					if (price > 0 && volume > 0)
					{
						_sells.Add(new PriceVolumePair(price, volume));
						_ask = price;
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CreateSnapshot(out PriceVolumePair[] buysArray, out PriceVolumePair[] sellsArray)
		{
			buysArray = new PriceVolumePair[0];
			sellsArray = new PriceVolumePair[0];
			lock (snapshotLocker)
			{
				buysArray = _buys.Take(Math.Min(_buys.Count, MarketSnapshot.Depth)).ToArray();
				sellsArray = _sells.Take(Math.Min(_sells.Count, MarketSnapshot.Depth)).ToArray();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private bool IsBuy(in MarketChange change)
		{
#if DEBUG
			if (_bid == EmptyBid && _ask == EmptyAsk)
			{
				throw new InvalidOperationException("_bid == Price.EmptyBid && _ask == Price.EmptyAsk");
			}
#endif
			if (_bid == double.MinValue)
			{
				return _ask > change.Price;
			}

			return _bid >= change.Price;
		}

		private sealed class ReverseComparer : IComparer<double>
		{
			public int Compare(double x, double y)
			{
				if (x > y)
				{
					return -1;
				}

				if (x < y)
				{
					return 1;
				}

				return 0;
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
		private double _bid = double.MinValue;
		private double _ask = double.MaxValue;
		private MarketSnapshot _marketSnapshot;
		private object snapshotLocker = new object();
		private const double EmptyBid = double.MinValue;
		private const double EmptyAsk = double.MaxValue;
	}

	public class PriceVolumePair
	{
		public PriceVolumePair(double price, double volume)
		{
			Price = price;
			Volume = volume;
		}

		public readonly double Price;
		public double Volume;
	}
}
