// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    /// <summary>
    /// Provides <see cref="PackageValidationSet"/> instances.
    /// </summary>
    public interface IValidationSetProvider<T> where T : IEntity
    {
        /// <summary>
        /// Reads validation set data from storage, creates one if did not exist in storage
        /// </summary>
        /// <param name="validationTrackingId">Validation tracking id</param>
        /// <param name="package">Package details from Gallery DB</param>
        /// <returns><see cref="PackageValidationSet"/> object with information about
        /// requested <paramref name="validationTrackingId"/>. Null if no further processing
        /// should be made (e.g. duplicate validation request was detected).
        /// </returns>
        //Task<PackageValidationSet> TryGetOrCreateValidationSetAsync(Guid validationTrackingId, Package package);
        Task<PackageValidationSet> TryGetOrCreateValidationSetAsync(PackageValidationMessageData message, IValidatingEntity<T> package);
    }
}
