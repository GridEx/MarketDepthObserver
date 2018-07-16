using System;
using System.IO;
using System.Net;
using System.Xml.Linq;

namespace GridEx.MarketDepthObserver.Config
{
	public class ConnectionConfig : IXml
	{
        public ConnectionConfig(string configFile)
        {
            configFilename = configFile ?? "Config.xml";
            if (!File.Exists(configFile))
            {
                IP = new IPAddress(new byte[] { 127, 0, 0, 1 });
                Port = 1234;
            }
            else
            {
				try
				{
					XDocument config = XDocument.Load(configFile);
					Load(config.Element("Config"));
				}
				catch
				{
					IP = new IPAddress(new byte[] { 127, 0, 0, 1 });
					Port = 1234;
				}
			}
        }

        public void Save()
        {
			try
			{
				GetAsXElement().Save(configFilename ?? "Config.xml");
			}
			catch { }
        }

		public void Load(XElement xElement)
		{
			var iP = new IPAddress(new byte[] { 127, 0, 0, 1 });
			var port = 1234;

			try
			{
				IPAddress.TryParse(xElement?.Attribute("IP")?.Value, out iP);
				int.TryParse(xElement?.Attribute("Port")?.Value, out port);
			}
			catch { }
			finally
			{
				IP = iP;
				Port = port;
			}
		}

		public XElement GetAsXElement()
		{
			return new XElement("Config",
			   new XAttribute("IP", IP.MapToIPv4().ToString()),
			   new XAttribute("Port", Port.ToString()));
		}

		public IPAddress IP { get; set; }
		public int Port { get; set; }

		private readonly string configFilename;
    }
}