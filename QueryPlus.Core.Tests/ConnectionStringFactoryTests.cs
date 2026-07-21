using FluentAssertions;
using Microsoft.Data.SqlClient;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Execution;

namespace QueryPlus.Core.Tests;

public class ConnectionStringFactoryTests
{
    private static SqlConnectionStringBuilder Build(ConnectionCredentials creds) =>
        new(ConnectionStringFactory.Build(creds, "srv", "db"));

    [Fact]
    public void Sets_server_database_and_application_name()
    {
        var b = Build(new ConnectionCredentials());
        b.DataSource.Should().Be("srv");
        b.InitialCatalog.Should().Be("db");
        b.ApplicationName.Should().Be("QueryPlus");
    }

    [Fact]
    public void Sql_auth_sets_user_and_password_not_integrated()
    {
        var b = Build(new ConnectionCredentials { Auth = AuthMode.Sql, SqlUser = "foo", SqlPassword = "bar" });
        b.IntegratedSecurity.Should().BeFalse();
        b.UserID.Should().Be("foo");
        b.Password.Should().Be("bar");
    }

    [Fact]
    public void Windows_auth_uses_integrated_security()
    {
        Build(new ConnectionCredentials { Auth = AuthMode.Windows }).IntegratedSecurity.Should().BeTrue();
    }

    [Fact]
    public void WindowsCredentials_uses_integrated_security_credentials_supplied_by_impersonation()
    {
        var b = Build(new ConnectionCredentials { Auth = AuthMode.WindowsCredentials, SqlUser = "u", Domain = "D", SqlPassword = "p" });
        b.IntegratedSecurity.Should().BeTrue();
        b.ShouldBeEquivalentToNoCredentialsInString();
    }

    [Theory]
    [InlineData(EncryptMode.Optional)]
    [InlineData(EncryptMode.Mandatory)]
    [InlineData(EncryptMode.Strict)]
    public void Maps_encryption_mode(EncryptMode mode)
    {
        var expected = mode switch
        {
            EncryptMode.Optional => SqlConnectionEncryptOption.Optional,
            EncryptMode.Strict => SqlConnectionEncryptOption.Strict,
            _ => SqlConnectionEncryptOption.Mandatory
        };
        Build(new ConnectionCredentials { Encryption = mode }).Encrypt.Should().Be(expected);
    }

    [Fact]
    public void Sets_trust_server_certificate_and_connect_timeout()
    {
        var b = Build(new ConnectionCredentials { TrustServerCertificate = true, ConnectTimeoutSec = 42 });
        b.TrustServerCertificate.Should().BeTrue();
        b.ConnectTimeout.Should().Be(42);
    }
}

internal static class ConnectionStringAssertions
{
    // For WindowsCredentials the alternate user/password must NOT leak into the connection string.
    public static void ShouldBeEquivalentToNoCredentialsInString(this SqlConnectionStringBuilder b)
    {
        b.ConnectionString.Should().NotContain("Password");
        b.ConnectionString.Should().NotContain("User ID");
    }
}
