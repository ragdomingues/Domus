namespace Domus.Domain.Services;

public static class TimezoneValidator
{
    public static bool IsValidIana(string? timezone)
    {
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            // On Windows, IANA may need conversion; accept well-known IANA pattern as fallback for Domus.
            return timezone.Contains('/') && !timezone.Contains(' ');
        }
        catch (InvalidTimeZoneException)
        {
            return timezone.Contains('/') && !timezone.Contains(' ');
        }
    }
}
