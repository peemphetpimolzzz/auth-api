using AuthApi.Api.Auth;
using Xunit;

namespace AuthApi.UnitTests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Hash_is_not_the_plaintext()
    {
        var hash = _hasher.Hash("P@ssw0rd123");
        Assert.NotEqual("P@ssw0rd123", hash);
    }

    [Fact]
    public void Verify_succeeds_for_the_correct_password()
    {
        var hash = _hasher.Hash("P@ssw0rd123");
        Assert.True(_hasher.Verify("P@ssw0rd123", hash));
    }

    [Fact]
    public void Verify_fails_for_a_wrong_password()
    {
        var hash = _hasher.Hash("P@ssw0rd123");
        Assert.False(_hasher.Verify("wrong-password", hash));
    }

    [Fact]
    public void Two_hashes_of_the_same_password_differ()
    {
        Assert.NotEqual(_hasher.Hash("same"), _hasher.Hash("same"));
    }
}
