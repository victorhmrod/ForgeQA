using System.Text;

namespace ForgeQA.Domain.Common;

public static class SlugGenerator
{
    public static string Generate(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input is required to generate a slug.", nameof(input));

        var normalized = input.Trim().ToLowerInvariant();
        var builder = new StringBuilder();
        var lastWasHyphen = false;

        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen && builder.Length > 0)
            {
                builder.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrEmpty(slug) ? "n-a" : slug;
    }
}
