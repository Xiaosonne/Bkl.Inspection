using Bkl.Models;
using System.Collections.Generic;

public class GenerateAllTaskRequest
{
	public long taskId { get; set; }

	public long factoryId { get; set; }

	public string mode { get; set; }

	public BklFactory factory { get; set; }
	public BklInspectionTask task { get; set; }
	public List<BklFactoryFacility> facilities { get; set; }
	public string SeqId { get; internal set; }
	public List<BklInspectionTaskDetail> taskDetails { get; set; }
	public List<BklInspectionTaskResult> taskResults { get; set; }
}
