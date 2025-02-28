using AttendenceService.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using zkemkeeper;

namespace AttendenceService.Services
{
    public class ZKTecoHelper
    {
        private CZKEM zkTecoDevice = new CZKEM();

        /// <summary>
        /// Connects to the ZKTeco device using the provided IP and port.
        /// </summary>
        public bool Connect(string ip, int port)
        {
            try
            {
                return zkTecoDevice.Connect_Net(ip, port);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Connection Failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Retrieves attendance records from the device.
        /// </summary>
        public List<HRSwapRecord> GetAttendanceRecords(int machineId, string machineIP, string machinePort)
        {
            List<HRSwapRecord> records = new List<HRSwapRecord>();

            if (!zkTecoDevice.ReadGeneralLogData(1))
            {
                Console.WriteLine($"[INFO] No attendance records found from machine {machineIP}:{machinePort}.");
                return records;
            }

            int dwEnrollNumber = 0, dwVerifyMode = 0, dwInOutMode = 0, dwYear = 0, dwMonth = 0, dwDay = 0;
            int dwHour = 0, dwMinute = 0, dwSecond = 0, dwWorkCode = 0;

            while (zkTecoDevice.SSR_GetGeneralLogData(
                1,
                out string enrollId,
                out dwVerifyMode,
                out dwInOutMode,
                out dwYear,
                out dwMonth,
                out dwDay,
                out dwHour,
                out dwMinute,
                out dwSecond,
                ref dwWorkCode))
            {
                // Ensure enrollId is numeric before processing
                if (int.TryParse(enrollId, out dwEnrollNumber))
                {
                    records.Add(new HRSwapRecord
                    {
                        EmpNo = enrollId,
                        SwapTime = new DateTime(dwYear, dwMonth, dwDay, dwHour, dwMinute, dwSecond),
                        ShiftIn = dwInOutMode == 0,  // Example: ShiftIn if mode is 0
                        ShiftOut = dwInOutMode == 1, // Example: ShiftOut if mode is 1
                        CreationDate = DateTime.Now,
                        LastUpdateDate = DateTime.Now,
                        MachineId = machineId,
                        MachineIP = machineIP,
                        MachinePort = machinePort,
                        DeviceLogId = dwWorkCode
                    });
                }
                else
                {
                    Console.WriteLine($"[WARNING] Invalid Employee ID: {enrollId}");
                }
            }

            return records;
        }
        /// <summary>
        /// Disconnects from the device.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                zkTecoDevice.Disconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Disconnection Failed: {ex.Message}");
            }
        }
    }
}
