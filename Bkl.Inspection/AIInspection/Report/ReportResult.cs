using Bkl.Infrastructure;
using System;
using System.Collections.Generic;
using System.Text.Json;

public class ReportResult
{
	public long TaskId { get; set; }
	public string SeqId { get; set; }
	public string FileName { get; set; }
	public string Status { get; set; }
	public string Location { get; set; }
	public long FactoryId { get; set; }
	public int FacilityCount { get; set; }
	public DateTime StartTime { get; set; }

	public void SetValue(IRedisClient redisClient)
	{
		redisClient.SetEntryInHash($"ReportGenerate:TaskId.{this.TaskId}", this.SeqId, JsonSerializer.Serialize(this));
	}
	public void LoadValue(IRedisClient redisClient)
	{
		string json = redisClient.GetValueFromHash($"ReportGenerate:TaskId.{this.TaskId}", this.SeqId);
		var val = JsonSerializer.Deserialize<ReportResult>(json);
		this.FileName = val.FileName;
		this.Status = val.Status;
		this.TaskId = val.TaskId;
		this.SeqId = SeqId;
		this.Location = val.Location;
		this.FactoryId = val.FactoryId;
		this.StartTime = val.StartTime;
		this.FacilityCount = val.FacilityCount;
	}
	public static List<ReportResult> LoadValues(IRedisClient redisClient, long taskId)
	{
		var keys = redisClient.GetKeysFromHash($"ReportGenerate:TaskId.{taskId}");
		List<ReportResult> results = new List<ReportResult>();
		foreach (var k in keys)
		{
			var result = new ReportResult { TaskId = taskId, SeqId = k };
			result.LoadValue(redisClient);
			results.Add(result);
		}
		return results;
	}
}
