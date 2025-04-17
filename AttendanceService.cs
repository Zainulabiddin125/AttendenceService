using AttendenceService.Data;
using AttendenceService.Services;
//using Serilog.Filters;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.ServiceProcess;
using System.Text;
using System.Timers;
using zkemkeeper;
using static AttendenceService.HttpServer;
using static AttendenceService.Services.ZKTecoHelper;

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
                //LogInfo($"Service started with Timer Interval: {timerInterval} ms");
                //LogInfo($"Run Times: {string.Join(", ", _runTimes)}");

                // Set up the timer
                _timer = new Timer(timerInterval);
                _timer.Elapsed += TimerElapsed;
                _timer.AutoReset = true;
                _timer.Enabled = true;

                // Trigger the first fetch immediately
                LogInfo("🛠️ Running initial fetch...");
                FetchAndProcessAttendance();

                // Start the HTTP server
                var httpServer = new HttpServer(this);
                _ = httpServer.StartAsync();
                LogInfo("HTTP server started successfully.");
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
                //LogInfo($"🕒 Timer ticked at {DateTime.Now.TimeOfDay}");
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
                //LogInfo($"⏰ Current Time: {now}");
                foreach (var runTime in _runTimes)
                {
                    //LogInfo($"🕒 Scheduled Time: {runTime}, Current Time: {now}");
                    //// Allow a 1-minute window to trigger the task
                    if (now >= runTime && now < runTime.Add(TimeSpan.FromMinutes(1)))
                    {
                        //LogInfo($"🔄 Executing Fetch at {runTime}...");
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
                //LogInfo("🔄 Fetching attendance records...");

                var machines = _dbHelper.GetActiveMachines();
                LogInfo($"📋 Found {machines.Count} active machines.");
                foreach (var machine in machines)
                {
                    DateTime fetchStartTime = DateTime.Now;
                    DateTime? fetchEndTime = null;
                    try
                    {
                        //LogInfo($"🔌 Connecting to machine {machine.IpAddress}:{machine.Port}...");
                        bool isConnected = _zkTecoHelper.Connect(machine.IpAddress, machine.Port);
                        //LogInfo($"🔍 Connection result for {machine.IpAddress}: {isConnected}");
                        if (isConnected)
                        {
                            //LogInfo($"✅ Successfully connected to {machine.IpAddress}. Fetching records...");

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
                            //LogInfo($"📊 Total records fetched from {machine.IpAddress}: {records.Count}");
                            if (records.Count > 0)
                            {
                                _dbHelper.InsertAttendanceRecords(machine.Id, records);
                                fetchEndTime = DateTime.Now;
                                _dbHelper.LogMachineSync(machine.Id, machine.IpAddress, "Success", records.Count, "Fetched successfully.", fetchStartTime, fetchEndTime);
                                //LogInfo($"✅ Success: {records.Count} records fetched from {machine.IpAddress}.");
                            }
                            else
                            {
                                fetchEndTime = DateTime.Now;
                                _dbHelper.LogMachineSync(machine.Id, machine.IpAddress, "Success", 0, "No new records found.", fetchStartTime, fetchEndTime);
                                //LogInfo($"✅ Success: No new records found from {machine.IpAddress}.");
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


        //  transfer employees
        public TransferResult TransferEmployees(MultipleTransferRequest request)
        {
            var result = new TransferResult { SuccessCount = 0, FailCount = 0 };
            var transferRecords = new List<TransferRequest>();

            // Step 1: Get list of employees to transfer
            var employeesToTransfer = request.TransferAllEmployees
                ? FetchEmployeesForSpecificIPs(new List<string> { request.SourceIP })
                : request.Employees.Select(e => new Employee
                {
                    EmpNo = e.EmpNo,
                    EmpName = e.EmpName
                }).ToList();

            // Step 2: Get list of destination IPs
            var destinationMachines = request.TransferAllMachines
                ? _dbHelper.GetActiveMachines()
                    .Select(m => m.IpAddress)
                    .Where(ip => ip != request.SourceIP)
                    .ToList()
                : request.DestinationIPs;

            // Step 3: Connect to source machine once and fetch all employees
            if (!_zkTecoHelper.Connect(request.SourceIP, 4370))
            {
                result.FailCount = employeesToTransfer.Count * destinationMachines.Count;
                result.Message = $"[ERROR] Failed to connect to source machine {request.SourceIP}.";
                return result;
            }

            var sourceEmployees = _zkTecoHelper.GetEmployees(request.SourceIP, 4370);
            _zkTecoHelper.Disconnect();

            // Step 4: For each destination machine, connect once, transfer all selected employees, then disconnect
            foreach (var destIp in destinationMachines)
            {
                if (!_zkTecoHelper.Connect(destIp, 4370))
                {
                    result.FailCount += employeesToTransfer.Count;
                    result.Message += $"[ERROR] Failed to connect to destination machine {destIp}. ";
                    continue;
                }

                foreach (var emp in employeesToTransfer)
                {
                    var employeeToTransfer = sourceEmployees.FirstOrDefault(e => e.EmpNo == emp.EmpNo);
                    if (employeeToTransfer == null)
                    {
                        result.FailCount++;
                        result.Message += $"[ERROR] Employee {emp.EmpNo} not found on source machine {request.SourceIP}. ";
                        continue;
                    }

                    if (_zkTecoHelper.UploadEmployee(employeeToTransfer))
                    {
                        result.SuccessCount++;
                        transferRecords.Add(new TransferRequest
                        {
                            EmpNo = emp.EmpNo,
                            EmpName = emp.EmpName,
                            SourceIP = request.SourceIP,
                            DestinationIP = destIp
                        });
                    }
                    else
                    {
                        result.FailCount++;
                        result.Message += $"[ERROR] Failed to upload employee {emp.EmpNo} to {destIp}. ";
                    }
                }

                _zkTecoHelper.Disconnect();
            }

            // Step 5: Insert all successful transfers into DB
            foreach (var transfer in transferRecords)
            {
                try
                {
                    _dbHelper.InsertTransferRecord(transfer.EmpNo, transfer.EmpName, transfer.SourceIP, transfer.DestinationIP, request.UserId);
                }
                catch (Exception ex)
                {
                    result.Message += $"[ERROR] DB insert failed for employee {transfer.EmpNo} to {transfer.DestinationIP}: {ex.Message}. ";
                }
            }

            result.Message += $"[SUMMARY] Success: {result.SuccessCount}, Failed: {result.FailCount}";
            return result;
        }



        //public TransferResult TransferEmployees(MultipleTransferRequest request)
        //{
        //    var result = new TransferResult { SuccessCount = 0, FailCount = 0 };
        //    var transferRequests = new List<TransferRequest>();

        //    // Determine the list of employees to transfer
        //    var employeesToTransfer = request.TransferAllEmployees? FetchEmployeesForSpecificIPs(new List<string> { request.SourceIP }): request.Employees.Select(e => new Employee
        //        {
        //            EmpNo = e.EmpNo,
        //            EmpName = e.EmpName
        //        }).ToList();

        //    // Determine the list of destination machines
        //    var destinationMachines = request.TransferAllMachines? _dbHelper.GetActiveMachines().Select(m => m.IpAddress).Where(ip => ip != request.SourceIP).ToList(): request.DestinationIPs;

        //    // Prepare transfer requests
        //    foreach (var emp in employeesToTransfer)
        //    {
        //        foreach (var destIp in destinationMachines)
        //        {
        //            transferRequests.Add(new TransferRequest
        //            {
        //                SourceIP = request.SourceIP,
        //                DestinationIP = destIp,
        //                EmpNo = emp.EmpNo,
        //                EmpName = emp.EmpName
        //            });
        //        }
        //    }

        //    // Execute transfers
        //    foreach (var transfer in transferRequests)
        //    {
        //        try
        //        {
        //            if (!_zkTecoHelper.Connect(transfer.SourceIP, 4370))
        //            {
        //                result.FailCount++;
        //                result.Message += $"[ERROR] Failed to connect to source machine {transfer.SourceIP}. ";
        //                continue;
        //            }

        //            var employees = _zkTecoHelper.GetEmployees(transfer.SourceIP, 4370);
        //            var employeeToTransfer = employees.FirstOrDefault(e => e.EmpNo == transfer.EmpNo);

        //            if (employeeToTransfer == null)
        //            {
        //                result.FailCount++;
        //                result.Message += $"[ERROR] Employee {transfer.EmpNo} not found on source machine {transfer.SourceIP}. ";
        //                continue;
        //            }

        //            _zkTecoHelper.Disconnect();

        //            if (!_zkTecoHelper.Connect(transfer.DestinationIP, 4370))
        //            {
        //                result.FailCount++;
        //                result.Message += $"[ERROR] Failed to connect to destination machine {transfer.DestinationIP}. ";
        //                continue;
        //            }

        //            if (_zkTecoHelper.UploadEmployee(employeeToTransfer))
        //            {
        //                _dbHelper.InsertTransferRecord(transfer.EmpNo, transfer.EmpName, transfer.SourceIP, transfer.DestinationIP, request.UserId);
        //                result.SuccessCount++;
        //            }
        //            else
        //            {
        //                result.FailCount++;
        //                result.Message += $"[ERROR] Failed to upload employee {transfer.EmpNo} to {transfer.DestinationIP}. ";
        //            }
        //        }
        //        catch (Exception ex)
        //        {
        //            result.FailCount++;
        //            result.Message += $"[ERROR] Exception transferring {transfer.EmpNo}: {ex.Message}. ";
        //        }
        //        finally
        //        {
        //            _zkTecoHelper.Disconnect();
        //        }
        //    }

        //    result.Message += $"[SUMMARY] Success: {result.SuccessCount}, Failed: {result.FailCount}";
        //    return result;
        //}

        //  fetch employees
        public List<Employee> FetchEmployeesForSpecificIPs(List<string> machineIPs)
        {
            var employees = new List<Employee>();
            foreach (var ip in machineIPs)
            {
                int port = 4370;
                if (_zkTecoHelper.Connect(ip, port))
                {
                    employees.AddRange(_zkTecoHelper.GetEmployees(ip, port));
                    _zkTecoHelper.Disconnect();
                }
            }
            return employees;
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
        // Model for Transfer Request
        public class TransferRequest
        {
            public string SourceIP { get; set; }
            public string DestinationIP { get; set; }
            public string EmpNo { get; set; }
            public string EmpName { get; set; }
        }

        // Model for Transfer Result
        public class TransferResult
        {
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public string Message { get; set; } = string.Empty;
        }
    }
}