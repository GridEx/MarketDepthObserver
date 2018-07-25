using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using GridEx.API.MarketStream;
using GridEx.MarketDepthObserver.Classes;
using GridEx.MarketDepthObserver.Config;
using MarketDepthClient.Classes;

namespace GridEx.MarketDepthObserver
{
	public partial class MainWindow : Window
	{
		public MainWindow()
		{
			logHistory = new List<string>(logHistoryStringsMaximum);

			InitializeComponent();

			Loaded += MainWindow_Loaded;
		}

		public Frequency Frequency
		{
			get => App.Options.Frequency;
			set => App.Options.Frequency = value;
		}

		public bool LogToFile
		{
			get => App.Options.LogToFile;
			set
			{
				App.Options.LogToFile = value;
				if (value)
				{
					ActivateLoggingToFile();
				}
				else
				{
					DiactivateLoggingToFile();
				}
			}
		}

		private void MainWindow_Loaded(object sender, RoutedEventArgs e)
		{
			Loaded -= MainWindow_Loaded;
			//_askScrollViewer = GetScrollViewer(askListView);
			//_bidScrollViewer = GetScrollViewer(bidListView);
			//_askScrollViewer.ScrollChanged += (sender1, e1) => _bidScrollViewer.ScrollToVerticalOffset(e1.VerticalOffset);
			//_bidScrollViewer.ScrollChanged += (sender2, e2) => _askScrollViewer.ScrollToVerticalOffset(e2.VerticalOffset);
		}

		private void MenuItem_Click(object sender, RoutedEventArgs e)
		{
			IPWindow iPWindow = new IPWindow(ref App.ConnectionConfig) { Owner = this };
			iPWindow.ShowDialog();
		}

		private void ConnectToMarketButton_Unchecked(object sender, RoutedEventArgs e)
		{
			FinishWatchMarket();

			ConnectToMarketButton.IsEnabled = false;
			ConnectToMarketButton.Header = "Disconnecting from server";

			Dispatcher.BeginInvoke(new Action(() =>
			{
				_processesStartedEvent.Reset();

				_enviromentExitWait.Wait(5000);

				if (!_enviromentExitWait.IsSet)
				{
					FinishWatchMarket();
					StopClient();
					_enviromentExitWait.Set();
				}

				ConnectToMarketButton.Header = "Disconnected from server (press to connect)";
				ConnectToMarketButton.IsEnabled = true;
			}), DispatcherPriority.Background);
		}

		private void ConnectToMarketButton_Checked(object sender, RoutedEventArgs e)
		{
			_stop = false;
			ConnectToMarketButton.IsEnabled = false;
			ConnectToMarketButton.Header = "Connecting to server";
			ConnectToMarketButton.ToolTip = "Trying to connect";
			Dispatcher.Invoke(new Action(() =>
			{
				CreateDataThread();
				CreateDataCollectionThread();
			}), DispatcherPriority.Background);
		}

		private void AddMessageToLog(string message)
		{
			Dispatcher.BeginInvoke(
				new Action(() =>
				{
					var text = $"{DateTime.Now.ToString("hh:mm:ss.fff")}: {message}";
					logHistory.Add(text);

					if (logHistory.Count > logHistoryStringsMaximum)
					{
						logHistory.RemoveRange(0, logHistoryStrings);
						log.Text = logHistory.Aggregate(string.Empty, (res, str) => $"{res}{str}{Environment.NewLine}").ToString();
					}
					else
					{
						log.Text += $"{text}{Environment.NewLine}";
					}

					logHistoruContainer.IsExpanded = true;
				}));
		}

		private void OnException(MarketClient client, Exception exception)
		{
			AddMessageToLog(exception.Message);
			Dispatcher.BeginInvoke(
				new Action(() =>
				{
					ConnectToMarketButton.IsChecked = false;
				}));
		}

		private void OnDisconnected(MarketClient client)
		{
			AddMessageToLog("Disconnected from server");
		}

		private void OnError(MarketClient client, SocketError socketError)
		{
			AddMessageToLog(socketError.ToString());
			Dispatcher.BeginInvoke(
				new Action(() =>
				{
					ConnectToMarketButton.IsChecked = false;
				}));
		}

		private void OnConnected(MarketClient client)
		{
			AddMessageToLog("Connected to server");
		}

		private void AddMessageToFileLog(string message)
		{
			lock (_fileStreamLocker)
			{
				if (_fileStream != null && _fileStream.CanWrite)
				{
					var bytes = Encoding.UTF8.GetBytes(message);
					try
					{
						_fileStream.Write(bytes, 0, bytes.Length);
					}
					catch(Exception ex)
					{
						AddMessageToLog(ex.Message);
						DiactivateLoggingToFile();
					}

				}
			}
		}

		private void OnMarketChangeAction(string message)
		{
			AddMessageToFileLog(message);
		}

		private void StopClient()
		{
			_stop = true;

			DiactivateLoggingToFile();

			if (marketClient != null)
			{
				marketClient.OnConnected -= OnConnected;
				marketClient.OnDisconnected -= OnDisconnected;
				marketClient.OnError -= OnError;
				marketClient.OnException -= OnException;
				marketClient.Dispose();
			}
		}

		private void FinishWatchMarket()
		{
			_stop = true;
		}

		private void CollectData()
		{
			var pauseEvent = new ManualResetEventSlim();
			pauseEvent.Wait(5000);

			if (marketClient == null || !marketClient.IsConnected)
			{
				Dispatcher.Invoke(new Action(() =>
				{
					StopClient();
					FinishWatchMarket();
					ConnectToMarketButton.IsChecked = false;

					MessageBox.Show(this,
						"Couldn't connect to server.\nPlease check destination IP and your network connection.",
						"Connection problem!",
						MessageBoxButton.OK,
						MessageBoxImage.Warning);
					ConnectToMarketButton.IsChecked = false;
				}));
			}
			else
			{
				try
				{
					Dispatcher.Invoke(new Action(() =>
					{
						ConnectToMarketButton.Header = "Connected to market (press to disconnect)";
						ConnectToMarketButton.ToolTip = "Press to disconnect from Market Depth Server";
						ConnectToMarketButton.IsEnabled = true;
					}));
				}
				catch { }

				pauseEvent.Reset();
				int elapsedMiliseconds = 0;
				MarketClient client = null;
				var buyArray = new PriceVolumePair[MarketSnapshot.Depth];
				var sellArray = new PriceVolumePair[MarketSnapshot.Depth];

				var asks = new MarketValueVisual[MarketSnapshot.Depth];
				var bids = new MarketValueVisual[MarketSnapshot.Depth];

				while (!_stop)
				{
					client = marketClient;
					if (client != null && client.IsConnected)
					{
						client.GetSnapshot(
							buyArray, 
							out int buyAmount, 
							sellArray,
							out int sellAmount);

						MarketSnapshotVisual.FillVisualPrices(
							buyArray, 
							buyAmount, 
							sellArray, 
							sellAmount,
							asks,
							bids);

						Dispatcher.BeginInvoke(new Action(() =>
						{
							bidListView.ItemsSource = bids.Take(buyAmount);
							askListView.ItemsSource = asks.Take(sellAmount);

#if DEBUG
							if (buyAmount > 0 && sellAmount > 0)
							{
								Debug.Assert(bids.Max(e => e.Price) <= asks.Min(e => e.Price));
							}
#endif

							if (elapsedMiliseconds >= 1000)
							{
								eventsTextBlock.Text = $"Events: {client.ReadAndResetCountOfDimensions()}";
								elapsedMiliseconds = 0;
							}
						}), DispatcherPriority.Normal);
					}
					pauseEvent.Reset();
					int frequencyInt = (int)Frequency;
					pauseEvent.Wait(frequencyInt);
					elapsedMiliseconds += frequencyInt;
				}

				StopClient();
			}
			GC.Collect(2, GCCollectionMode.Forced);
		}

		private void CreateDataCollectionThread()
		{
			_dataCollectionThread = new Thread(new ThreadStart(CollectData));
			_dataCollectionThread.SetApartmentState(ApartmentState.STA);
			_dataCollectionThread.Start();
		}

		private void CreateDataThread()
		{
			_dataThread = new Thread(new ThreadStart(DataThread))
			{
				Priority = ThreadPriority.Highest,
				IsBackground = true
			};
			_dataThread.SetApartmentState(ApartmentState.MTA);
			_dataThread.Start();
		}

		private void DataThread()
		{
			_enviromentExitWait.Reset();

			_cancellationTokenSource = new CancellationTokenSource();
			_processesStartedEvent = new ManualResetEventSlim();

			marketClient = new MarketClient(_enviromentExitWait, _processesStartedEvent);

			marketClient.OnConnected += OnConnected;
			marketClient.OnDisconnected += OnDisconnected;
			marketClient.OnError += OnError;
			marketClient.OnException += OnException;

			if (LogToFile)
			{
				ActivateLoggingToFile();
			}

			marketClient.Run(App.ConnectionConfig.IP.MapToIPv4(), App.ConnectionConfig.Port, ref _cancellationTokenSource, ref _enviromentExitWait);
		}

		private void Window_Closed(object sender, EventArgs e)
		{
			FinishWatchMarket();
			_enviromentExitWait.Wait(5000);

			if (!_enviromentExitWait.IsSet && marketClient != null)
				StopClient();

			App.ConnectionConfig.Save();
		}

		private void ActivateLoggingToFile()
		{
			if (marketClient != null)
			{
				if (_fileStream == null)
				{
					var directory = "Logs";
					if (!Directory.Exists(directory))
					{
						Directory.CreateDirectory(directory);
					}
					_fileStream = File.Open($"{directory}\\{DateTime.Now.ToString("dd-MM-yyy hh-mm-ss.fff")}.log.txt", FileMode.Create);
				}

				marketClient.AddMessageToFileLog += AddMessageToFileLog;
			}
		}

		private void DiactivateLoggingToFile()
		{
			lock (_fileStreamLocker)
			{
				if (_fileStream != null)
				{
					_fileStream.Dispose();
					_fileStream = null;
				}

				if (marketClient != null)
				{
					marketClient.AddMessageToFileLog -= AddMessageToFileLog;
				}
			}
		}

		//private ScrollViewer GetScrollViewer(DependencyObject element)
		//{
		//	if (element is ScrollViewer)
		//	{
		//		return (ScrollViewer)element;
		//	}

		//	for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
		//	{
		//		var child = VisualTreeHelper.GetChild(element, i);

		//		var result = GetScrollViewer(child);
		//		if (result == null)
		//		{
		//			continue;
		//		}
		//		else
		//		{
		//			return result;
		//		}
		//	}

		//	return null;
		//}

		private Thread _dataCollectionThread;
		private Thread _dataThread;
		private MarketClient marketClient;
		private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
		private ManualResetEventSlim _enviromentExitWait = new ManualResetEventSlim(true);
		private ManualResetEventSlim _processesStartedEvent = new ManualResetEventSlim();
		//private ScrollViewer _askScrollViewer;
		//private ScrollViewer _bidScrollViewer;
		private bool _stop;
		private ushort logHistoryStrings = 50;
		private ushort logHistoryStringsMaximum = 100;
		private List<string> logHistory;
		private FileStream _fileStream;
		private object _fileStreamLocker = new object();
	}
}
