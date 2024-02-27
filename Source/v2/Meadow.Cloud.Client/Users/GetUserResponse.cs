namespace Meadow.Cloud.Client.Users;

public class GetUserResponse(string id, string email, string firstName, string lastName, string fullName)
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = id;

    [JsonPropertyName("email")]
    public string Email { get; set; } = email;

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; } = firstName;

    [JsonPropertyName("lastName")]
    public string LastName { get; set; } = lastName;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = fullName;
}
