using CliFx.Exceptions;
using CliFx.Extensibility;
using System.Text.Json;

namespace Meadow.CLI.Commands.DeviceManagement;

public class JsonDocumentBindingConverter : BindingConverter<JsonDocument>
{
    private const string InvalidArg = "Provided argument is not valid JSON:";

    public override JsonDocument Convert(string? rawValue)
    {
        try
        {
            if (rawValue != null)
                return JsonDocument.Parse(rawValue);
            else
                throw new CommandException($"{InvalidArg}");
        }
        catch (JsonException ex)
        {
            throw new CommandException($"{InvalidArg} {ex.Message}", showHelp: false, innerException: ex);
        }
    }
}
