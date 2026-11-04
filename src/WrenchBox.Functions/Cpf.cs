using System.Text.RegularExpressions;

namespace WrenchBox.Functions;

public static class Cpf
{
    public static string OnlyDigits(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? string.Empty : Regex.Replace(raw, @"\D", string.Empty);

    public static bool IsValid(string? raw)
    {
        var cpf = OnlyDigits(raw);
        if (cpf.Length != 11 || cpf.Distinct().Count() == 1)
            return false;

        var sum = 0;
        for (var i = 0; i < 9; i++)
            sum += (cpf[i] - '0') * (10 - i);

        var remainder = sum % 11;
        var digit1 = remainder < 2 ? 0 : 11 - remainder;
        if (cpf[9] - '0' != digit1)
            return false;

        sum = 0;
        for (var i = 0; i < 10; i++)
            sum += (cpf[i] - '0') * (11 - i);

        remainder = sum % 11;
        var digit2 = remainder < 2 ? 0 : 11 - remainder;
        return cpf[10] - '0' == digit2;
    }
}
