using AttendenceService.Data;
using System;
using System.Collections.Generic;
using System.IO;
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
        /// 
        public List<HRSwapRecord> GetAttendanceRecords(int machineId, string machineIP, string machinePort)
        {
            List<HRSwapRecord> records = new List<HRSwapRecord>();
            Dictionary<string, HRSwapRecord> lastPunches = new Dictionary<string, HRSwapRecord>(); // Stores last punch per user

            LogInfo($"[INFO] Attempting to read attendance records from machine {machineIP}:{machinePort}.");

            if (!zkTecoDevice.ReadGeneralLogData(1))
            {
                LogInfo($"[INFO] No attendance records found from machine {machineIP}:{machinePort}.");
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
                if (!int.TryParse(enrollId, out dwEnrollNumber))
                {
                    LogError($"[WARNING] Invalid Employee ID encountered: {enrollId}");
                    continue;
                }

                DateTime punchTime = new DateTime(dwYear, dwMonth, dwDay, dwHour, dwMinute, dwSecond);

                bool isShiftIn = false;
                bool isShiftOut = false;

                if (dwInOutMode == 0) // Typically represents IN
                {
                    isShiftIn = true;
                }
                else if (dwInOutMode == 1) // Typically represents OUT
                {
                    isShiftOut = true;
                }
                else
                {
                    // If the mode is unclear, determine based on last punch record
                    if (lastPunches.ContainsKey(enrollId))
                    {
                        var lastPunch = lastPunches[enrollId];
                        if (lastPunch.ShiftIn) // If last punch was IN, this must be OUT
                        {
                            isShiftOut = true;
                        }
                        else
                        {
                            isShiftIn = true; // Otherwise, mark it as IN
                        }
                    }
                    else
                    {
                        isShiftIn = true; // Default first punch of the day as IN
                    }
                }

                HRSwapRecord newRecord = new HRSwapRecord
                {
                    EmpNo = enrollId,
                    SwapTime = punchTime,
                    ShiftIn = isShiftIn,
                    ShiftOut = isShiftOut,
                    CreationDate = DateTime.Now,
                    LastUpdateDate = DateTime.Now,
                    MachineId = machineId,
                    MachineIP = machineIP,
                    MachinePort = machinePort,
                    DeviceLogId = dwWorkCode
                };

                lastPunches[enrollId] = newRecord;
                records.Add(newRecord);

                //LogInfo($"[INFO] Attendance recorded: EmpNo={enrollId}, Time={punchTime}, ShiftIn={isShiftIn}, ShiftOut={isShiftOut}");
            }

            LogInfo($"[INFO] Successfully retrieved {records.Count} attendance records from machine {machineIP}:{machinePort}.");
            return records;
        }



        //public List<HRSwapRecord> GetAttendanceRecords(int machineId, string machineIP, string machinePort)
        //{
        //    List<HRSwapRecord> records = new List<HRSwapRecord>();

        //    if (!zkTecoDevice.ReadGeneralLogData(1))
        //    {
        //        Console.WriteLine($"[INFO] No attendance records found from machine {machineIP}:{machinePort}.");
        //        return records;
        //    }

        //    int dwEnrollNumber = 0, dwVerifyMode = 0, dwInOutMode = 0, dwYear = 0, dwMonth = 0, dwDay = 0;
        //    int dwHour = 0, dwMinute = 0, dwSecond = 0, dwWorkCode = 0;

        //    while (zkTecoDevice.SSR_GetGeneralLogData(
        //        1,
        //        out string enrollId,
        //        out dwVerifyMode,
        //        out dwInOutMode,
        //        out dwYear,
        //        out dwMonth,
        //        out dwDay,
        //        out dwHour,
        //        out dwMinute,
        //        out dwSecond,
        //        ref dwWorkCode))
        //    {
        //        // Ensure enrollId is numeric before processing
        //        if (int.TryParse(enrollId, out dwEnrollNumber))
        //        {
        //            records.Add(new HRSwapRecord
        //            {
        //                EmpNo = enrollId,
        //                SwapTime = new DateTime(dwYear, dwMonth, dwDay, dwHour, dwMinute, dwSecond),
        //                ShiftIn = dwInOutMode == 0,  // Example: ShiftIn if mode is 0
        //                ShiftOut = dwInOutMode == 1, // Example: ShiftOut if mode is 1
        //                CreationDate = DateTime.Now,
        //                LastUpdateDate = DateTime.Now,
        //                MachineId = machineId,
        //                MachineIP = machineIP,
        //                MachinePort = machinePort,
        //                DeviceLogId = dwWorkCode
        //            });
        //        }
        //        else
        //        {
        //            Console.WriteLine($"[WARNING] Invalid Employee ID: {enrollId}");
        //        }
        //    }

        //    return records;
        //}
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
        public List<HRSwapRecord> GetNewAttendanceRecords(int machineId, string ipAddress, string port, DateTime lastTimestamp)
        {
            List<HRSwapRecord> allRecords = GetAttendanceRecords(machineId, ipAddress, port);
            return allRecords.Where(record => record.SwapTime > lastTimestamp).ToList();
        }
        private void LogInfo(string message)
        {
            try
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + "\\LogsFile.txt";
                using (StreamWriter sw = new StreamWriter(logPath, true))
                {
                    sw.WriteLine($"{DateTime.Now}: {message}");
                }
            }
            catch (Exception ex)
            {
                string errorLog = $"❌ Failed to log info: {ex.Message}";
                Console.WriteLine(errorLog);
                File.AppendAllText(AppDomain.CurrentDomain.BaseDirectory + "\\LogsFile.txt", $"{DateTime.Now}: {errorLog}\n");
            }
        }
        private void LogError(string message)
        {
            try
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + "\\LogsFile.txt";
                using (StreamWriter sw = new StreamWriter(logPath, true))
                {
                    sw.WriteLine($"Error In Attendance Insertion: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to log error: {ex.Message}");
            }
        }
    }
}
