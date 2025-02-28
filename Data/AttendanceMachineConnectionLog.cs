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
        public DateTime ConnectionStartTime { get; set; }
        public DateTime? ConnectionEndTime { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public int? RecordsRead { get; set; }
    }
}
