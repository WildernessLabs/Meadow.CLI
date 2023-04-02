using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Meadow.CLI.Core.CloudServices.Messages
{
    public class UserOrg
    {
        [JsonPropertyName("orgId")]
        public string OrgId { get; set; }
        [JsonPropertyName("orgName")]
        public object OrgName { get; set; }
        [JsonPropertyName("userId")]
        public string UserId { get; set; }
        [JsonPropertyName("role")]
        public string Role { get; set; }
    }

}
