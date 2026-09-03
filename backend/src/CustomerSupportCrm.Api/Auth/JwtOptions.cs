namespace CustomerSupportCrm.Api.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string SigningKey { get; set; } = default!;   // >= 32 UTF-8 bytes (256-bit, HMAC-SHA256)
    public int AccessTokenMinutes { get; set; } = 60;
}
