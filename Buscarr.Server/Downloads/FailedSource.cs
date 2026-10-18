namespace Buscarr.Server.Downloads;

public class FailedSource
{
    public int Id { get; init; }
    public int RequestId { get; init; }
    public required string Username { get; init; }
    public required string Directory { get; init; }
}
