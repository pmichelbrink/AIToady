using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<HubDatabase>();

var app = builder.Build();
var db = app.Services.GetRequiredService<HubDatabase>();
//SampleDataSeeder.Seed(db);
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapHub<HarvesterHub>("/hub");
app.MapPost("/api/harvester/{id}/status", (string id, StatusUpdate update, HubDatabase db, IHubContext<HarvesterHub> hub) =>
{
    db.UpdateStatus(id, update.Status);
    hub.Clients.All.SendAsync("statusUpdate", id, update.Status);
});
app.MapPost("/api/harvester/{id}/log", (string id, LogEntry entry, HubDatabase db, IHubContext<HarvesterHub> hub) =>
{
    db.AddLog(id, entry.Message);
    hub.Clients.All.SendAsync("logUpdate", id, entry.Message);
});
app.MapGet("/api/harvesters", (HubDatabase db) => db.GetAll());
app.Run();

record StatusUpdate(string Status);
record LogEntry(string Message);
