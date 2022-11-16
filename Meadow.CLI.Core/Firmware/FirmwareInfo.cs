using System;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meadow.CLI.Core
{
    public class BuildDateConverter : JsonConverter<DateTime>
    {
        // build date is in the format "2022-09-01 09:47:26"
        private const string FormatString = "yyyy-MM-dd HH:mm:ss";

        public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            Debug.Assert(typeToConvert == typeof(DateTime));

            if (!reader.TryGetDateTime(out DateTime value))
            {
                value = DateTime.ParseExact(reader.GetString(), FormatString, CultureInfo.InvariantCulture);
            }

            return value;
        }

        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString(FormatString));
        }
    }

    public class FirmwareInfo
    {
        public string Version { get; set; } = string.Empty;
        [JsonPropertyName("build-date")]
        public DateTime BuildDate { get; set; }
        [JsonPropertyName("build-hash")]
        public string BuildHash { get; set; } = string.Empty;
        public bool IsLatest { get; set; }

        public override bool Equals(object obj)
        {
            var other = obj as FirmwareInfo;
            if (other == null) return false;
            return BuildHash.Equals(other.BuildHash);
        }

        public override int GetHashCode()
        {
            return BuildHash.GetHashCode();
        }
    }
}
