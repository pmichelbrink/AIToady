using AIToady.Harvester.Models;
using System.Text.Json;

namespace AIToady.Harvester.ViewModels
{
    public class SassNetViewModel : BrianEnosViewModel
    {
        public override async Task ExecuteLoadThreads()
        {
            try
            {
                if (InvokeExecuteScriptRequested == null)
                {
                    AddLogEntry("WebView2 not ready. Please navigate to a page first.");
                    return;
                }

                string script = @"
                    (function() {
                        let threads = [];
                        document.querySelectorAll('li.ipsDataItem[data-rowid]').forEach(li => {
                            let link = li.querySelector('.ipsDataItem_main .ipsDataItem_title a[href*=""/topic/""]');
                            if (link) {
                                let lastPostDate = null;
                                let timeElement = li.querySelector('.ipsDataItem_lastPoster time[datetime]');
                                if (timeElement) lastPostDate = timeElement.getAttribute('datetime');
                                threads.push({ url: link.href, lastPostDate: lastPostDate });
                            }
                        });
                        return JSON.stringify(threads);
                    })()
                ";

                string result = await InvokeExecuteScriptRequested(script);
                result = JsonSerializer.Deserialize<string>(result);
                var threads = JsonSerializer.Deserialize<ThreadInfo[]>(result);
                _threadInfos.Clear();
                _threadInfos.AddRange(threads);
            }
            catch (Exception ex)
            {
                AddLogEntry($"Error loading threads: {ex.Message}");
            }
        }
    }
}
