public static class PendingGameLoad
{
    private static string pendingFileName;

    public static void Set(string fileName)
    {
        pendingFileName = fileName;
    }

    public static string Consume()
    {
        string value = pendingFileName;
        pendingFileName = null;
        return value;
    }

    public static bool HasPendingLoad()
    {
        return !string.IsNullOrWhiteSpace(pendingFileName);
    }

    public static void Clear()
    {
        pendingFileName = null;
    }
}