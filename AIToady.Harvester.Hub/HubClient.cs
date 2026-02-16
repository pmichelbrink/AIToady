using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class HubClient
{
    private readonly HttpClient _http;
    private readonly string _id;

    public HubClient(string harvesterId, string hubUrl = "http://localhost:5000")
    {
        _http = new HttpClient 
        { 
            BaseAddress = new Uri(hubUrl),
            Timeout = TimeSpan.FromSeconds(2)
        };
        _id = harvesterId;
    }

    public async Task<bool> UpdateStatus(string status)
    {
        try 
        { 
            await _http.PostAsJsonAsync($"/api/harvester/{_id}/status", new { Status = status });
            return true;
        }
        catch { return false; }
    }

    public async Task<bool> Log(string message)
    {
        try 
        { 
            await _http.PostAsJsonAsync($"/api/harvester/{_id}/log", new { Message = message });
            return true;
        }
        catch { return false; }
    }
}

// Example usage:
// var client = new HarvesterClient("Harvester-1");
// if (!await client.UpdateStatus("Harvesting"))
// {
//     // Hub is not available - stop trying
//     return;
// }
// await client.Log("Started processing batch 42");
