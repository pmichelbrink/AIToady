public class SampleDataSeeder
{
    public static void Seed(HubDatabase db)
    {
        var harvesters = new[] { "Harvester-1", "Harvester-2", "Harvester-3", "Harvester-4" };
        var statuses = new[] { "Harvesting", "Stuck", "Error", "Idle" };
        var logs = new[] {
            "Started processing batch",
            "Connected to data source",
            "Processing 1000 records",
            "Completed batch successfully",
            "Warning: High memory usage",
            "Retrying failed connection",
            "Database query took 2.3s",
            "Checkpoint saved"
        };

        for (int i = 0; i < harvesters.Length; i++)
        {
            db.UpdateStatus(harvesters[i], statuses[i]);
            for (int j = 0; j < 10; j++)
                db.AddLog(harvesters[i], $"{logs[j % logs.Length]} #{j + 1}");
        }
    }
}
