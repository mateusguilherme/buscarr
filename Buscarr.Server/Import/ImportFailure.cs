namespace Buscarr.Server.Import;

public sealed record ImportFailure(string Reason, bool Rejected = false, string? QuarantinePath = null);
