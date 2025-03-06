using AttendenceService.Data;
using AttendenceService.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
namespace AttendenceService
{
    public partial class AttendanceService : ServiceBase
    {
         private Timer _timer;
        private readonly DatabaseHelper _dbHelper = new DatabaseHelper();
        private readonly ZKTecoHelper _zkTecoHelper = new ZKTecoHelper();
        public AttendanceService()
        {
            ServiceName = "AttendanceService"; // Unique name for Windows Service
            InitializeComponent();
        }
        public void TestStart(string[] args)
        {
            OnStart(args);
        }

        public void TestStop()
        {
            OnStop();
        }
        protected override void OnStart(string[] args)
        {
            _timer = new Timer(1800000); // Run every 30 minutes (1800000 milliseconds)
            _timer.Elapsed += TimerElapsed;
            _timer.Start();

            if (_dbHelper.TestConnection())
            {
                LogInfo("✅ Database connection successful.");
            }
            else
            {
                LogError("❌ Database connection failed. Service cannot proceed.");
                Stop();
                return;
            }
            LogInfo("✅ Service started successfully.");
            //TimerElapsed(this, null);
        }
        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            LogInfo("🔄 Fetching attendance records...");

            var machines = _dbHelper.GetActiveMachines();

            foreach (var machine in machines)
            {
                DateTime fetchStartTime = DateTime.Now;
                DateTime? fetchEndTime = null;
                try
                {                   
                    LogInfo($"🔌 Connecting to machine {machine.IpAddress}:{machine.Port}...");                 
                    bool isConnected = _zkTecoHelper.Connect(machine.IpAddress, machine.Port);
                    LogInfo($"🔍 Connection result for {machine.IpAddress}: {isConnected}");
                    if (isConnected)
                    {
                        LogInfo($"✅ Successfully connected to {machine.IpAddress}. Fetching records...");

                        List<HRSwapRecord> records;
                        if (machine.IsFetchAll)
                        {
                            // Fetch all records
                            records = _zkTecoHelper.GetAttendanceRecords(machine.Id, machine.IpAddress, machine.Port.ToString());
                        }
                        else
                        {
                            // Fetch only new records
                            //DateTime? lastInsertedRecordTime = _dbHelper.GetLastRecordCreationTimestamp(machine.Id, machine.IpAddress);                          
                            DateTime? lastInsertedRecordTime = _dbHelper.GetLastAttendanceTimestamp(machine.Id, machine.IpAddress);                          
                            if (lastInsertedRecordTime.HasValue)
                            {
                                records = _zkTecoHelper.GetNewAttendanceRecords(machine.Id, machine.IpAddress, machine.Port.ToString(), lastInsertedRecordTime.Value);
                            }
                            else
                            {
                                records = _zkTecoHelper.GetAttendanceRecords(machine.Id, machine.IpAddress, machine.Port.ToString());
                            }
                        }
                        LogInfo($"📊 Total records fetched from {machine.IpAddress}: {records.Count}");
                        if (records.Count > 0)
                        {
                            _dbHelper.InsertAttendanceRecords(machine.Id, records);
                            fetchEndTime = DateTime.Now;
                            _dbHelper.LogMachineSync(machine.Id,machine.IpAddress, "Success", records.Count, "Fetched successfully.", fetchStartTime, fetchEndTime);
                            LogInfo($"✅ Success: {records.Count} records fetched from {machine.IpAddress}.");
                        }
                        else
                        {
                            fetchEndTime = DateTime.Now;
                            _dbHelper.LogMachineSync(machine.Id, machine.IpAddress, "Success", 0, "No new records found.", fetchStartTime, fetchEndTime);
                            LogInfo($"✅ Success: No new records found from {machine.IpAddress}.");
                        }

                        _zkTecoHelper.Disconnect();
                    }
                    if (!isConnected)
                    {
                        LogError($"⚠️ Connection to {machine.IpAddress}:{machine.Port} failed. Possible reasons: \n" +
                            "1. Device is offline or unreachable.\n" +
                            "2. Port 4370 is blocked by firewall.\n" +
                            "3. Another system is already connected to the device.\n" +
                            "4. SDK connection is not allowed in device settings.\n" +
                            "5. Network issues.");                       
                        _dbHelper.LogMachineSync(machine.Id, machine.IpAddress, "Failed", 0, "Connection failed.", fetchStartTime, null);
                    }
                }
                catch (Exception ex)
                {
                    _dbHelper.LogMachineSync(machine.Id, machine.IpAddress, "Error", 0, $"Exception: {ex.Message}", fetchStartTime, null);
                    LogError($"❌ Error processing machine {machine.IpAddress}: {ex.Message}");
                }
            }
        }
        
        protected override void OnStop()
        {
            _timer?.Stop();
            LogInfo("🛑 Service stopped.");
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
                try
                {
                    if (!EventLog.SourceExists(ServiceName))
                    {
                        EventLog.CreateEventSource(ServiceName, "Application");
                    }
                    EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Information);
                }
                catch (Exception evEx)
                {
                    string eventError = $"⚠️ Failed to write to Event Log: {evEx.Message}";
                    File.AppendAllText(logPath, $"{DateTime.Now}: {eventError}\n");
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
                EventLog.WriteEntry(ServiceName, message, EventLogEntryType.Error);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to log error: {ex.Message}");
            }
        }

    }
}
