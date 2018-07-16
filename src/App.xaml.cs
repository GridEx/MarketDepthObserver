using System.Windows;
using GridEx.MarketDepthObserver.Config;

namespace GridEx.MarketDepthObserver
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public static ConnectionConfig ConnectionConfig;
		public static Options Options;

		static App()
		{
			ConnectionConfig = new ConnectionConfig("config.xml");
			Options = new Options("options.xml");
		}
	}
}
