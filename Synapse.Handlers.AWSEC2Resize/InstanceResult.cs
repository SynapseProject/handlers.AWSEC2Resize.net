using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synapse.Handlers.AWSEC2Resize
{
    public class InstanceResult
    {
        public string InstanceId { get; set; }
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public string InstanceState { get; set; }
        public string InstanceType { get; set; }
        public string Status { get; set; }
    }
}
