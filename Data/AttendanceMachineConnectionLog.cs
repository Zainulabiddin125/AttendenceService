using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendenceService.Data
{
    public class AttendanceMachineConnectionLog
    {
        public int Id { get; set; }
        public int MachineId { get; set; }
        public string Machine_IP { get; set; }
        public DateTime Connection_StartTime { get; set; }
        public DateTime? Connection_EndTime { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
       
        public int? RecordsRead { get; set; }
    }
}
