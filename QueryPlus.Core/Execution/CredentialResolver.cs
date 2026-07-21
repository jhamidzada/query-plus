using QueryPlus.Core.Domain;

namespace QueryPlus.Core.Execution;

/// <summary>Decides which credentials a target actually connects with.</summary>
public static class CredentialResolver
{
    /// <summary>
    /// A target uses its own override credentials when present and usable; otherwise it falls
    /// back to the list's credentials. An override that still needs a password (e.g. a leftover
    /// snapshot whose password was never saved) defers to the list, so setting the list password
    /// is enough to make the run work.
    /// </summary>
    public static ConnectionCredentials Resolve(DistributionList list, Target target)
    {
        var credentials = target.Credentials;
        if (credentials == null || credentials.NeedsPassword)
            return list.DefaultCredentials();
        return credentials;
    }
}
