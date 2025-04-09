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
                string employeeName = "Unknown";
                if (zkTecoDevice.SSR_GetUserInfo(1, enrollId, out employeeName, out _, out _, out _))
                {
                    //LogInfo($"[INFO] Retrieved Employee Name: {employeeName} for ID: {enrollId}");
                }
                //else
                //{
                //    LogError($"[ERROR] Failed to retrieve Employee Name for ID: {enrollId}");
                //}

                DateTime punchTime = new DateTime(dwYear, dwMonth, dwDay, dwHour, dwMinute, dwSecond);

                bool isShiftIn = dwInOutMode == 0;
                bool isShiftOut = dwInOutMode == 1;

                HRSwapRecord newRecord = new HRSwapRecord
                {
                    EmpNo = enrollId,
                    EmpName = employeeName,
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
        public List<Employee> GetEmployees(string ip, int port)
        {
            List<Employee> employees = new List<Employee>();
           
                // Read all employees from the device
                if (!zkTecoDevice.ReadAllUserID(1))
                {
                    LogInfo($"[INFO] No employee records found on the device at IP: {ip}:{port}.");
                    return employees;
                }

                while (zkTecoDevice.SSR_GetAllUserInfo(
                    1,
                    out string enrollId,
                    out string employeeName,
                    out string password,
                    out int privilege,
                    out bool enabled))
                {
                    employees.Add(new Employee
                    {
                        EmpNo = enrollId,
                        EmpName = employeeName
                    });

                    LogInfo($"[INFO] Retrieved Employee ID: {enrollId}, Name: {employeeName}");
                }

                LogInfo($"[INFO] Successfully retrieved {employees.Count} employees from the device at IP: {ip}:{port}.");            
            return employees;
        }
        //public bool UploadEmployee(string ip, int port, string EmpNo,string EmpName)
        public bool UploadEmployee(Employee employee)
        {
            try
            {
            bool result = zkTecoDevice.SSR_SetUserInfo(
               1,
               employee.EmpNo,
               employee.EmpName,
               "", // Password (optional, leave empty if not required)
               0,  // Privilege level (0 = User, 14 = Admin)
               true // Enabled status
                );
                if (result)
                {
                    //LogInfo($"[INFO] Successfully uploaded Employee ID: {employee.EmpNo}, Name: {employee.EmpName} to device at IP: {ip}");
                    return true;
                }
                else
                {
                    //LogError($"[ERROR] Failed to upload Employee ID: {employee.EmpNo}, Name: {employee.EmpName} to device at IP: {ip}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                //LogError($"[ERROR] Exception during upload of Employee ID: {employee.EmpNo}, Name: {employee.EmpName}: {ex.Message}");
                return false;
            }
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