using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using AttendenceService.Data;

namespace AttendenceService
{
    public class HttpServer
    {
        private readonly AttendanceService _attendanceService;

        public HttpServer(AttendanceService attendanceService)
        {
            _attendanceService = attendanceService;
        }

        public async Task StartAsync()
        {
            HttpListener listener = new HttpListener();
            // Add both prefixes for the endpoints
            listener.Prefixes.Add("http://localhost:5000/api/employees/");
            listener.Prefixes.Add("http://localhost:5000/api/transfer/");
            listener.Start();

            while (true)
            {
                var context = await listener.GetContextAsync();
                _ = HandleRequestAsync(context); // Handle requests asynchronously
            }
        }
        private async Task HandleRequestAsync(HttpListenerContext context)
        {
            try
            {
                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/api/transfer/")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync();
                        var request = JsonConvert.DeserializeObject<MultipleTransferRequest>(json);
                        // Perform the transfer
                        var result = _attendanceService.TransferEmployees(request);
                        // Return response
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "application/json";
                        using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
                        {
                            await writer.WriteAsync(JsonConvert.SerializeObject(result));
                        }
                    }
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/api/employees/")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync();
                        var machineIPs = JsonConvert.DeserializeObject<List<string>>(json);
                        // Fetch employees for the provided IPs
                        var employees = _attendanceService.FetchEmployeesForSpecificIPs(machineIPs);
                        // Send the response back to the client
                        context.Response.StatusCode = (int)HttpStatusCode.OK;
                        context.Response.ContentType = "application/json";
                        using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
                        {
                            await writer.WriteAsync(JsonConvert.SerializeObject(new { employees }));
                        }
                    }
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            catch (Exception ex)
            {
                context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                context.Response.ContentType = "application/json";
                using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
                {
                    await writer.WriteAsync(JsonConvert.SerializeObject(new { error = ex.Message }));
                }
            }
            finally
            {
                context.Response.Close();
            }
        }
        public class MultipleTransferRequest
        {
            public string SourceIP { get; set; }
            public List<string> DestinationIPs { get; set; }
            public List<EmployeeTransfer> Employees { get; set; }
            public bool TransferAllEmployees { get; set; }
            public bool TransferAllMachines { get; set; }
            public string UserId { get; set; }
        }
        public class EmployeeTransfer
        {
            public string EmpNo { get; set; }
            public string EmpName { get; set; }
        }
        public class TransferResult
        {
            public int SuccessCount { get; set; }
            public int FailCount { get; set; }
            public string Message { get; set; }
        }        
    }
}