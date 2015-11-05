﻿using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HtmlAgilityPack;
using ZipDiff.Core;
using ZipHash.Core;

namespace UmbracoArchiveProcessor
{
	class Program
	{
		static readonly object _object = new object();

		static void Main(string[] args)
		{
			var current_dir = Environment.CurrentDirectory;

			var target_path = Path.Combine(current_dir, "artifacts");
			var target_dir = new DirectoryInfo(target_path);

			if (!target_dir.Exists)
				target_dir.Create();

			var umbraco_version = GetLatestUmbracoVersionNumber();

			if (!string.IsNullOrWhiteSpace(umbraco_version))
				DownloadUmbracoReleaseArchive(umbraco_version, target_path);

			GenerateHashes(target_dir);

			// get previous version
		}

		static string GetLatestUmbracoVersionNumber()
		{
			var web = new HtmlWeb();

			var url = "https://our.umbraco.org/download/";
			var doc = web.Load(url);

			var link = doc.DocumentNode.SelectSingleNode("//a[@id='downloadButton']/span[1]");
			var text = link.InnerText;

			var version_number = text.Replace("Download Umbraco v", string.Empty);

			Console.WriteLine("Latest Umbraco version number: {0}", version_number);

			return version_number;
		}

		static void DownloadUmbracoReleaseArchive(string umbraco_version, string target_path)
		{
			var filename = string.Format("UmbracoCms.{0}.zip", umbraco_version);

			if (File.Exists(Path.Combine(target_path, filename)))
			{
				Console.WriteLine("File already downloaded.");
				return;
			}

			var web = new HtmlWeb();

			using (var client = new WebClient())
			{
				var filepath = Path.Combine(target_path, filename);
				var uri = new Uri(string.Format("http://umbracoreleases.blob.core.windows.net/download/UmbracoCms.{0}.zip", umbraco_version));

				//client.DownloadProgressChanged += (s, e) =>
				//{
				//	lock (_object)
				//	{
				//		Console.WriteLine("{0} downloaded {1} of {2} bytes. {3}% complete...",
				//			((TaskCompletionSource<object>)e.UserState).Task.AsyncState,
				//			e.BytesReceived,
				//			e.TotalBytesToReceive,
				//			e.ProgressPercentage);
				//	}
				//};

				var task = client.DownloadFileTaskAsync(uri, filepath);

				task.Wait();
			}

			Console.WriteLine("\n\rDownload complete.");
		}

		private static void GenerateHashes(DirectoryInfo target_dir, string pattern = "UmbracoCms.*.zip", SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			var files = target_dir.GetFiles(pattern, searchOption);
			foreach (var file in files)
			{
				var gen = new HashGenerator(file.FullName, new HashAlgorithm[] { MD5.Create() });
				gen.GenerateHashes();
			}
		}
	}
}