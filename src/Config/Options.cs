using System;
using System.IO;
using System.Xml.Linq;
using MarketDepthClient.Classes;

namespace GridEx.MarketDepthObserver.Config
{
	public class Options : IXml
	{
        public Options(string optionsFile)
        {
			optionsFilename = optionsFile ?? "options.xml";
            if (!File.Exists(optionsFile))
            {
				_logToFile = false;
				_frequency = Frequency.Hz1;
			}
            else
            {
				try
				{
					Load(XDocument.Load(optionsFilename).Element("Options"));
				}
				catch
				{ 
					_logToFile = false;
					_frequency = Frequency.Hz1;
				}
            }
        }

        public void Save()
        {
			try
			{
				GetAsXElement().Save(optionsFilename ?? "options.xml");
			}
			catch { }
        }

		public void Load(XElement xElement)
		{
			var logToFile = false;
			var frequency = Frequency.Hz1;

			try
			{
				bool.TryParse(xElement?.Attribute("LogToFile")?.Value, out logToFile);
				Enum.TryParse<Frequency>(xElement?.Attribute("Frequency")?.Value, out frequency);
			}
			catch { }
			finally
			{
				_logToFile = logToFile;
				_frequency = frequency;
			}
		}

		public XElement GetAsXElement()
		{
			return new XElement("Options",
			   new XAttribute("LogToFile", LogToFile),
			   new XAttribute("Frequency", Frequency.ToString()));
		}

		public bool LogToFile
		{
			get => _logToFile;
			set
			{
				_logToFile = value;
				Save();
			}
		}
		public Frequency Frequency
		{
			get => _frequency;
			set
			{
				_frequency = value;
				Save();
			}
		}

		private readonly string optionsFilename;
		private bool _logToFile;
		private Frequency _frequency;
	}
}