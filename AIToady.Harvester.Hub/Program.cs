using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;

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
app.MapPost("/api/harvester/{id}/log", (string id, LogEntryRequest entry, HubDatabase db, IHubContext<HarvesterHub> hub) =>
{
    db.AddLog(id, entry.Message);
    hub.Clients.All.SendAsync("logUpdate", id, new { entry.Message, Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") });
});
app.MapGet("/api/harvesters", (HubDatabase db) => db.GetAll());
app.MapDelete("/api/harvester/{id}", (string id, HubDatabase db, IHubContext<HarvesterHub> hub) =>
{
    db.Delete(id);
    hub.Clients.All.SendAsync("harvesterDeleted", id);
});
app.Run();

record StatusUpdate(string Status);
record LogEntryRequest(string Message);
