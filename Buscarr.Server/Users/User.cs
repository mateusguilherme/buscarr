namespace Buscarr.Server.Users;

public class User
{
    public int Id { get; init; }
    public required string Email { get; init; }
    public string PasswordHash { get; set; } = "";
    public string? EditionPreference { get; init; }
    public string? QualityPreference { get; init; }
    public bool IsAdmin { get; init; }
}
