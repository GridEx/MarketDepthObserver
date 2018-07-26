using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using GridEx.API;
using GridEx.API.MarketStream;

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

		public bool IsConnected { get => _marketStreamSocket != null && _marketStreamSocket.IsConnected; }
			
		public void Run(IPAddress serverAddress, int serverPort, 
			ref CancellationTokenSource cancellationTokenSource,
			ref ManualResetEventSlim enviromentExitWait)
		{
			_enviromentExitWait = enviromentExitWait;
			_cancellationTokenSource = cancellationTokenSource;
			_marketSnapshotBuilder = new MarketSnapshotBuilder();

			_marketStreamSocket = new MarketStreamSocket();
			_marketStreamSocket.OnError += OnErrorHandler;
			_marketStreamSocket.OnDisconnected += OnDisconnectedHandler;
			_marketStreamSocket.OnConnected += OnConnectedHandler;
			_marketStreamSocket.OnException += OnExceptionHandler;
			_marketStreamSocket.OnMarketChange += OnMarketChangeHandler;
			_marketStreamSocket.OnMarketSnapshot += OnMarketSnapshotHandler;

			void RunSocket()
			{
				try
				{
					_marketStreamSocket.Connect(new IPEndPoint(serverAddress.MapToIPv4(), serverPort));
					_marketStreamSocket.WaitResponses(_cancellationTokenSource.Token);
				}
				catch
				{
					try
					{
						_marketStreamSocket.Dispose();
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

			if (_marketStreamSocket != null)
			{
				try
				{
					_marketStreamSocket.OnError -= OnErrorHandler;
					_marketStreamSocket.OnDisconnected -= OnDisconnectedHandler;
					_marketStreamSocket.OnConnected -= OnConnectedHandler;
					_marketStreamSocket.OnException -= OnExceptionHandler;
					_marketStreamSocket.OnMarketChange -= OnMarketChangeHandler;
					_marketStreamSocket.OnMarketSnapshot -= OnMarketSnapshotHandler;
					_marketStreamSocket.Dispose();
				}
				catch
				{

				}
				finally
				{
					_marketStreamSocket = null;
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

		private void OnMarketChangeHandler(MarketStreamSocket marketStreamSocket, MarketChange marketChange)
		{
			_marketSnapshotBuilder.Build(ref marketChange);
			Interlocked.Increment(ref _countOfDimensions);

			if (AddMessageToFileLog != null)
			{
				string marketChangeType = "??";
				switch (marketChange.MarketChangeType)
				{
					case MarketChangeTypeCode.AskByAddedOrder:
						marketChangeType = "AA";
						break;
					case MarketChangeTypeCode.AskByCancelledOrder:
						marketChangeType = "AC";
						break;
					case MarketChangeTypeCode.AskByExecutedOrder:
						marketChangeType = "AE";
						break;
					case MarketChangeTypeCode.BidByAddedOrder:
						marketChangeType = "BA";
						break;
					case MarketChangeTypeCode.BidByCancelledOrder:
						marketChangeType = "BC";
						break;
					case MarketChangeTypeCode.BidByExecutedOrder:
						marketChangeType = "BE";
						break;
					case MarketChangeTypeCode.BuyVolumeByAddedOrder:
						marketChangeType = "VABuy";
						break;
					case MarketChangeTypeCode.BuyVolumeByCancelledOrder:
						marketChangeType = "VCB";
						break;
					case MarketChangeTypeCode.BidVolumeByExecutedOrder:
						marketChangeType = "VEB";
						break;
					case MarketChangeTypeCode.BuyVolumeInfoAdded:
						marketChangeType = "IVB";
						break;
					case MarketChangeTypeCode.SellVolumeByAddedOrder:
						marketChangeType = "VAS";
						break;
					case MarketChangeTypeCode.SellVolumeByCancelledOrder:
						marketChangeType = "VCS";
						break;
					case MarketChangeTypeCode.AskVolumeByExecutedOrder:
						marketChangeType = "VEA";
						break;
					case MarketChangeTypeCode.SellVolumeInfoAdded:
						marketChangeType = "IVS";
						break;
				}
				AddMessageToFileLog?.Invoke($"{DateTime.Now.ToString("mm:ss.fff")} P={marketChange.Price.ToString("F11")} V={marketChange.Volume.ToString("F11")} {marketChangeType}{Environment.NewLine}");
			}
		}

		private unsafe void OnMarketSnapshotHandler(MarketStreamSocket marketStreamSocket, MarketSnapshot marketSnapshot)
		{
			_marketSnapshotBuilder.ReBuild(ref marketSnapshot);
			if (AddMessageToFileLog != null)
			{
				StringBuilder log = new StringBuilder($"----- IN  MS - {DateTime.Now.ToString("hh:mm:ss.fff")}{Environment.NewLine}");
				for (int i = 0; i < MarketSnapshot.Depth; i++)
				{
					log.AppendLine($"     BP={marketSnapshot.BuyPrices[i].ToString("F11")} BV={marketSnapshot.BuyVolumes[i].ToString("F11")}");
					log.AppendLine($"     SP={marketSnapshot.SellPrices[i].ToString("F11")} SV={marketSnapshot.SellPrices[i].ToString("F11")}{Environment.NewLine}");
				}
				log.AppendLine($"----- OUT  MS");
				AddMessageToFileLog?.Invoke(log.ToString());
			}
		}

		void OnExceptionHandler(GridExSocketBase socket, Exception exception)
		{
			_marketStreamSocket.OnException -= OnExceptionHandler;
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

		private MarketStreamSocket _marketStreamSocket;
		private MarketSnapshotBuilder _marketSnapshotBuilder;

		private ManualResetEventSlim _enviromentExitWait;
		private ManualResetEventSlim _processesStartedEvent;

		private CancellationTokenSource _cancellationTokenSource;

		private long _countOfDimensions = 0;

		public object Factory { get; private set; }
	}
}
