using System;
using System.IO;
using System.Text.Json;

namespace AIToady.Harvester
{
    public class SettingsManager
    {
        private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public static void Save()
        {
            var settings = Properties.Settings.Default;
            var json = JsonSerializer.Serialize(new
            {
                settings.Url,
                settings.NextElement,
                settings.ThreadElement,
                settings.ThreadsToSkip,
                settings.PageLoadDelay,
                settings.RootFolder,
                settings.WindowWidth,
                settings.WindowHeight,
                settings.WindowLeft,
                settings.WindowTop,
                settings.StartTime,
                settings.EndTime,
                settings.SkipExistingThreads,
                settings.SiteName,
                settings.ForumName,
                settings.MessageElement,
                settings.ImageElement,
                settings.AttachmentElement,
                settings.HoursOfOperationEnabled,
                settings.EmailAccount,
                settings.EmailPassword,
                settings.Category,
                settings.DarkMode,
                settings.ScheduleForums,
                settings.HarvestSince,
                settings.MessagesPerPage,
                settings.InPrivateMode,
                settings.SkipImages
            }, new JsonSerializerOptions { WriteIndented = true });
            
            File.WriteAllText(SettingsPath, json);
        }

        public static void Load()
        {
            if (!File.Exists(SettingsPath)) return;

            var json = File.ReadAllText(SettingsPath);
            var data = JsonSerializer.Deserialize<JsonElement>(json);
            var settings = Properties.Settings.Default;

            if (data.TryGetProperty("Url", out var url)) settings.Url = url.GetString();
            if (data.TryGetProperty("NextElement", out var next)) settings.NextElement = next.GetString();
            if (data.TryGetProperty("ThreadElement", out var thread)) settings.ThreadElement = thread.GetString();
            if (data.TryGetProperty("ThreadsToSkip", out var skip)) settings.ThreadsToSkip = skip.GetInt32();
            if (data.TryGetProperty("PageLoadDelay", out var delay)) settings.PageLoadDelay = delay.GetInt32();
            if (data.TryGetProperty("RootFolder", out var root)) settings.RootFolder = root.GetString();
            if (data.TryGetProperty("WindowWidth", out var width)) settings.WindowWidth = width.GetDouble();
            if (data.TryGetProperty("WindowHeight", out var height)) settings.WindowHeight = height.GetDouble();
            if (data.TryGetProperty("WindowLeft", out var left)) settings.WindowLeft = left.GetDouble();
            if (data.TryGetProperty("WindowTop", out var top)) settings.WindowTop = top.GetDouble();
            if (data.TryGetProperty("StartTime", out var start)) settings.StartTime = start.GetString();
            if (data.TryGetProperty("EndTime", out var end)) settings.EndTime = end.GetString();
            if (data.TryGetProperty("SkipExistingThreads", out var skipEx)) settings.SkipExistingThreads = skipEx.GetBoolean();
            if (data.TryGetProperty("SiteName", out var site)) settings.SiteName = site.GetString();
            if (data.TryGetProperty("ForumName", out var forum)) settings.ForumName = forum.GetString();
            if (data.TryGetProperty("MessageElement", out var msg)) settings.MessageElement = msg.GetString();
            if (data.TryGetProperty("ImageElement", out var img)) settings.ImageElement = img.GetString();
            if (data.TryGetProperty("AttachmentElement", out var att)) settings.AttachmentElement = att.GetString();
            if (data.TryGetProperty("HoursOfOperationEnabled", out var hours)) settings.HoursOfOperationEnabled = hours.GetBoolean();
            if (data.TryGetProperty("EmailAccount", out var email)) settings.EmailAccount = email.GetString();
            if (data.TryGetProperty("EmailPassword", out var pass)) settings.EmailPassword = pass.GetString();
            if (data.TryGetProperty("Category", out var cat)) settings.Category = cat.GetString();
            if (data.TryGetProperty("DarkMode", out var dark)) settings.DarkMode = dark.GetBoolean();
            if (data.TryGetProperty("ScheduleForums", out var sched)) settings.ScheduleForums = sched.GetString();
            if (data.TryGetProperty("HarvestSince", out var since)) settings.HarvestSince = since.GetString();
            if (data.TryGetProperty("MessagesPerPage", out var msgs)) settings.MessagesPerPage = msgs.GetInt32();
            if (data.TryGetProperty("InPrivateMode", out var priv)) settings.InPrivateMode = priv.GetBoolean();
            if (data.TryGetProperty("SkipImages", out var skipImg)) settings.SkipImages = skipImg.GetBoolean();
        }
    }
}
