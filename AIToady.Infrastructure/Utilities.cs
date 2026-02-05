namespace AIToady.Infrastructure
{
    public class Utilities
    {
        public static bool IsValidForumUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.StartsWith("chrome-error://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("edge-error://", StringComparison.OrdinalIgnoreCase))
                return false;

            return Uri.TryCreate(url, UriKind.Absolute, out var uri) && 
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public static bool IsXenForoAttachmentUrl(string url)
        {
            return url?.Contains("attachments/") == true && 
                   System.Text.RegularExpressions.Regex.IsMatch(url, @"attachments/([^/]+?)\.(\d+)/?$");
        }

        public static string GetXenForoAttachmentFileName(string attachmentUrl)
        {
            var match = System.Text.RegularExpressions.Regex.Match(attachmentUrl, @"attachments/([^/]+?)\.(\d+)/?$");
            if (match.Success)
            {
                string baseName = match.Groups[1].Value;
                int lastHyphen = baseName.LastIndexOf('-');
                if (lastHyphen > 0)
                    return baseName.Substring(0, lastHyphen) + "." + baseName.Substring(lastHyphen + 1);
            }
            return null;
        }
    }
}
