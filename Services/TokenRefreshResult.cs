namespace PAYLO_WEB.Services
{
    public enum TokenRefreshResult
    {
        NotNeeded,   // Token is fresh — no action taken, user is fine
        Refreshed,   // Token was expiring — refreshed successfully
        Failed       // Refresh attempted but failed — force logout
    }
}
