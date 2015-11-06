using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using HtmlAgilityPack;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using UmbracoArchiveProcessor.Models;
using ZipDiff.Core;
using ZipDiff.Core.Output;
using ZipHash.Core;

namespace UmbracoArchiveProcessor
{
	class Program
	{
		static readonly object _object = new object();

		static void Main(string[] args)
		{
			var current_dir = Environment.CurrentDirectory;

			var target_path = Path.Combine(current_dir, "_tmp");
			var target_dir = new DirectoryInfo(target_path);

			if (!target_dir.Exists)
				target_dir.Create();

			var artifacts_path = Path.Combine(current_dir, "artifacts");
			var artifacts_dir = new DirectoryInfo(artifacts_path);

			if (!artifacts_dir.Exists)
				artifacts_dir.Create();

			var umbraco_version = GetLatestUmbracoVersionNumber();

			if (!string.IsNullOrWhiteSpace(umbraco_version))
				DownloadUmbracoReleaseArchive(umbraco_version, target_path);

			GenerateHashes(target_dir);

			UpdateArchiveData(target_dir);

			GetUmbracoAssemblyVersions(target_dir);

			GenerateDiffPatches(target_dir);

			MoveToVersionDirectory(umbraco_version, target_dir);

			Console.WriteLine("\n\rThank you and goodnight!");
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

				client.DownloadFile(uri, filepath);
			}

			Console.WriteLine("Download complete.");
		}

		static void GenerateHashes(DirectoryInfo target_dir, string pattern = "UmbracoCms.*.zip", SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			var files = target_dir.GetFiles(pattern, searchOption);
			foreach (var file in files)
			{
				var gen = new HashGenerator(file.FullName, new HashAlgorithm[] { MD5.Create() });
				gen.GenerateHashes();

				Console.WriteLine("Generated hash for: {0}", file.Name);
			}
		}

		static UmbracoArchiveModel GetUmbracoArchiveModel(string path)
		{
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
			var path = Path.Combine(target_dir.FullName, "..", "data", "releases.json");
			var model = GetUmbracoArchiveModel(path);

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

			var path1 = path.Replace("releases.json", "..\\artifacts\\releases.json");
			File.WriteAllText(path1, output);

			// create stub for latest release version
			var latest = model.Releases.Last();
			var path2 = path1.Replace("releases.json", "latest.json");
			var output2 = JsonConvert.SerializeObject(latest, Formatting.Indented);
			File.WriteAllText(path2, output2);

			// latest version number - plain text
			File.WriteAllText(path2.Replace(".json", string.Empty), latest.Version);
		}

		static string ConvertHash(byte[] hash)
		{
			return BitConverter
				.ToString(hash)
				.Replace("-", string.Empty)
				.ToLower();
		}

		static byte[] ReadFully(Stream input)
		{
			byte[] buffer = new byte[16 * 1024];
			using (MemoryStream ms = new MemoryStream())
			{
				int read;
				while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
				{
					ms.Write(buffer, 0, read);
				}
				return ms.ToArray();
			}
		}

		static void GetUmbracoAssemblyVersions(DirectoryInfo target_dir, string pattern = "UmbracoCms.*.zip", SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			var files = target_dir.GetFiles(pattern, searchOption);
			foreach (var file in files)
			{
				var zipFile = new ZipFile(file.OpenRead());
				var md5 = MD5.Create();
				var list = new List<UmbracoAssemblyVersion>();

				foreach (var entry in zipFile.Cast<ZipEntry>())
				{
					if (entry.Name.Contains("bin") && entry.Name.EndsWith(".dll") && (!entry.Name.Contains("amd64") && !entry.Name.Contains("x86")))
					{
						var fileName = Path.GetFileName(entry.Name);

						using (var stream = zipFile.GetInputStream(entry))
						{
							var bytes = ReadFully(stream);
							var hash = ConvertHash(md5.ComputeHash(bytes));

							try
							{
								var assembly = Assembly.Load(bytes);
								var assemblyName = assembly.GetName();
								var umbracoVersion = file.Name.Replace("UmbracoCms.", string.Empty).Replace(".zip", string.Empty);

								list.Add(new UmbracoAssemblyVersion
								{
									AssemblyName = assemblyName.Name,
									AssemblyVersion = assemblyName.Version.ToString(),
									FileSize = bytes.Length,
									MD5Hash = hash,
									UmbracoVersion = umbracoVersion
								});
							}
							catch (Exception)
							{
								Console.WriteLine("---- Unable to load '{0}'", fileName);
							}
						}
					}
				}

				zipFile.Close();

				var json = JsonConvert.SerializeObject(list, Formatting.Indented);
				File.WriteAllText(file.FullName.Replace(".zip", ".AssemblyVersions.json"), json);
			}
		}

		private static void GenerateDiffPatches(DirectoryInfo target_dir)
		{
			var path = Path.Combine(target_dir.FullName, "..", "data", "releases.json");
			var model = GetUmbracoArchiveModel(path);
			var releases = model.Releases;

			var patch_dir = Path.Combine(target_dir.FullName, "artifacts", "patches");
			if (!Directory.Exists(patch_dir))
				Directory.CreateDirectory(patch_dir);

			var builders = new Dictionary<string, IBuilder>()
			{
				{ "html", new HtmlBuilder() },
				{ "xml", new XmlBuilder2() },
				{ "txt", new TextBuilder() },
				{ "zip", new ZipBuilder() }
			};

			var a = releases[releases.Count - 2];

			if (File.Exists(Path.Combine(target_dir.FullName, a.FileName)))
				DownloadUmbracoReleaseArchive(a.Version, target_dir.FullName);

			var b = releases[releases.Count - 1];

			Console.WriteLine("{0} - {1}", a.Version, b.Version);

			var file1 = new FileInfo(Path.Combine(target_dir.FullName, a.FileName));
			var file2 = new FileInfo(Path.Combine(target_dir.FullName, b.FileName));

			var calc = new DifferenceCalculator(file1, file2)
			{
				CompareCrcValues = true,
				CompareTimestamps = false,
				IgnoreCase = true
			};

			var diffs = calc.GetDifferences();

			foreach (var item in builders)
			{
				var patch_name = string.Format("UmbracoCms.{0}-{1}.{2}", a.Version, b.Version, item.Key);
				var patch_file = Path.Combine(patch_dir, patch_name);

				if (!File.Exists(patch_file))
					item.Value.Build(patch_file, diffs);
			}

			Console.WriteLine(diffs);

			Console.WriteLine("Finished generating diff patches.");
		}

		static void MoveToVersionDirectory(string umbraco_version, DirectoryInfo target_dir, string pattern = "UmbracoCms.*.zip", SearchOption searchOption = SearchOption.TopDirectoryOnly)
		{
			var files = target_dir.GetFiles(pattern, searchOption);

			foreach (var file in files)
			{
				var version_path = Path.Combine(target_dir.FullName, "..", "artifacts", umbraco_version);
				var version_dir = new DirectoryInfo(version_path);

				if (!version_dir.Exists)
					version_dir.Create();

				var associatedFiles = target_dir.GetFiles(file.Name.Replace(".zip", ".*"), searchOption);

				foreach (var associatedFile in associatedFiles)
				{
					associatedFile.MoveTo(Path.Combine(version_dir.FullName, associatedFile.Name));
				}
			}
		}
	}
}