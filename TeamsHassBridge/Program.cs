using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TeamsHassBridge
{
    public class Program
    {

        private static TeamsStatus LastStatus { get; set; } = new TeamsStatus();
        private static bool Startup = true;

        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting up and reading logs so far...");

            var autoResetEvent = new AutoResetEvent(false);
            var fileSystemWatcher = new FileSystemWatcher(".");
            fileSystemWatcher.Filter = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}\\Microsoft\\Teams\\logs.txt";
            fileSystemWatcher.EnableRaisingEvents = true;
            fileSystemWatcher.Changed += (s, e) => autoResetEvent.Set();

            var fileStream = new FileStream(fileSystemWatcher.Filter, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using (var streamReader = new StreamReader(fileStream))
            {
                var s = "";
                while (true)
                {
                    s = await streamReader.ReadLineAsync();
                    if (streamReader.EndOfStream)
                    {
                        if (Startup == true)
                        {
                            Startup = false;
                            EndStartup();
                        }
                    }

                    if (s != null)
                        await ReadLine(s);
                    else
                        autoResetEvent.WaitOne(1000);
                }
            }
        }

        private static async void EndStartup()
        {
            Console.WriteLine("Logs read - sending status, and starting real time monitoring");
            await SendUpdate(LastStatus, true);
        }

        private static async Task ReadLine(string line)
        {
            Debug.WriteLine(line);
            var status = new TeamsStatus();
            if (line.Contains("Sending Teams call status to SfB:TeamsPendingCall"))            
                status.TeamsOnCall = true;            
            else if (line.Contains("Sending Teams call status to SfB:TeamsActiveCall"))            
                status.TeamsOnCall = true;            
            else if (line.Contains("Sending Teams call status to SfB:TeamsNoCall"))            
                status.TeamsOnCall = false;            
            else if (line.Contains("Added NewActivity"))            
                status.TeamsUnread = true;            
            else if (line.Contains("Removing NewActivity"))            
                status.TeamsUnread = false;            
            
            if (line.Contains("-- info -- Added"))
            {
                var regex = new Regex("Added (.*?) \\(current state");
                var match = regex.Match(line);
                if (match.Success)
                {
                    var state = match.Groups[1];
                    switch (state.Value)
                    {
                        case "Available":
                            status.TeamsState = States.Available;
                            break;
                        case "Busy":
                            status.TeamsState = States.Busy;
                            break;
                        case "Away":
                            status.TeamsState = States.Away;
                            break;
                        case "BeRightBack":
                            status.TeamsState = States.Away;
                            break;
                        case "DoNotDisturb":
                            status.TeamsState = States.DoNotDisturb;
                            break;
                    }
                }
            }
            await SendUpdate(status);
        }

        private static async Task SendUpdate(TeamsStatus teamsStatus, bool force = false)
        {
            if (!teamsStatus.IsNull())
            {
                if (!LastStatus.Equals(teamsStatus) || force)
                {
                    if (teamsStatus.TeamsOnCall == null)
                        teamsStatus.TeamsOnCall = LastStatus.TeamsOnCall;

                    if (teamsStatus.TeamsState == null)
                        teamsStatus.TeamsState = LastStatus.TeamsState;

                    if (teamsStatus.TeamsUnread == null)
                        teamsStatus.TeamsUnread = LastStatus.TeamsUnread;

                    using (var client = new HttpClient())
                    {
                        LastStatus = teamsStatus;
                        if (!Startup)
                        {
                            var json = JsonConvert.SerializeObject(teamsStatus);
                            var stringContent = new StringContent(json, UnicodeEncoding.UTF8, "application/json");
                            Console.WriteLine($"Sending request - {json}");
                            var url = Properties.Settings.Default.Url;
                            await client.PutAsync(url, stringContent);
                        }
                    }
                }
            }
        }
    }
}
