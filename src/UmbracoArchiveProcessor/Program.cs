using System;
using System.IO;
using HtmlAgilityPack;

namespace UmbracoArchiveProcessor
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine(Environment.CurrentDirectory);


			var umbraco_version = DiscoverLatestRelease();
			
			Console.WriteLine(umbraco_version);
		}

		static string DiscoverLatestRelease()
		{
			var web = new HtmlWeb();

			var url = "https://our.umbraco.org/download/";
			var doc = web.Load(url);

			var link = doc.DocumentNode.SelectSingleNode("//a[@id='downloadButton']/span[1]");
			var text = link.InnerText;

			return text.Replace("Download Umbraco v", string.Empty);
		}
	}
}