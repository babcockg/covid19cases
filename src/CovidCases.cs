using System;
using System.IO;
using System.Net;
using System.Timers;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace ConsoleCovidExplorer
{
    public static class CovidCases
    {
        private static string CachedETagValue
        {
            get
            {
                string eTag = string.Empty;
                if (File.Exists($"{projectPath}ETag.txt"))
                {
                    eTag = File.ReadAllText($"{projectPath}ETag.txt");
                }
                return eTag;
            }
        }

        public static IConfiguration Config { get => config; set => config = value; }

        private static string GetPublishedData(string uri, string fileName)
        {
            string returnValue = $@"{projectPath}{fileName}";
            Uri NYTimesUri = new Uri(uri);
            string eTag = CachedETagValue;

            HttpWebRequest myHttpWebRequest = (HttpWebRequest)HttpWebRequest.Create(NYTimesUri);

            myHttpWebRequest.Method = "GET";
            myHttpWebRequest.Headers.Add(HttpRequestHeader.IfNoneMatch, eTag);

            try
            {
                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();
                using (Stream output = File.OpenWrite(returnValue))
                using (Stream input = myHttpWebResponse.GetResponseStream())
                {
                    input.CopyTo(output);
                }

                File.WriteAllText($"{projectPath}ETag.txt", myHttpWebResponse.Headers[HttpResponseHeader.ETag]);
                Console.WriteLine($"{myHttpWebResponse.Headers[HttpResponseHeader.ETag]}");
                if (myHttpWebResponse.StatusCode == HttpStatusCode.OK)
                {
                    Console.WriteLine("\r\nRequest succeeded and the requested information is in the response , Description : {0}", myHttpWebResponse.StatusDescription);
                    Console.WriteLine($"HttpWebResponse.LastWriteTime = {myHttpWebResponse.LastModified}");
                }
            }
            catch (WebException exc)
            {
                HttpWebResponse response = (HttpWebResponse)exc.Response;
                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    Console.WriteLine($"Data file in cache is the latest available.");
                }
                else
                {
                    returnValue = string.Empty;
                    Exception e = exc;
                    while (e != null)
                    {
                        Console.WriteLine($"{e.GetType().Name} : {e.Message}");
                        e = e.InnerException;
                    }
                }
            }
            catch (Exception exc)
            {
                returnValue = string.Empty;
                while (exc != null)
                {
                    Console.WriteLine($"{exc.GetType().Name} : {exc.Message}");
                    exc = exc.InnerException;
                }
            }

            return returnValue;
        }

        public static void CreateConfiguration()
        {
            #region create config from appsettings.json
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            Config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();
            #endregion
        }

        public static void ReadConfiguration()
        {
            projectPath = Config["userPrefs:outputDirectory"];
            if (!Directory.Exists(projectPath))
            {
                Directory.CreateDirectory(projectPath);
            }
            sourceDataUri = Config["userPrefs:remoteDataSource"];
            cacheFileName = Config["userPrefs:outputFileName"];
        }

        public static void FetchData()
        {
            #region output the status of the cache file
            Console.WriteLine($"Cached data file date = {new FileInfo(dataFilePath).LastAccessTime}");
            var lines = File.ReadAllLines(dataFilePath);
            #endregion

            #region transform csv data into a List<DataPoint>
            foreach (var l in lines.Skip(1))
            {
                var values = l.Split(new[] { ',' });
                try
                {
                    dataPoints.Add(new DataPoint
                    {
                        Date = DateTime.Parse(values[0]),
                        County = values[1],
                        State = values[2],
                        Id = (string.IsNullOrEmpty(values[3]) ? 0 : int.Parse(values[3])),
                        Cases = int.Parse(values[4]),
                        Deaths = int.Parse(values[5])
                    });
                }
                catch (Exception exc)
                {
                    while (exc != null)
                    {
                        Console.WriteLine($"{exc.GetType().Name} : {exc.Message}");
                        exc = exc.InnerException;
                    }
                }
            }
            #endregion
        }

        public static void FilterDataPoints()
        {
            #region filter data by state if states are contained in appsettings.json
            try
            {
                stateFilter = Config.GetSection("userPrefs:stateFilter").Get<string[]>();
                if (stateFilter != null && stateFilter.Any())
                {
                    queryPoints = dataPoints.Where(d => stateFilter.Contains(d.State, StringComparer.OrdinalIgnoreCase)).ToList<DataPoint>();
                }
                else
                {
                    queryPoints = dataPoints;
                }
            }
            catch (Exception exc)
            {
                while (exc != null)
                {
                    System.Console.WriteLine($"{exc.GetType().Name} : {exc.Message}");
                    exc = exc.InnerException;
                }
            }

            #endregion
        }

        public static void DumpStateTotals()
        {
            totalPoints = new List<StateTotal>();

            string currentState = string.Empty;
            int totalCases = 0;
            int totalDeaths = 0;

            var localData = queryPoints
                .GroupBy(d => new { d.State, d.County })
                .Select(d => d.OrderByDescending(x => x.Date).First());

            foreach (var r in localData.OrderBy(q => q.State))
            {
                // First row of data? Initialize currentState variable
                if (string.IsNullOrEmpty(currentState))
                {
                    currentState = r.State;
                }

                if (!r.State.Equals(currentState))
                {
                    totalPoints.Add(new StateTotal
                    {
                        StateName = currentState,
                        TotalCases = totalCases,
                        TotalDeaths = totalDeaths
                    });
                    currentState = r.State;
                    totalCases = 0;
                    totalDeaths = 0;
                }
                totalCases += r.Cases;
                totalDeaths += r.Deaths;
            }
            totalPoints.Add(new StateTotal
            {
                StateName = currentState,
                TotalCases = totalCases,
                TotalDeaths = totalDeaths
            });


            System.Console.WriteLine($"{new string(' ', 8)}  {"State",-32} {"Cases",20} {"Deaths",20}");
            System.Console.WriteLine($"{Dashes(9)} {Dashes(32)} {Dashes(20)} {Dashes(20)}");
            int i = 0;
            foreach (var r in totalPoints.OrderByDescending(tp => tp.TotalCases))
            {
                System.Console.WriteLine($"{++i,8}) {r.StateName,-32} {r.TotalCases,20:#,##0} {r.TotalDeaths,20:#,##0}");
            }
            System.Console.WriteLine();
        }

        public static void DumpFilteredData()
        {

            #region group data by state, county ... output the results
            var localData = queryPoints
                .GroupBy(d => new { d.State, d.County })
                .Select(d => d.OrderByDescending(x => x.Date).First());
            int i = 0;

            System.Console.WriteLine($"{new string(' ', 9)} {"Date",-12} {"State",-32} {"County",-32} {"Cases",20} {"Deaths",20}");
            System.Console.WriteLine($"{Dashes(9)}{Dashes(12)} {Dashes(32)} {Dashes(32)} {Dashes(20)} {Dashes(20)}");
            foreach (var r in localData.OrderByDescending(rr => rr.Cases).ThenBy(rr => rr.State).ThenBy(rr => rr.County))
            {
                System.Console.WriteLine($"{++i,8}) {r.Date,-12:MM/dd/yy} {r.State,-32} {r.County,-32} {r.Cases,20:#,##0} {r.Deaths,20:#,##0}");
            }
            #endregion
        }
        public static string Dashes(int width, char pad = '-')
        {
            return new string(pad, width);
        }

        static List<DataPoint> dataPoints = new List<DataPoint>();
        static string projectPath = string.Empty;
        static string cacheFileName = string.Empty;
        static string sourceDataUri = string.Empty;
        static string dataFilePath = string.Empty;
        static List<DataPoint> queryPoints = null;
        static List<StateTotal> totalPoints = null;
        static IConfiguration config = null;
        static string[] stateFilter = null;

        public static int Main(string[] args)
        {
            TextWriter tw = null;
            if (args.Contains("-redirect"))
            {
                tw = Console.Out;
                Console.SetOut(new StreamWriter("output.txt"));
            }

            CreateConfiguration();
            ReadConfiguration();
            dataFilePath = GetPublishedData(sourceDataUri, cacheFileName);

            if (string.IsNullOrEmpty(dataFilePath))
            {
                System.Console.WriteLine($"Error copying remote data to [{dataFilePath}]");
                Environment.Exit(1);
            }

            FetchData();
            FilterDataPoints();

            if (stateFilter != null && stateFilter.Any())
            {
                System.Console.WriteLine($"States included: {string.Join(',', stateFilter)}\n");
            }
            DumpStateTotals();
            DumpFilteredData();

            if (tw != null)
            {
                Console.Out.Flush();
                Console.Out.Close();

                Console.SetOut(tw);
            }
            // Done!!
            if (!Console.IsOutputRedirected)
            {

                Timer exitTimer = new Timer(int.Parse(config["userPrefs:exitDelayInMilliseconds"]));

                exitTimer.Elapsed += (sender, e) => Environment.Exit(0);
                exitTimer.Enabled = true;
                exitTimer.Start();

                System.Console.Write($"Done. Press any key to continue. ");
                Console.Read();
            }

            return 0;


        }

        public static void TimerExit(object sender, ElapsedEventArgs e)
        {
            Environment.Exit(0);
        }
    }
}
