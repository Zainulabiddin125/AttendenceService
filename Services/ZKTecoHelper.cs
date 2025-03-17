using AttendenceService.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using zkemkeeper;

namespace AttendenceService.Services
{
    public class ZKTecoHelper
    {
        private CZKEM zkTecoDevice = new CZKEM();

        public bool Connect(string ip, int port)
        {
            try
            {
                return zkTecoDevice.Connect_Net(ip, port);
            }
            catch (Exception ex)
            {
                LogError($"[ERROR] Connection Failed: {ex.Message}");
                return false;
            }
        }

        public List<HRSwapRecord> GetAttendanceRecords(int machineId, string machineIP, string machinePort)
        {
            List<HRSwapRecord> records = new List<HRSwapRecord>();
            Dictionary<string, HRSwapRecord> lastPunches = new Dictionary<string, HRSwapRecord>();

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

                bool isShiftIn = dwInOutMode == 0;
                bool isShiftOut = dwInOutMode == 1;

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
                    MachinePort = machinePort
                };

                lastPunches[enrollId] = newRecord;
                records.Add(newRecord);
            }

           LogInfo($"[INFO] Successfully retrieved {records.Count} attendance records from machine {machineIP}:{machinePort}.");
            return records;
        }

        public void Disconnect()
        {
            try
            {
                zkTecoDevice.Disconnect();
            }
            catch (Exception ex)
            {
                LogError($"[ERROR] Disconnection Failed: {ex.Message}");
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
                Console.WriteLine($"❌ Failed to log info: {ex.Message}");
            }
        }

        private void LogError(string message)
        {
            try
            {
                string logPath = AppDomain.CurrentDomain.BaseDirectory + "\\LogsFile.txt";
                using (StreamWriter sw = new StreamWriter(logPath, true))
                {
                    sw.WriteLine($"{DateTime.Now}: ERROR: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to log error: {ex.Message}");
            }
        }
    }
}