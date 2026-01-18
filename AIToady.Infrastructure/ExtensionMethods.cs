namespace AIToady.Infrastructure
{
    public static class ExtensionMethods
    {
        public static bool IsUnavailableError(this string message)
        {
            return message.Contains("301") || message.Contains("400") || message.Contains("410") || message.Contains("429") ||
                   message.Contains("423") || message.Contains("443") || message.Contains("failed to respond") || 
                   message.Contains("no data") || message.Contains("403") || message.Contains("404") || message.Contains("409") ||
                   message.Contains("401") || message.Contains("415") || message.Contains("409") || message.Contains("432") ||
                   message.Contains("422") || message.Contains("302") || message.Contains("500") || message.Contains("303") ||
                   message.Contains("418") || message.Contains("406") || message.Contains("520") || message.Contains("444");
        }

        public static bool IsTimeoutError(this string message)
        {
            return message.Contains("actively refused") || message.Contains("error occurred while sending the request") || message.Contains("such host is known") ||
                   message.Contains("100") || message.Contains("SSL") || message.Contains("441") || message.Contains("502") || message.Contains("503") || 
                   message.Contains("504") || message.Contains("521") || message.Contains("522") || message.Contains("526") || message.Contains("530") ||
                   message.Contains("523") || message.Contains("525");
        }

        public static string GetRootDomain(this string host)
        {
            var parts = host.Split('.');
            return parts.Length >= 2 ? string.Join(".", parts.TakeLast(2)) : host;
        }
    }
}