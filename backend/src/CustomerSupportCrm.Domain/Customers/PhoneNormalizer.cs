namespace CustomerSupportCrm.Domain.Customers;

/// <summary>
/// Story 20 (correction): a pragmatic canonical form for phone-number lookup/dedup. Strips everything
/// but digits, then reconciles the two equivalent shapes a customer or channel provider commonly
/// sends: the local trunk-prefix form ("01234500001") and the full country-code form
/// ("201234500001" / "+201234500001" / "0020 1234500001") — both normalize to
/// <c>"+" + DefaultCountryCode + subscriberNumber</c>, so the same phone number always resolves to the
/// same <see cref="Customer"/> regardless of which format a given message happened to carry.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DefaultCountryCode"/> is a hard-coded assumption (Egypt, "20"), not configurable — this
/// story has no config plumbing (see its scope note) and every phone number seen in this project so
/// far is Egyptian. A local-format number from a <i>different</i> country would be normalized
/// incorrectly under this assumption; that is an accepted limitation until a real per-tenant/country
/// setting exists, not something this class tries to detect.
/// </para>
/// <para>
/// <see cref="Customer.Phone"/> itself is stored exactly as typed (Story 07's convention — "trimmed
/// only, no format transformation") for every entry path except this one, so an existing customer
/// whose phone was hand-typed in a form this method doesn't reconcile (e.g. with extra digits, a
/// missing digit, or a different country's local format) may still not match. Known, accepted
/// limitation — same shape as <see cref="Customer.Email"/> not being unique.
/// </para>
/// </remarks>
public static class PhoneNormalizer
{
    private const string DefaultCountryCode = "20";

    public static string Normalize(string phone)
    {
        var digits = new string(phone.Where(char.IsDigit).ToArray());

        if (digits.Length == 0)
        {
            return string.Empty;
        }

        if (digits.StartsWith("00", StringComparison.Ordinal))
        {
            // International dialling prefix ("00...") already carries a country code, same as "+...".
            digits = digits[2..];
        }
        else if (digits.StartsWith("0", StringComparison.Ordinal) && digits.Length > 1)
        {
            // Local trunk prefix ("0...") — swap it for the assumed country code so this matches the
            // same number's full international form.
            digits = DefaultCountryCode + digits[1..];
        }

        return "+" + digits;
    }
}
