﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// This interface manages the state of gallery artifacts: gallery DB and packages container.
    /// </summary>
    public interface IPackageStatusProcessor
    {
        Task SetPackageStatusAsync(
            Package package,
            PackageValidationSet validationSet,
            PackageStatus packageStatus);
    }
}