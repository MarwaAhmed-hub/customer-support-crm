namespace CustomerSupportCrm.Domain.Users;

/// <summary>
/// The single, shared definition of how a user's e-mail address is normalized before it is stored
/// or compared.
/// </summary>
/// <remarks>
/// <para>
/// <b>Later stories must reuse this helper.</b> Story 02 (users-management) applies the identical
/// normalization on create and update, or the unique index on <c>Users.Email</c> will let
/// near-duplicates through.
/// </para>
/// <para>
/// Normalization happens in application code on purpose. SQL Server's default collation
/// (<c>SQL_Latin1_General_CP1_CI_AS</c>) is case-insensitive, so relying on it would silently mask
/// a missing normalization here and then break against a case-sensitive collation, or against a
/// C# comparison using <see cref="StringComparison.Ordinal"/>. No explicit collation is set on the
/// <c>Email</c> column.
/// </para>
/// </remarks>
public static class EmailNormalizer
{
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
