using AttendenceService.Data;
using AttendenceService.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Timers;

namespace AttendenceService
{
    //public partial class AttendanceService : ServiceBase
    public partial class AttendanceService : ServiceBase
    {
        private Timer _timer;
        private readonly DatabaseHelper _dbHelper = new DatabaseHelper();
        private readonly ZKTecoHelper _zkTecoHelper = new ZKTecoHelper();
        private List<TimeSpan> _runTimes;

        public AttendanceService()
        {
            ServiceName = "AttendenceService"; // Unique name for Windows Service
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
            try
            {
                // Read configuration from App.config
                int timerInterval = int.Parse(ConfigurationManager.AppSettings["TimerInterval"]);
                string[] runTimes = ConfigurationManager.AppSettings["RunTimes"].Split(',');
                // Parse run times
                _runTimes = runTimes.Select(rt => TimeSpan.TryParse(rt, out var time) ? time : (TimeSpan?)null).Where(t => t.HasValue).Select(t => t.Value).ToList();

                // Log initial settings
                LogInfo($"Service started with Timer Interval: {timerInterval} ms");
                LogInfo($"Run Times: {string.Join(", ", _runTimes)}");

                // Set up the timer
                _timer = new Timer(timerInterval);
                _timer.Elapsed += TimerElapsed;
                _timer.AutoReset = true;
                _timer.Enabled = true;

                if (Environment.UserInteractive)
                {
                    LogInfo("🛠️ Running in development mode. Fetching immediately...");
                    FetchAndProcessAttendance();
                }
                else
                {
                    LogInfo("🚀 Running in production mode. Service initialized.");
                }
            }
            catch (Exception ex)
            {
                LogError($"❌ Error during OnStart: {ex.Message}");
                Stop();
            }
        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                LogInfo($"🕒 Timer ticked at {DateTime.Now.TimeOfDay}");
                CheckAndRunService();
            }
            catch (Exception ex)
            {
                LogError($"❌ Timer execution error: {ex.Message}");
            }
        }

        private void CheckAndRunService()
        {
            try
            {
                TimeSpan now = DateTime.Now.TimeOfDay;
                LogInfo($"⏰ Current Time: {now}");
                foreach (var runTime in _runTimes)
                {
                    LogInfo($"🕒 Scheduled Time: {runTime}, Current Time: {now}");
                    // Allow a 1-minute window to trigger the task
                    if (now >= runTime && now < runTime.Add(TimeSpan.FromMinutes(1)))
                    {
                        LogInfo($"🔄 Executing Fetch at {runTime}...");
                        FetchAndProcessAttendance();
                        return; // Exit to avoid duplicate runs
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"❌ Error in CheckAndRunService: {ex.Message}");
            }
        }

        private void FetchAndProcessAttendance()
        {
            try
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
                                DateTime? lastInsertedRecordTime = _dbHelper.GetLastRecordCreationTimestamp(machine.Id, machine.IpAddress);
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
                                _dbHelper.LogMachineSync(machine.Id, machine.IpAddress, "Success", records.Count, "Fetched successfully.", fetchStartTime, fetchEndTime);
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
            catch (Exception ex)
            {
                LogError($"❌ Unhandled exception in TimerElapsed: {ex.Message}");
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