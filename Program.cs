using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;

namespace ConsoleCovidExplorer
{
    class Program
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

        private static string GetPublishedData(string uri, string fileName)
        {
            var path = $@"{projectPath}{fileName}";
            Uri NYTimesUri = new Uri(uri);

            string eTag = CachedETagValue;

            HttpWebRequest myHttpWebRequest = (HttpWebRequest)HttpWebRequest.Create(NYTimesUri);

            myHttpWebRequest.Method = "GET";
            myHttpWebRequest.Headers.Add(HttpRequestHeader.IfNoneMatch, eTag);

            try
            {
                HttpWebResponse myHttpWebResponse = (HttpWebResponse)myHttpWebRequest.GetResponse();

                using (Stream output = File.OpenWrite(path))
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
                while (exc != null)
                {
                    Console.WriteLine($"{exc.GetType().Name} : {exc.Message}");
                    exc = exc.InnerException;
                }
            }

            return path;
        }

        static List<DataPoint> dataPoints = new List<DataPoint>();
        static string projectPath = string.Empty;
        static string cacheFileName = string.Empty;
        static string sourceDataUri = string.Empty;
        static string[] statesToQuery = { "Kansas" };

        public static int Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            projectPath = config["userPrefs:outputDirectory"];
            if (!Directory.Exists(projectPath))
            {
                Directory.CreateDirectory(projectPath);
            }

            sourceDataUri = config["userPrefs:remoteDataSource"];
            cacheFileName = config["userPrefs:outputFileName"];
            string dataFilePath = GetPublishedData(sourceDataUri,cacheFileName);

            Console.WriteLine($"Cached data file date = {new FileInfo(dataFilePath).LastAccessTime}");
            var lines = File.ReadAllLines(dataFilePath);

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

            List<DataPoint> queryPoints = null;
            if (statesToQuery.Any())
            {
                queryPoints = dataPoints.Where(d => statesToQuery.Contains(d.State)).ToList<DataPoint>();
            }
            else
            {
                queryPoints = dataPoints;
            }

            var localData = queryPoints
                .GroupBy(d => new { d.State, d.County })
                .Select(d => d.OrderByDescending(x => x.Date).First());


            foreach (var r in localData.OrderBy(rr => rr.State).ThenByDescending(rr => rr.Cases))
            {
                System.Console.WriteLine($"{r.Date,-12:MM/dd/yy} {r.State,-40} {r.County,-40} {r.Cases,-20} {r.Deaths,-20}");
            }

            System.Console.Write($"Done. Press any key to continue.");
            Console.ReadKey(true);

            return 0;


        }
    }
}
