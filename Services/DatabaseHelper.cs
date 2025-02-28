using AttendenceService.Data;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;

namespace AttendenceService.Services
{
    public class DatabaseHelper
    {
        //private readonly string _connectionString = "Data Source=ROBOT;Initial Catalog=HR;Integrated Security=True;Persist Security Info=True;Encrypt=True;TrustServerCertificate=True;user id=sa;password=123;";
        private readonly string _connectionString = "Data Source=SRV-ATTENDANCE;Initial Catalog=HR_Testing;Integrated Security=True;Persist Security Info=True;user id=sa;password=123@@;";
        // Method to test database connection
        public bool TestConnection()
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    Console.WriteLine("✅ Connection Successful!");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("❌ Connection Failed: " + ex.Message);
                return false;
            }
        }

        public List<AttendanceMachine> GetActiveMachines()
        {
            var machines = new List<AttendanceMachine>();

            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT * FROM AttendenceMachines WHERE IsActive = 1", conn))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        machines.Add(new AttendanceMachine
                        {
                            Id = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            IpAddress = reader.GetString(2),
                            Port = reader.GetInt32(3),
                            IsActive = reader.GetBoolean(4),
                            IsFetchAll = reader.GetBoolean(5),
                            SerialNumber = reader.IsDBNull(6) ? null : reader.GetString(6),
                            Description = reader.IsDBNull(7) ? null : reader.GetString(7),
                            DeviceModel = reader.IsDBNull(8) ? null : reader.GetString(8),
                            CreatedAt = reader.GetDateTime(9),
                            LastUpdated = reader.IsDBNull(10) ? (DateTime?)null : reader.GetDateTime(10)
                        });
                    }
                }
            }
            return machines;
        }

        public void InsertAttendanceRecords(int machineId, List<HRSwapRecord> records)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                foreach (var record in records)
                {
                    //LogInfo($"📌 Inserting: MachineID={machineId}, EmpID={record.EmpNo}, Time={record.SwapTime}");
                    try
                    {
                        using (var cmd = new SqlCommand(@"
                    INSERT INTO HR_Swap_Record 
                    (Emp_No, Swap_Time, Remarks, Creation_Date, Machine_IP, Machine_Port, DeviceLogId, MachineId) 
                    VALUES (@EmpNo, @SwapTime, @Remarks, @CreationDate, @MachineIP, @MachinePort, @DeviceLogId, @MachineId)", conn))
                        {
                            cmd.Parameters.AddWithValue("@EmpNo", record.EmpNo ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@SwapTime", record.SwapTime);
                            cmd.Parameters.AddWithValue("@Remarks", record.Remarks ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@CreationDate", record.CreationDate);
                            cmd.Parameters.AddWithValue("@MachineIP", record.MachineIP);
                            cmd.Parameters.AddWithValue("@MachinePort", record.MachinePort);
                            cmd.Parameters.AddWithValue("@DeviceLogId", record.DeviceLogId ?? (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@MachineId", record.MachineId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError($"❌ Failed to insert record: {ex.Message}");
                    }

                }
            }
        }

        public void LogMachineSync(int machineId, string status, int recordsRead, string errorMessage, DateTime startTime, DateTime? endTime)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand(@"
                INSERT INTO AttendenceMachineConnectionLogs (MachineId, ConnectionStartTime, ConnectionEndTime, Status, ErrorMessage, RecordsRead) 
                VALUES (@MachineId, @StartTime, @EndTime, @Status, @ErrorMessage, @RecordsRead)", conn))
                {
                    cmd.Parameters.AddWithValue("@MachineId", machineId);
                    cmd.Parameters.AddWithValue("@StartTime", startTime);
                    cmd.Parameters.AddWithValue("@EndTime", endTime ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@ErrorMessage", errorMessage ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@RecordsRead", recordsRead);
                    cmd.ExecuteNonQuery();
                }
            }
        }
          
        public DateTime? GetLastAttendanceTimestamp(int machineId, string ipAddress)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT MAX(Swap_Time) FROM HR_Swap_Record WHERE MachineId = @MachineId AND Machine_IP = @IpAddress", conn))
                {
                    cmd.Parameters.AddWithValue("@MachineId", machineId);
                    cmd.Parameters.AddWithValue("@IpAddress", ipAddress); // Removed extra @

                    var result = cmd.ExecuteScalar();
                    return result != DBNull.Value ? (DateTime?)result : null;
                }
            }
        }
        public DateTime? GetLastRecordCreationTimestamp(int machineId, string ipAddress)
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (var cmd = new SqlCommand("SELECT MAX(Creation_Date) FROM HR_Swap_Record WHERE MachineId = @MachineId AND Machine_IP = @IpAddress", conn))
                {
                    cmd.Parameters.AddWithValue("@MachineId", machineId);
                    cmd.Parameters.AddWithValue("@IpAddress", ipAddress);
                    var result = cmd.ExecuteScalar();
                    return result != DBNull.Value ? (DateTime?)result : null;
                }
            }
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
