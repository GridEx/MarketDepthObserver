using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GridEx.API;
using GridEx.API.MarketDepth;
using GridEx.API.MarketDepth.Responses;

namespace GridEx.MarketDepthObserver.Classes
{
	class MarketClient : IDisposable
	{
		public Action<MarketClient, Exception> OnException = delegate { };
		public Action<MarketClient> OnConnected = delegate { };
		public Action<MarketClient, SocketError> OnError = delegate { };
		public Action<MarketClient> OnDisconnected = delegate { };
		public Action<string> AddMessageToFileLog = delegate { }; 

		public MarketClient(ManualResetEventSlim enviromentExitWait, ManualResetEventSlim processesStartedEvent)
		{
			_enviromentExitWait = enviromentExitWait;
			_processesStartedEvent = processesStartedEvent;
			_marketSnapshotBuilder = new MarketSnapshotBuilder();
		}

		public bool IsConnected
		{
			get
			{
				var socket = _marketDepthSocket;
				return socket != null && socket.IsConnected;
			}
		}
			
		public void Run(IPAddress serverAddress, int serverPort, 
			ref CancellationTokenSource cancellationTokenSource,
			ref ManualResetEventSlim enviromentExitWait)
		{
			_enviromentExitWait = enviromentExitWait;
			_cancellationTokenSource = cancellationTokenSource;
			_marketSnapshotBuilder = new MarketSnapshotBuilder();

			var socket = new MarketDepthSocket();
			_marketDepthSocket = socket;
			socket.OnError += OnErrorHandler;
			socket.OnDisconnected += OnDisconnectedHandler;
			socket.OnConnected += OnConnectedHandler;
			socket.OnException += OnExceptionHandler;
			socket.OnMarketChange += OnMarketChangeHandler;
			socket.OnMarketSnapshotLevel3 += OnMarketSnapshotHandler;

			void RunSocket()
			{
				try
				{
					socket.Connect(new IPEndPoint(serverAddress.MapToIPv4(), serverPort));
					socket.WaitResponses(_cancellationTokenSource.Token);
				}
				catch
				{
					try
					{
						socket.Dispose();
					}
					catch { }
				}
				finally
				{
					Dispose();
				}
			}
			Task.Factory.StartNew(
				() => RunSocket(),
				TaskCreationOptions.LongRunning);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void GetSnapshot(PriceVolumePair[] buyArray, out int buyAmount, PriceVolumePair[] sellArray, out int sellAmount)
		{
			_marketSnapshotBuilder.CreateSnapshot(buyArray, out buyAmount, sellArray, out sellAmount);
		}

		public void Disconnect(bool waitLittlePause = false)
		{
			if (!_cancellationTokenSource.IsCancellationRequested)
			{
				_cancellationTokenSource.Cancel();
			}

			var socket = _marketDepthSocket;
			if (socket != null)
			{
				try
				{
					socket.OnError -= OnErrorHandler;
					socket.OnDisconnected -= OnDisconnectedHandler;
					socket.OnConnected -= OnConnectedHandler;
					socket.OnException -= OnExceptionHandler;
					socket.OnMarketChange -= OnMarketChangeHandler;
					socket.OnMarketSnapshotLevel3 -= OnMarketSnapshotHandler;
					socket.Dispose();
				}
				catch
				{

				}
				finally
				{
					_marketDepthSocket = null;
				}
			}

			if (waitLittlePause)
			{
				Thread.Sleep(1000);
			}

			if (!_enviromentExitWait.IsSet)
			{
				_enviromentExitWait.Set();
			}
		}

		public void Dispose()
		{
			Disconnect();
		}
		public long ReadAndResetCountOfDimensions()
		{
			return Interlocked.Exchange(ref _countOfDimensions, 0);
		}

		private void OnMarketChangeHandler(MarketDepthSocket marketDepthSocket, MarketChange marketChange)
		{
			_marketSnapshotBuilder.Build(ref marketChange);
			Interlocked.Increment(ref _countOfDimensions);

			if (AddMessageToFileLog != null)
			{
				string marketChangeType = "??";
				switch (marketChange.MarketChangeType)
				{
					case MarketChangeTypeCode.AskPriceByAddedOrder:
						marketChangeType = "AA";
						break;
					case MarketChangeTypeCode.AskPriceByCanceledOrder:
						marketChangeType = "AC";
						break;
					case MarketChangeTypeCode.AskPriceByExecutedOrder:
						marketChangeType = "AE";
						break;
					case MarketChangeTypeCode.BidPriceByAddedOrder:
						marketChangeType = "BA";
						break;
					case MarketChangeTypeCode.BidPriceByCanceledOrder:
						marketChangeType = "BC";
						break;
					case MarketChangeTypeCode.BidPriceByExecutedOrder:
						marketChangeType = "BE";
						break;
					case MarketChangeTypeCode.BidVolumeByAddedOrder:
					case MarketChangeTypeCode.BuyingVolumeByAddedOrder:
						marketChangeType = "VABuy";
						break;
					case MarketChangeTypeCode.BidVolumeByCanceledOrder:
					case MarketChangeTypeCode.BuyingVolumeByCanceledOrder:
						marketChangeType = "VCB";
						break;
					case MarketChangeTypeCode.BidVolumeByExecutedOrder:
						marketChangeType = "VEB";
						break;
					case MarketChangeTypeCode.BuyingVolumeInfoAdded:
						marketChangeType = "IVB";
						break;
					case MarketChangeTypeCode.AskVolumeByAddedOrder:
					case MarketChangeTypeCode.SellingVolumeByAddedOrder:
						marketChangeType = "VAS";
						break;
					case MarketChangeTypeCode.AskVolumeByCanceledOrder:
					case MarketChangeTypeCode.SellingVolumeByCanceledOrder:
						marketChangeType = "VCS";
						break;
					case MarketChangeTypeCode.AskVolumeByExecutedOrder:
						marketChangeType = "VEA";
						break;
					case MarketChangeTypeCode.SellingVolumeInfoAdded:
						marketChangeType = "IVS";
						break;
				}
				AddMessageToFileLog?.Invoke($"{DateTime.Now.ToString("mm:ss.fff")} P={marketChange.Price.ToString("F11")} V={marketChange.Volume.ToString("F11")} {marketChangeType}{Environment.NewLine}");
			}
		}

		private unsafe void OnMarketSnapshotHandler(MarketDepthSocket socket, ref MarketSnapshotLevel3 marketSnapshot)
		{
			_marketSnapshotBuilder.ReBuild(ref marketSnapshot);
			if (AddMessageToFileLog != null)
			{
				StringBuilder log = new StringBuilder($"----- IN  MS - {DateTime.Now.ToString("hh:mm:ss.fff")}{Environment.NewLine}");
				for (int i = 0; i < MarketSnapshotLevel3.MaxDepth; i++)
				{
					log.AppendLine($"     BP={marketSnapshot.BidPrices[i].ToString("F11")} BV={marketSnapshot.BidVolumes[i].ToString("F11")}");
					log.AppendLine($"     SP={marketSnapshot.AskPrices[i].ToString("F11")} SV={marketSnapshot.AskVolumes[i].ToString("F11")}{Environment.NewLine}");
				}
				log.AppendLine($"----- OUT  MS");
				AddMessageToFileLog?.Invoke(log.ToString());
			}
		}

		void OnExceptionHandler(GridExSocketBase socket, Exception exception)
		{
			var s = _marketDepthSocket;
			if (s != null)
			{
				s.OnException -= OnExceptionHandler;
			}

			Disconnect();
			OnException?.Invoke(this, exception);
		}

		void OnErrorHandler(GridExSocketBase socket, SocketError error)
		{
			OnError?.Invoke(this, error);
		}

		void OnDisconnectedHandler(GridExSocketBase socket)
		{
			Disconnect();
			OnDisconnected?.Invoke(this);
		}

		void OnConnectedHandler(GridExSocketBase socket)
		{
			_processesStartedEvent.Set();
			OnConnected?.Invoke(this);
		}

		private MarketDepthSocket _marketDepthSocket;
		private MarketSnapshotBuilder _marketSnapshotBuilder;

		private ManualResetEventSlim _enviromentExitWait;
		private ManualResetEventSlim _processesStartedEvent;

		private CancellationTokenSource _cancellationTokenSource;

		private long _countOfDimensions = 0;

		public object Factory { get; private set; }
	}
}
