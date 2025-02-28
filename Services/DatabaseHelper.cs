using AttendenceService.Data;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AttendenceService.Services
{
    public class DatabaseHelper
    {
        private readonly string _connectionString = "Data Source=ROBOT;Initial Catalog=HR;Integrated Security=True;Persist Security Info=True;Encrypt=True;TrustServerCertificate=True;user id=sa;password=123;";
        //private readonly string _connectionString = "Data Source=SRV-ATTENDANCE;Initial Catalog=HR_Testing;Integrated Security=True;Persist Security Info=True;Encrypt=True;TrustServerCertificate=True;user id=sa;password=123@@;";
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
                            SerialNumber = reader.IsDBNull(5) ? null : reader.GetString(5),
                            Description = reader.IsDBNull(6) ? null : reader.GetString(6),
                            DeviceModel = reader.IsDBNull(7) ? null : reader.GetString(7),
                            CreatedAt = reader.GetDateTime(8),
                            LastUpdated = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9)
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
    }
}
