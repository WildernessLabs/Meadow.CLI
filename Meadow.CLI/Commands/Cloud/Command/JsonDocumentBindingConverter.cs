using CliFx.Exceptions;
using CliFx.Extensibility;
using System.Text.Json;

namespace Meadow.CLI.Commands.Cloud.Command
{
    public class JsonDocumentBindingConverter : BindingConverter<JsonDocument>
    {
        public override JsonDocument Convert(string rawValue)
        {
            try { return JsonDocument.Parse(rawValue); }
            catch (JsonException ex)
            {
                throw new CommandException($"Provided arguments is not valid JSON: {ex.Message}", showHelp: false, innerException: ex);
            }
        }
    }
}
