using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Newtonsoft.Json;
using UmbracoArchiveProcessor.Models;
using ZipDiff.Core;
using ZipHash.Core;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip;

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

			var data_path = Path.Combine(current_dir, "data");
			var data_dir = new DirectoryInfo(target_path);
			if (!data_dir.Exists)
				UpdateArchiveData(data_dir);

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

		static void GenerateHashes(DirectoryInfo target_dir, string pattern = "UmbracoCms.*.zip", SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			var files = target_dir.GetFiles(pattern, searchOption);
			foreach (var file in files)
			{
				var gen = new HashGenerator(file.FullName, new HashAlgorithm[] { MD5.Create() });
				gen.GenerateHashes();
			}
		}

		static UmbracoArchiveModel GetUmbracoArchiveModel(DirectoryInfo target_dir, string filename = "releases.json")
		{
			var path = Path.Combine(target_dir.FullName, "releases.json");
			var input = File.Exists(path) ? File.ReadAllText(path) : "{ \"releases\": [ ] }";
			return JsonConvert.DeserializeObject<UmbracoArchiveModel>(input);
		}

		static DateTime GetDateTimeForZip(FileInfo file)
		{
			if (!file.Exists)
				return DateTime.MinValue;

			using (var reader = file.OpenRead())
			{
				var zipFile = new ZipFile(reader);
				var date = zipFile[0].DateTime;
				zipFile.Close();

				return date;
			}
		}

		static void UpdateArchiveData(DirectoryInfo target_dir, string pattern = "UmbracoCms.*.zip", SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			var path = Path.Combine(target_dir.FullName, "releases.json");
			var model = GetUmbracoArchiveModel(target_dir);

			var files = target_dir.GetFiles(pattern, searchOption);
			foreach (var file in files)
			{
				var umbracoVersion = file.Name.Replace("UmbracoCms.", string.Empty).Replace(".zip", string.Empty);

				if (model.Releases.Any(x => x.Version == umbracoVersion))
				{
					Console.WriteLine("Release already added.");
					continue;
				}

				var date = GetDateTimeForZip(file);
				var hash = File.ReadAllText(file.FullName + ".md5");

				var release = new UmbracoArchiveRelease
				{
					Version = umbracoVersion,
					FileName = file.Name,
					FileSize = file.Length,
					Date = date,
					Notes = "",
					MD5Hash = hash
				};

				model.Releases.Add(release);
			}

			model.Releases.Sort((t1, t2) => new Version(t1.Version).CompareTo(new Version(t2.Version)));

			var output = JsonConvert.SerializeObject(model, Formatting.Indented);
			File.WriteAllText(path, output);

			// create stub for latest release version
			var latest = model.Releases.Last();
			var path2 = path.Replace("releases.json", "latest.json");
			var output2 = JsonConvert.SerializeObject(latest, Formatting.Indented);
			File.WriteAllText(path2, output2);

			// latest version number - plain text
			File.WriteAllText(path2.Replace(".json", string.Empty), latest.Version);
		}
	}
}