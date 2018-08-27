using System.Collections.Generic;
using Amazon.EC2.Model;

public class ResizeRequest
{
    public List<ResizeDetail> Details { get; set; }
}