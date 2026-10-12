namespace Orderly.Domain;

public static class PurchaseOrderReference
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return string.Concat(value.Where(char.IsLetterOrDigit));
    }
}
