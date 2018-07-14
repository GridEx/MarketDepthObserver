using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using GridEx.API.MarketStream;

namespace MarketDepthClient.Classes
{
	public sealed class MarketSnapshotBuilder
	{
		public MarketSnapshotBuilder()
		{
			_marketSnapshot = new MarketSnapshot(DateTimeOffset.UtcNow.Ticks);
			_selector = OrderBy;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Build(MarketChange change)
		{
			_volumes.TryGetValue(change.Price, out var volume);

			switch (change.MarketChangeType)
			{
				case MarketChangeTypeCode.BidByAddedOrder:
				case MarketChangeTypeCode.BidByExecutedOrder:
				case MarketChangeTypeCode.BidByCancelledOrder:
					if (volume == Zero)
					{
						if (change.Volume != Zero)
						{
							_buys.Add(change.Price);
						}
					}
					else
					{
						if (change.Volume == Zero)
						{
							_buys.Remove(change.Price);
						}
					}

					_bid = change.Price;
					break;
				case MarketChangeTypeCode.AskByAddedOrder:
				case MarketChangeTypeCode.AskByExecutedOrder:
				case MarketChangeTypeCode.AskByCancelledOrder:
					if (volume == Zero)
					{
						if (change.Volume != Zero)
						{
							_sells.Add(change.Price);
						}
					}
					else
					{
						if (change.Volume == Zero)
						{
							_sells.Remove(change.Price);
						}
					}
					_ask = change.Price;
					break;
				case MarketChangeTypeCode.VolumeByAddedOrder:
				case MarketChangeTypeCode.VolumeByExecutedOrder:
				case MarketChangeTypeCode.VolumeByCancelledOrder:
					if (volume == Zero)
					{
						if (change.Volume != Zero)
						{

							if (change.Price < _bid)
							{
								_buys.Add(change.Price);
							}
							else
							{
								_sells.Add(change.Price);
							}
						}
					}
					else
					{
						if (change.Volume == Zero)
						{
							if (change.Price < _bid)
							{
								_buys.Remove(change.Price);
							}
							else
							{
								_sells.Remove(change.Price);
							}
						}
					}
					break;
				default:
					throw new NotSupportedException($"Not supported type: '{change.MarketChangeType}'.");
			}

			volume = change.Volume;

			return true;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public MarketSnapshot CreateSnapshot()
		{
			_marketSnapshot.Time = DateTimeOffset.UtcNow.Ticks;

			FillBuySide();

			FillSellSide();

			return _marketSnapshot;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private unsafe void FillBuySide()
		{
			var buyIndex = 0;
			foreach (var buy in _buys.OrderByDescending(_selector).Take(MarketSnapshot.Depth))
			{
				_volumes.TryGetValue(buy, out var volume);
				_marketSnapshot.BuyPrices[buyIndex] = buy;
				_marketSnapshot.BuyVolumes[buyIndex] = volume;
				++buyIndex;
			}

			for (var i = buyIndex; i < MarketSnapshot.Depth; ++i)
			{
				_marketSnapshot.BuyPrices[i] = Zero;
				_marketSnapshot.BuyVolumes[i] = Zero;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private unsafe void FillSellSide()
		{
			var sellIndex = 0;
			foreach (var sell in _sells.OrderBy(_selector).Take(MarketSnapshot.Depth))
			{
				_volumes.TryGetValue(sell, out var volume);
				_marketSnapshot.SellPrices[sellIndex] = sell;
				_marketSnapshot.SellVolumes[sellIndex] = volume;
				++sellIndex;
			}

			for (var i = sellIndex; i < MarketSnapshot.Depth; ++i)
			{
				_marketSnapshot.SellPrices[i] = Zero;
				_marketSnapshot.SellVolumes[i] = Zero;
			}
		}

		private static double OrderBy(double d)
		{
			return d;
		}

		private readonly Func<double, double> _selector;
		private readonly Dictionary<double, double> _volumes = new Dictionary<double, double>(1048576);
		private readonly HashSet<double> _buys = new HashSet<double>(1048576);
		private readonly HashSet<double> _sells = new HashSet<double>(1048576);
		private double _bid = double.MinValue;
		private double _ask = double.MaxValue;
		private const double Zero = 0;
		private MarketSnapshot _marketSnapshot;
	}
}
