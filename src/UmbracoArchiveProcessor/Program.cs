﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using HtmlAgilityPack;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;
using RazorEngine;
using RazorEngine.Configuration;
using RazorEngine.Templating;
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

            if (target_dir.Exists == false)
                target_dir.Create();

            var archive_path = Path.Combine(current_dir, "archive");
            var archive_dir = new DirectoryInfo(archive_path);

            if (archive_dir.Exists == false)
                archive_dir.Create();

            if (args.Length == 0)
            {
                Console.WriteLine("Please specify the Umbraco version number.");
                Environment.Exit(2);
            }

            //var latest_version = args.Length > 0 ? args[0] : GetLatestUmbracoVersionNumber();
            var latest_version = args[0];

            if (string.Equals(latest_version, "html", StringComparison.OrdinalIgnoreCase) == true)
            {
                GenerateHtmlPage(target_dir);
                Console.WriteLine("\n\rHTML page re-generated!");
                Environment.Exit(0);
            }

            var previous_version = args.Length > 1 ? args[1] : string.Empty;

            if (string.IsNullOrWhiteSpace(latest_version) == true)
            {
                Console.WriteLine("Unable to get the latest Umbraco version number.");
                Environment.Exit(2);
            }

            var alreadyProcessed = HasUmbracoReleaseAlreadyProcessed(target_path, latest_version);
            if (alreadyProcessed == true && string.IsNullOrWhiteSpace(previous_version) == true)
            {
                Console.WriteLine("Umbraco version has already been processed.");
                Environment.Exit(2);
            }

            var umbraco_filename = string.Format("UmbracoCms.{0}.zip", latest_version);

            DownloadUmbracoReleaseArchive(target_path, umbraco_filename);

            if (alreadyProcessed == false)
            {
                GenerateHashes(target_dir, umbraco_filename);

                UpdateArchiveData(target_dir, umbraco_filename);

                GetUmbracoAssemblyVersions(target_dir, umbraco_filename);
            }

            GenerateDiffPatches(target_path, latest_version, previous_version);

            MoveToVersionDirectory(latest_version, target_dir, umbraco_filename);

            GenerateHtmlPage(target_dir);

            Console.WriteLine("\n\rThank you and goodnight!");
        }

        //static string GetLatestUmbracoVersionNumber()
        //{
        //    var web = new HtmlWeb();

        //    var url = "https://our.umbraco.org/download/";
        //    var doc = web.Load(url);

        //    var link = doc.DocumentNode.SelectSingleNode("//a[@id='downloadButton']");
        //    var text = link.InnerText;

        //    var version_number = text.Replace("Download ", string.Empty);

        //    Console.WriteLine("Latest Umbraco version number: {0}", version_number);

        //    return version_number;
        //}

        static bool HasUmbracoReleaseAlreadyProcessed(string target_path, string latest_version)
        {
            var path = Path.Combine(target_path, "..", "data", "releases.json");
            var model = GetUmbracoArchiveModel(path);

            return model.Releases.Any(x => x.Version == latest_version);
        }

        static void DownloadUmbracoReleaseArchive(string target_path, string umbraco_filename)
        {
            var filepath = Path.Combine(target_path, umbraco_filename);

            if (File.Exists(filepath))
            {
                Console.WriteLine("File already downloaded.");
                return;
            }

            var web = new HtmlWeb();

            using (var client = new WebClient())
            {
                var uri = new Uri(string.Format("https://umbracoreleases.blob.core.windows.net/download/{0}", umbraco_filename));

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
            lock (_object)
            {
                var input = File.Exists(path) ? File.ReadAllText(path) : "{ \"releases\": [ ] }";
                return JsonConvert.DeserializeObject<UmbracoArchiveModel>(input);
            }
        }

        static DateTime GetDateTimeForZip(FileInfo file)
        {
            if (file.Exists == false)
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

                Console.WriteLine("Added '{0}' to the releases.", umbracoVersion);
            }

            model.Releases.Sort((t1, t2) => new Version(t1.Version).CompareTo(new Version(t2.Version)));

            var output = JsonConvert.SerializeObject(model, Formatting.Indented);
            File.WriteAllText(path, output);

            var path1 = path.Replace("releases.json", "..\\archive\\releases.json");
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
                    if (entry.Name.Contains("bin") && entry.Name.EndsWith(".dll") && (entry.Name.Contains("amd64") == false && entry.Name.Contains("x86") == false))
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

        static void GenerateDiffPatches(string target_path, string latest_version, string previous_version)
        {
            var path = Path.Combine(target_path, "..", "data", "releases.json");
            var model = GetUmbracoArchiveModel(path);
            var releases = model.Releases;

            var diffs_dir = Path.Combine(target_path, "..", "archive", "diffs");
            if (Directory.Exists(diffs_dir) == false)
                Directory.CreateDirectory(diffs_dir);

            var builders = new Dictionary<string, IBuilder>()
            {
                { "html", new HtmlBuilder() },
                { "json", new JsonBuilder() },
                { "txt", new TextBuilder() },
                { "xml", new XmlBuilder2() },
                { "zip", new ZipBuilder() }
            };

            var latestIndex = releases.FindIndex(x => x.Version == latest_version);
            if (latestIndex == -1)
                latestIndex = releases.Count - 1;

            var previousIndex = releases.FindIndex(x => x.Version == previous_version);
            if (previousIndex == -1)
                previousIndex = latestIndex - 1;

            var previous = releases[previousIndex];

            if (File.Exists(Path.Combine(target_path, previous.FileName)) == false)
            {
                Console.WriteLine("Could not find '{0}' on disk; downloading.", previous.Version);
                DownloadUmbracoReleaseArchive(target_path, string.Format("UmbracoCms.{0}.zip", previous.Version));
            }

            var current = releases[latestIndex];

            Console.WriteLine("Comparing the differences between: {0} - {1}", previous.Version, current.Version);

            var file1 = new FileInfo(Path.Combine(target_path, previous.FileName));
            var file2 = new FileInfo(Path.Combine(target_path, current.FileName));

            var calc = new DifferenceCalculator(file1, file2)
            {
                CompareCrcValues = true,
                CompareTimestamps = false,
                IgnoreCase = true
            };

            var diffs = calc.GetDifferences();

            foreach (var item in builders)
            {
                var patch_name = string.Format("UmbracoCms.{0}-{1}.{2}", previous.Version, current.Version, item.Key);
                var patch_file = Path.Combine(diffs_dir, patch_name);

                if (File.Exists(patch_file) == false)
                    item.Value.Build(patch_file, diffs);
            }

            Console.WriteLine(diffs);

            File.Delete(Path.Combine(target_path, previous.FileName));

            Console.WriteLine("Finished generating diff patches.");
        }

        static void MoveToVersionDirectory(string latest_version, DirectoryInfo target_dir, string pattern = "UmbracoCms.*.zip", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            var files = target_dir.GetFiles(pattern, searchOption);

            foreach (var file in files)
            {
                var version_path = Path.Combine(target_dir.FullName, "..", "archive", latest_version);
                var version_dir = new DirectoryInfo(version_path);

                if (version_dir.Exists == false)
                    version_dir.Create();

                var associatedFiles = target_dir.GetFiles(file.Name.Replace(".zip", ".*"), searchOption);

                foreach (var associatedFile in associatedFiles)
                {
                    associatedFile.CopyTo(Path.Combine(version_dir.FullName, associatedFile.Name), true);
                }
            }
        }

        static void GenerateHtmlPage(DirectoryInfo target_dir)
        {
            var data_path = Path.Combine(target_dir.FullName, "..", "data");

            var template_path = Path.Combine(data_path, "__template.cshtml");

            if (File.Exists(template_path) == false)
            {
                Console.WriteLine("The template file does not exist.");
                return;
            }

            var path = Path.Combine(data_path, "releases.json");
            var model = GetUmbracoArchiveModel(path);

            var razorTemplate = File.ReadAllText(template_path);

            var config = new TemplateServiceConfiguration { Debug = true, ReferenceResolver = new Template.SystemRuntimeResolver() };
            Engine.Razor = RazorEngineService.Create(config);

            var contents = Engine.Razor.RunCompile(razorTemplate, "UmbracoArchiveTemplate", typeof(UmbracoArchiveModel), model, null);

            var html = Path.Combine(target_dir.FullName, "..", "archive", "index.html");
            File.WriteAllText(html, contents);

            Console.WriteLine("Finished generating HTML archive index.");
        }
    }
}