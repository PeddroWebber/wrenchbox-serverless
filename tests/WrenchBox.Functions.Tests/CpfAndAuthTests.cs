using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;
using WrenchBox.Functions;

namespace WrenchBox.Functions.Tests;

public class CpfAndAuthTests
{
    [Theory]
    [InlineData("39053344705")]
    [InlineData("390.533.447-05")]
    public void Cpf_KnownValidVector_IsAccepted(string raw) =>
        Cpf.IsValid(raw).Should().BeTrue();

    [Theory]
    [InlineData("11111111111")]
    [InlineData("39053344700")]
    [InlineData("123")]
    [InlineData("")]
    public void Cpf_Invalid_IsRejected(string raw) =>
        Cpf.IsValid(raw).Should().BeFalse();

    [Fact]
    public void AuthDecision_ActiveCustomer_IsAllowed()
    {
        var denial = AuthDecision.Decide(true, new CustomerRecord(Guid.NewGuid(), AuthDecision.Active));
        denial.Should().Be(AuthDenial.None);
    }

    [Fact]
    public void AuthDecision_InvalidCpf_DoesNotLookLikeAMissingCustomer() =>
        AuthDecision.Decide(false, null).Should().Be(AuthDenial.InvalidCpf);

    [Fact]
    public void AuthDecision_UnknownCustomer_IsNotFound() =>
        AuthDecision.Decide(true, null).Should().Be(AuthDenial.NotFound);

    [Theory]
    [InlineData(AuthDecision.Inactive, AuthDenial.Inactive)]
    [InlineData(AuthDecision.Blocked, AuthDenial.Blocked)]
    public void AuthDecision_StatusBlocksToken(int status, AuthDenial expected) =>
        AuthDecision.Decide(true, new CustomerRecord(Guid.NewGuid(), status)).Should().Be(expected);

    [Fact]
    public void Token_RoundTrip_CarriesCustomerIdAndRole_NotTheCpf()
    {
        var issuer = new TokenIssuer(new MemoryConfiguration(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = "WrenchBox_Dev_Secret_Key_Min_32_Chars!",
            ["Jwt:Issuer"] = "WrenchBox",
            ["Jwt:Audience"] = "WrenchBox.Admin",
            ["Jwt:CustomerExpiryMinutes"] = "30"
        }));

        var customerId = Guid.NewGuid();
        var (token, expires) = issuer.Issue(customerId);

        token.Should().NotContain("39053344705");
        expires.Should().BeAfter(DateTime.UtcNow);
        issuer.TryValidate("Bearer " + token, "Customer").Should().BeTrue();
        issuer.TryValidate("Bearer " + token, "Admin").Should().BeFalse();
    }
}

file sealed class MemoryConfiguration : IConfiguration
{
    private readonly Dictionary<string, string?> _data;

    public MemoryConfiguration(Dictionary<string, string?> data) => _data = data;

    public string? this[string key]
    {
        get => _data.TryGetValue(key, out var value) ? value : null;
        set => _data[key] = value;
    }

    public IEnumerable<IConfigurationSection> GetChildren() => [];

    public IChangeToken GetReloadToken() => NullChangeToken.Instance;

    public IConfigurationSection GetSection(string key) => new EmptySection(key);

    private sealed class EmptySection : IConfigurationSection
    {
        public EmptySection(string key) => Key = key;
        public string? this[string key] { get => null; set { } }
        public string Key { get; }
        public string Path => Key;
        public string? Value { get; set; }
        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => NullChangeToken.Instance;
        public IConfigurationSection GetSection(string key) => new EmptySection(key);
    }

    private sealed class NullChangeToken : IChangeToken
    {
        public static readonly NullChangeToken Instance = new();
        public bool HasChanged => false;
        public bool ActiveChangeCallbacks => false;
        public IDisposable RegisterChangeCallback(Action<object?> callback, object? state) => new Noop();
        private sealed class Noop : IDisposable { public void Dispose() { } }
    }
}
