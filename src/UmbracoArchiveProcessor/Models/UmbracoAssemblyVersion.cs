using Newtonsoft.Json;

namespace UmbracoArchiveProcessor.Models
{
	public class UmbracoAssemblyVersion
	{
		[JsonProperty("assemblyName")]
		public string AssemblyName { get; set; }

		[JsonProperty("assemblyVersion")]
		public string AssemblyVersion { get; set; }

		[JsonProperty("filesize")]
		public long FileSize { get; set; }

		[JsonProperty("md5hash")]
		public string MD5Hash { get; set; }

		[JsonProperty("umbracoVersion")]
		public string UmbracoVersion { get; set; }
	}
}