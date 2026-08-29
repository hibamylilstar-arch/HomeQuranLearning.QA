using System.Diagnostics;
using System.Security.Principal;

namespace HomeQuranLearning.ClassroomAgent.Setup;

internal sealed record InteractiveUserIdentity(
    string AccountName,
    string Sid)
{
    public static InteractiveUserIdentity Resolve()
    {
        string accountName =
            NativeMethods.QuerySessionUserName(
                Process.GetCurrentProcess().SessionId);

        var account =
            new NTAccount(accountName);

        var sid =
            (SecurityIdentifier)account.Translate(
                typeof(SecurityIdentifier));

        return new InteractiveUserIdentity(
            accountName,
            sid.Value);
    }
}
