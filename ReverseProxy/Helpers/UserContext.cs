static class UserContext
{
    public const string HeaderName = "X-User-Id";
    public const string ItemKey = "UserIdGuid";

    public static bool TryReadUserId(HttpContext ctx, out Guid userId)
    {
        userId = default;

        if (!ctx.Request.Headers.TryGetValue(HeaderName, out var raw))
            return false;

        var s = raw.ToString();
        return Guid.TryParse(s, out userId);
    }
}