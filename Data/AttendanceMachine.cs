using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendenceService.Data
{
    public class AttendanceMachine
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string IpAddress { get; set; }
        public int Port { get; set; }
        public bool IsActive { get; set; }
        public bool IsFetchAll { get; set; }
        public string SerialNumber { get; set; }
        public string Description { get; set; }
        public string DeviceModel { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUpdated { get; set; }
    }
}
