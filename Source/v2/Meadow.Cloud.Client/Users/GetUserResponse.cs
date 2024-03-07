namespace Meadow.Cloud.Client.Users;

public class GetUserResponse
{
    public GetUserResponse(string id, string email, string firstName, string lastName, string fullName)
    {
        Id = id;
        Email = email;
        FirstName = firstName;
        LastName = lastName;
        FullName = fullName;
    }

    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("firstName")]
    public string FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string LastName { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; }
}
