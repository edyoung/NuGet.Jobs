﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Jobs.Validation.PackageSigning.ProcessSignature
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var job = new Job();
            JobRunner.Run(job, args).GetAwaiter().GetResult();
        }
    }
}
