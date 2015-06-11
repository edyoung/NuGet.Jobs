﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Jobs.Common;

namespace Search.GenerateCuratedFeedReport
{
    internal class Job : JobBase
    {
        private const string GetCuratedPackagesScript = @"-- Work Service / Search.GenerateCuratedFeedReport 
         SELECT pr.[Id], cf.[Name] FROM [dbo].[PackageRegistrations] pr 
         Inner join CuratedPackages cp on cp.PackageRegistrationKey = pr.[Key]
         join CuratedFeeds cf on cp.[CuratedFeedKey] = cf.[Key]";

        public static readonly string DefaultContainerName = "ng-search-data";
        public static readonly string ReportName = "curatedfeeds.json";

        public SqlConnectionStringBuilder Source { get; set; }
        public CloudStorageAccount Destination { get; set; }
        public CloudBlobContainer DestinationContainer { get; set; }
        public string DestinationContainerName { get; set; }
        public string OutputDirectory { get; set; }

        public override async Task<bool> Run()
        {
            string destination = string.IsNullOrEmpty(OutputDirectory) ?
                (Destination.Credentials.AccountName + "/" + DestinationContainerName) :
                OutputDirectory;

            if (string.IsNullOrEmpty(destination))
            {
                throw new ArgumentException(Strings.WarehouseJob_NoDestinationAvailable);
            }

            Trace.TraceInformation(string.Format("Generating Curated feed report from {0}/{1} to {2}.", Source.DataSource, Source.InitialCatalog, destination));

            var curatedPackages = new CuratedPackages();

            using (var connection = new SqlConnection(Source.ConnectionString))
            {
                connection.Open();

                var command = new SqlCommand(GetCuratedPackagesScript, connection);
                command.CommandType = CommandType.Text;

                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    string packageRegistrationId = (string)reader.GetValue(0);
                    string feedName = (string)reader.GetValue(1);

                    curatedPackages[packageRegistrationId].Add(feedName);
                }
            }

            Trace.TraceInformation(String.Format("Gathered {0} rows of data.", curatedPackages.Count));

            // Create JArray out of the dictionary.
            var curatedFeeds = new JArray();

            foreach (var packageId in curatedPackages.Keys)
            {
                var details = new JArray();
                details.Add(packageId);

                foreach (var feed in curatedPackages[packageId])
                {
                    var feedName = new JArray(feed);
                    details.Add(feedName);
                }

                curatedFeeds.Add(details);
            }

            await WriteReport(curatedFeeds.ToString(Formatting.None), ReportName, Formatting.None);

            return true;
        }

        protected async Task WriteReport(string report, string name, Formatting formatting)
        {
            if (!string.IsNullOrEmpty(OutputDirectory))
            {
                await WriteToFile(report, name);
            }
            else
            {
                await DestinationContainer.CreateIfNotExistsAsync();
                await WriteToBlob(report, name);
            }
        }

        public override bool Init(IDictionary<string, string> jobArgsDictionary)
        {
            Source =
                new SqlConnectionStringBuilder(
                    JobConfigManager.GetArgument(jobArgsDictionary,
                        JobArgumentNames.SourceDatabase,
                        EnvironmentVariableKeys.SqlGallery));

            OutputDirectory = JobConfigManager.GetArgument(jobArgsDictionary,
                       JobArgumentNames.OutputDirectory);

            if (string.IsNullOrEmpty(OutputDirectory))
            {
                Destination = CloudStorageAccount.Parse(
                                           JobConfigManager.GetArgument(jobArgsDictionary,
                                               JobArgumentNames.PrimaryDestination, EnvironmentVariableKeys.StoragePrimary));

                DestinationContainerName = JobConfigManager.TryGetArgument(jobArgsDictionary, JobArgumentNames.DestinationContainerName) ?? DefaultContainerName;

                DestinationContainer = Destination.CreateCloudBlobClient().GetContainerReference(DestinationContainerName);
            }

            return true;
        }

        private async Task WriteToFile(string report, string name)
        {
            string fullPath = Path.Combine(OutputDirectory, name);
            string parentDir = Path.GetDirectoryName(fullPath);
            Trace.TraceInformation(String.Format("Writing report to {0}", fullPath));

            if (!Directory.Exists(parentDir))
            {
                Directory.CreateDirectory(parentDir);
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            using (var writer = new StreamWriter(File.OpenWrite(fullPath)))
            {
                await writer.WriteAsync(report);
            }

            Trace.TraceInformation(String.Format("Wrote report to {0}", fullPath));
        }

        private async Task WriteToBlob(string report, string name)
        {
            var blob = DestinationContainer.GetBlockBlobReference(name);
            Trace.TraceInformation(String.Format("Writing report to {0}", blob.Uri.AbsoluteUri));

            blob.Properties.ContentType = "application/json";
            await blob.UploadTextAsync(report);

            Trace.TraceInformation(String.Format("Wrote report to {0}", blob.Uri.AbsoluteUri));
        }
    }
}

