using System.Collections.Generic;
using Amazon.EC2.Model;

public class ResizeDetail
{
    public string Environment { get; set; }

    public string Region { get; set; }

    public string InstanceId { get; set; }

    public string NewInstanceType { get; set; }

    public bool StopRunningInstance { get; set; } = false;

    public bool StartStoppedInstance { get; set; } = false;

    public string ReturnFormat { get; set; } = "json";
}