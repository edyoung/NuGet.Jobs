﻿using NuGet.Jobs.Common;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Stats.Replicator
{
    class Program
    {
        static void Main(string[] args)
        {
            var job = new Job();
            JobRunner.Run(job, args).Wait();
        }
    }
}
