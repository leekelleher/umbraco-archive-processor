using System.Collections.Generic;
using Newtonsoft.Json;

namespace UmbracoArchiveProcessor.Models
{
    public class UmbracoArchiveModel
    {
        [JsonProperty("releases")]
        public List<UmbracoArchiveRelease> Releases { get; set; }
    }
}