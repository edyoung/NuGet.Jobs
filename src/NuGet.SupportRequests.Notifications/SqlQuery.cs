﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.SupportRequests.Notifications
{
    internal static class SqlQuery
    {
        internal static string GetUnresolvedIssues(string onCallPagerDutyUserName = null)
        {
            // If I.[CreatedBy] IS NULL, then the user data was deleted and the support request issue should be ignored.
            var query =
                "SELECT I.[CreatedBy], I.[CreatedDate], I.[PackageId], I.[PackageVersion], I.[OwnerEmail], I.[Reason], I.[PackageRegistrationKey], ISNULL(A.[PagerDutyUsername], \'Unassigned\') AS \'AdminPagerDutyUsername\', ISNULL(A.[GalleryUsername], \'Unassigned\') AS \'AdminGalleryUsername\', I.[IssueStatusId] AS \'IssueStatus\' FROM [dbo].[Issues] AS I (NOLOCK) LEFT OUTER JOIN [dbo].[Admins] AS A (NOLOCK) ON I.[AssignedToId] = A.[Key] WHERE I.[IssueStatusId] <> 3 AND I.[CreatedBy] IS NOT NULL ";

            if (!string.IsNullOrEmpty(onCallPagerDutyUserName))
            {
                query += "AND (I.[AssignedToId] IS NULL OR A.[PagerDutyUserName] = @pagerDutyUserName) ";
            }

            query += "ORDER BY I.[CreatedDate] ASC";

            return query;
        }

        internal const string GetTopSupportRequestReasonsInPeriod =
            "SELECT COUNT(I.[Key]) AS \'IssueCount\', I.[Reason] FROM [dbo].[Issues] AS I (NOLOCK) WHERE I.[CreatedDate] BETWEEN @startDate AND @endDate GROUP BY I.[Reason] ORDER BY [IssueCount] DESC";

        internal const string GetIssueCountCreatedInPeriod =
            "SELECT COUNT(I.[Key]) FROM [dbo].[Issues] AS I (NOLOCK) WHERE I.[CreatedDate] BETWEEN @startDate AND @endDate";

        internal const string GetIssueCountClosedInPeriod =
            "SELECT COUNT(I.[Key]) FROM [dbo].[Issues] AS I (NOLOCK) INNER JOIN [dbo].[History] AS H (NOLOCK) ON I.[Key] = H.[IssueId] WHERE I.[IssueStatusId] = 3 AND H.[IssueStatusId] = 3 AND H.[EntryDate] BETWEEN @startDate AND @endDate";

        internal const string GetAverageTimeToResolutionInPeriod =
            "SELECT CAST(AVG(CAST(T.[ResolutionTime] AS FLOAT)) AS DATETIME) AS \'AvgResolutionTime\' FROM (SELECT MAX(H.[EntryDate]) - I.[CreatedDate] AS \'ResolutionTime\' FROM [dbo].[Issues] AS I (NOLOCK) INNER JOIN[dbo].[History] AS H (NOLOCK) ON I.[Key] = H.[IssueId]WHERE I.[IssueStatusId] = 3 AND H.[IssueStatusId] = 3 AND I.[CreatedDate] BETWEEN @startDate AND @endDate GROUP BY I.[Key], I.[CreatedDate]) AS T";
    }
}