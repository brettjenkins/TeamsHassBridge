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
        private const int TeamsIdleTimeSecs = 360;
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
            else if (line.Contains("Machine is unlocked"))
                status.TeamsIdle = false;
            else if (line.Contains("Machine is locked"))
                status.TeamsIdle = true;
            else if (line.Contains("Machine has been idle for"))
            {
                var regex = new Regex("Machine has been idle for ([0-9]*(\\.[0-9]*)?) seconds");
                var match = regex.Match(line);
                if (match.Success)
                {
                    var secondsIdle = double.Parse(match.Groups[1].Value);
                    Console.WriteLine($"Idle for {secondsIdle}");
                    status.TeamsIdle = secondsIdle > TeamsIdleTimeSecs;
                }
            }
                
            /* The Teams log doesn't report when the status has gone idle, annoyingly, and it's not documented by MS but from observing the teams idle logic seems to be as thus:
               Note as well I'm saying Idle/Not-Idle because the status returns to the last set status - this is why I set a bool for idle and not change the status,
               because that status will be restored when not idle
                * IF UNLOCKED:
                    * Teams seems to check idle time every few minutes (between 2-5 minutes each check in my testing), so there's a bit of a lag depending on when the check is
                    * After teams has realised that the PC has been idle for more than 360 seconds (6 minutes), Teams will report the user as Idle
                    * After teams has realised that the PC has been idle for less than 360 seconds, if idle Teams will report the user as Not-Idle
                    * (this isn't instant as it depends on the idle check that teams runs every few minutes)
                * LOCKING the PC will instantly set the status to Idle
                * UNLOCKING the PC will instantly set the status to Not-Idle
             
             */
            
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
                if (!teamsStatus.TeamsOnCall.HasValue)
                    teamsStatus.TeamsOnCall = LastStatus.TeamsOnCall;

                if (!teamsStatus.TeamsState.HasValue)
                    teamsStatus.TeamsState = LastStatus.TeamsState;

                if (!teamsStatus.TeamsUnread.HasValue)
                    teamsStatus.TeamsUnread = LastStatus.TeamsUnread;

                if (!teamsStatus.TeamsIdle.HasValue)
                    teamsStatus.TeamsIdle = LastStatus.TeamsIdle;

                if (!LastStatus.Equals(teamsStatus) || force)
                {
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
