using System.Xml.Linq;

namespace GridEx.MarketDepthObserver.Config
{
	interface IXml
	{
		void Load(XElement xElement);

		XElement GetAsXElement();
	}
}
