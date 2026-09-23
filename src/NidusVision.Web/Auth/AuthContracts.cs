namespace NidusVision.Web.Auth;

public sealed record PasswordRequest(string Password);

public sealed record AuthStatusResponse(bool Configured, bool Authenticated);
