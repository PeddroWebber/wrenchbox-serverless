namespace WrenchBox.Functions;

public enum AuthDenial
{
    None = 0,
    InvalidCpf = 1,
    NotFound = 2,
    Inactive = 3,
    Blocked = 4
}

public static class AuthDecision
{
    public const int Active = 0;
    public const int Inactive = 1;
    public const int Blocked = 2;

    public static AuthDenial Decide(bool cpfValid, CustomerRecord? customer)
    {
        if (!cpfValid)
            return AuthDenial.InvalidCpf;
        if (customer is null)
            return AuthDenial.NotFound;

        return customer.Status switch
        {
            Active => AuthDenial.None,
            Blocked => AuthDenial.Blocked,
            _ => AuthDenial.Inactive
        };
    }
}

public sealed record CustomerRecord(Guid Id, int Status);
