using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendenceService.Data
{
    public class Transfer_Record
    {
        public string Emp_No { get; set; }
        public string Emp_Name { get; set; }
        public string SourceMachine_IP { get; set; }
        public string DestinationMachine_IP { get; set; }
        public string Created_By { get; set; }
        public DateTime Creation_Date { get; set; }
    }
}
