using FluentAssertions;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Execution;

namespace QueryPlus.Core.Tests;

public class CredentialResolverTests
{
    private static DistributionList List(ConnectionCredentials defaults) => new()
    {
        Auth = defaults.Auth,
        SqlUser = defaults.SqlUser,
        SqlPassword = defaults.SqlPassword,
        Domain = defaults.Domain,
        Encryption = defaults.Encryption,
        TrustServerCertificate = defaults.TrustServerCertificate,
        ConnectTimeoutSec = defaults.ConnectTimeoutSec,
        CommandTimeoutSec = defaults.CommandTimeoutSec
    };

    [Fact]
    public void Uses_list_default_when_target_has_no_override()
    {
        var list = List(new ConnectionCredentials { Auth = AuthMode.Sql, SqlUser = "listuser", SqlPassword = "p" });
        var target = new Target("S", "DB");

        CredentialResolver.Resolve(list, target).SqlUser.Should().Be("listuser");
    }

    [Fact]
    public void Uses_override_when_it_has_everything_it_needs()
    {
        var list = List(new ConnectionCredentials { Auth = AuthMode.Sql, SqlUser = "listuser", SqlPassword = "lp" });
        var target = new Target("S", "DB")
        {
            Credentials = new ConnectionCredentials { Auth = AuthMode.Sql, SqlUser = "ovr", SqlPassword = "op" }
        };

        CredentialResolver.Resolve(list, target).SqlUser.Should().Be("ovr");
    }

    [Fact]
    public void Override_missing_its_password_falls_back_to_the_list()
    {
        // The reported bug: a leftover WindowsCredentials override with no saved password.
        var list = List(new ConnectionCredentials
        {
            Auth = AuthMode.WindowsCredentials, Domain = "CORP", SqlUser = "svc", SqlPassword = "secret"
        });
        var target = new Target("S", "DB")
        {
            Credentials = new ConnectionCredentials
            {
                Auth = AuthMode.WindowsCredentials, Domain = "CORP", SqlUser = "svc", SqlPassword = ""
            }
        };

        var resolved = CredentialResolver.Resolve(list, target);
        resolved.SqlPassword.Should().Be("secret"); // came from the list
    }

    [Fact]
    public void Windows_override_without_password_is_kept_as_is()
    {
        // Plain Windows auth needs no password, so it is not treated as "missing" one.
        var list = List(new ConnectionCredentials { Auth = AuthMode.Sql, SqlUser = "listuser", SqlPassword = "lp" });
        var target = new Target("S", "DB")
        {
            Credentials = new ConnectionCredentials { Auth = AuthMode.Windows }
        };

        CredentialResolver.Resolve(list, target).Auth.Should().Be(AuthMode.Windows);
    }
}
