using System;
using Newtonsoft.Json;

namespace UmbracoArchiveProcessor.Models
{
    public class UmbracoArchiveRelease
    {
        [JsonProperty("version")]
        public string Version { get; set; }

        [JsonProperty("filename")]
        public string FileName { get; set; }

        [JsonProperty("filesize")]
        public long FileSize { get; set; }

        [JsonProperty("date")]
        public DateTime Date { get; set; }

        [JsonProperty("md5hash")]
        public string MD5Hash { get; set; }

        [JsonProperty("notes")]
        public string Notes { get; set; }

        public override string ToString()
        {
            return string.Format("{0} - {1}", this.Version, this.Date);
        }
    }
}