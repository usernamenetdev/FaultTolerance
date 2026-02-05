static class InfraPaths
{
    public static bool IsInfra(PathString path) =>
        path.StartsWithSegments("/health") ||
        path.StartsWithSegments("/alive") ||
        path.StartsWithSegments("/ready");
}