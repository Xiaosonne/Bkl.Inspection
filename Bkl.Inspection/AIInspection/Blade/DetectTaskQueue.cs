using Bkl.Models;
public class DetectTaskQueue : BackgroundTaskQueue<DetectTaskInfo>
{
     
    public DetectTaskQueue(int capacity):base(capacity)
    {
        
    }
}
public class ELDetectTaskQueue : BackgroundTaskQueue<ELDetectTaskInfo>
{

    public ELDetectTaskQueue(int capacity) : base(capacity)
    {

    }
}


public class SegTaskQueue : BackgroundTaskQueue<SegTaskInfo>
{
    public SegTaskQueue(int capacity) : base(capacity)
    {

    }
}

public class FuseTaskQueue : BackgroundTaskQueue<FuseTaskInfo>
{
    public FuseTaskQueue(int capacity) : base(capacity)
    {

    }
}