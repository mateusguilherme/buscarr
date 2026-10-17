namespace Buscarr.Server.Users;

public class User
{
    public int Id { get; set; }
    public required string Email { get; set; }
    public required string Name { get; set; }
    public string PasswordHash { get; set; } = "";
    public string? EditionPreference { get; set; }
}
