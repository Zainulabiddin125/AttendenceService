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

            //Console.WriteLine("Listening for requests at http://localhost:5000/api/employees/ and http://localhost:5000/api/transfer/...");

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
                //Console.WriteLine($"Received {context.Request.HttpMethod} request at {context.Request.Url.AbsolutePath}");

                if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/api/transfer/")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync();

                        //Console.WriteLine($"Received JSON payload: {json}");

                        var transferRequest = JsonConvert.DeserializeObject<TransferEmployeeRequest>(json);

                        //Console.WriteLine($"Source IP: {transferRequest.SourceIP}, Destination IP: {transferRequest.DestinationIP}, Employee: {transferRequest.EmpNo} - {transferRequest.EmpName}");

                        // Transfer the employee
                        var result = _attendanceService.TransferEmployee(
                            transferRequest.SourceIP,
                            transferRequest.DestinationIP,
                            transferRequest.EmpNo,
                            transferRequest.EmpName,
                            transferRequest.UserId
                        );

                        // Check if the result contains an error
                        if (result.Contains("[ERROR]"))
                        {
                            // Return an error response with an appropriate status code
                            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                            context.Response.ContentType = "application/json";
                            using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
                            {
                                await writer.WriteAsync(JsonConvert.SerializeObject(new { error = result }));
                            }
                        }
                        else
                        {
                            // Return a success response
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            context.Response.ContentType = "application/json";
                            using (var writer = new StreamWriter(context.Response.OutputStream, Encoding.UTF8))
                            {
                                await writer.WriteAsync(JsonConvert.SerializeObject(new { message = result }));
                            }
                        }                       
                    }
                }
                else if (context.Request.HttpMethod == "POST" && context.Request.Url.AbsolutePath == "/api/employees/")
                {
                    using (var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8))
                    {
                        var json = await reader.ReadToEndAsync();
                        //Console.WriteLine($"Received JSON payload: {json}");

                        var machineIPs = JsonConvert.DeserializeObject<List<string>>(json);
                        //Console.WriteLine($"Deserialized machine IPs: {string.Join(", ", machineIPs)}");

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
                //Console.WriteLine($"Error processing request: {ex}");
                //Console.WriteLine($"Stack Trace: {ex.StackTrace}");
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

        // Define the TransferEmployeeRequest model
        public class TransferEmployeeRequest
        {
            public string SourceIP { get; set; }
            public string DestinationIP { get; set; }
            public string EmpNo { get; set; } 
            public string EmpName { get; set; }
            public string UserId { get; set; }
        }
       
    }
}