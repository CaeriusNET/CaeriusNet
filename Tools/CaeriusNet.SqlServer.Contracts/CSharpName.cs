using System.Text;

namespace CaeriusNet.SqlServer.Contracts;

internal static class CSharpName
{
    internal static string FromSqlName(string name)
    {
        var sb = new StringBuilder(name.Length);
        var upperNext = true;
        foreach (var ch in name)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                upperNext = true;
                continue;
            }

            sb.Append(upperNext ? char.ToUpperInvariant(ch) : ch);
            upperNext = false;
        }

        if (sb.Length == 0)
            return "SqlContract";

        if (char.IsDigit(sb[0]))
            sb.Insert(0, '_');

        return sb.ToString();
    }
}
