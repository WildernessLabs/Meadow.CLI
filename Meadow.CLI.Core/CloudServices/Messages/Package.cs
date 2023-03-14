using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Meadow.CLI.Core.CloudServices.Messages
{
    public class Package
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = default!;
        [JsonPropertyName("packageId")]
        public string PackageId { get; set; } = default!;
        [JsonPropertyName("orgId")]
        public string OrgId { get; set; } = default!;
        [JsonPropertyName("publishedDate")]
        public DateTime? PublishedDate { get; set; } = default!;
        [JsonPropertyName("name")]
        public string Name { get; set; } = default!;
        [JsonPropertyName("description")]
        public string Description { get; set; } = default!;
    }
}
