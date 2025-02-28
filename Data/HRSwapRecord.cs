using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendenceService.Data
{
    public class HRSwapRecord
    {
        public int PkLineId { get; set; }
        public string EmpNo { get; set; }
        public DateTime? SwapTime { get; set; }
        public bool Manual { get; set; } = false;
        public bool ShiftIn { get; set; } = false;
        public bool ShiftOut { get; set; } = false;
        public string Remarks { get; set; }
        public DateTime CreationDate { get; set; } = DateTime.Now;
        public int? LastUpdateBy { get; set; }
        public DateTime? LastUpdateDate { get; set; }
        public string MachineIP { get; set; }
        public string MachinePort { get; set; }
        public int? DeviceLogId { get; set; }
        public int? MachineId { get; set; }
    }
}
