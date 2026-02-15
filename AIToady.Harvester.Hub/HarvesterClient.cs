using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

public class HarvesterClient
{
    private readonly HttpClient _http;
    private readonly string _id;

    public HarvesterClient(string harvesterId, string hubUrl = "http://localhost:5000")
    {
        _http = new HttpClient { BaseAddress = new Uri(hubUrl) };
        _id = harvesterId;
    }

    public async Task UpdateStatus(string status) =>
        await _http.PostAsJsonAsync($"/api/harvester/{_id}/status", new { Status = status });

    public async Task Log(string message) =>
        await _http.PostAsJsonAsync($"/api/harvester/{_id}/log", new { Message = message });
}

// Example usage:
// var client = new HarvesterClient("Harvester-1");
// await client.UpdateStatus("Harvesting");
// await client.Log("Started processing batch 42");
