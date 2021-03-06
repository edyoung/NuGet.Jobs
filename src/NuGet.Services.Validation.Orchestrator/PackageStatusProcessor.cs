﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using NuGetGallery.Packaging;

namespace NuGet.Services.Validation.Orchestrator
{
    public class PackageStatusProcessor : IPackageStatusProcessor
    {
        private readonly ICorePackageService _galleryPackageService;
        private readonly IValidationPackageFileService _packageFileService;
        private readonly IValidatorProvider _validatorProvider;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<PackageStatusProcessor> _logger;

        public PackageStatusProcessor(
            ICorePackageService galleryPackageService,
            IValidationPackageFileService packageFileService,
            IValidatorProvider validatorProvider,
            ITelemetryService telemetryService,
            ILogger<PackageStatusProcessor> logger)
        {
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _packageFileService = packageFileService ?? throw new ArgumentNullException(nameof(packageFileService));
            _validatorProvider = validatorProvider ?? throw new ArgumentNullException(nameof(validatorProvider));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task SetPackageStatusAsync(
            Package package,
            PackageValidationSet validationSet,
            PackageStatus packageStatus)
        {
            if (package == null)
            {
                throw new ArgumentNullException(nameof(package));
            }

            if (validationSet == null)
            {
                throw new ArgumentNullException(nameof(validationSet));
            }

            if (package.PackageStatusKey == PackageStatus.Deleted)
            {
                throw new ArgumentException(
                    $"A package in the {nameof(PackageStatus.Deleted)} state cannot be processed.",
                    nameof(package));
            }

            if (package.PackageStatusKey == PackageStatus.Available &&
                packageStatus == PackageStatus.FailedValidation)
            {
                throw new ArgumentException(
                    $"A package cannot transition from {nameof(PackageStatus.Available)} to {nameof(PackageStatus.FailedValidation)}.",
                    nameof(packageStatus));
            }

            switch (packageStatus)
            {
                case PackageStatus.Available:
                    return MakePackageAvailableAsync(package, validationSet);
                case PackageStatus.FailedValidation:
                    return MakePackageFailedValidationAsync(package, validationSet);
                default:
                    throw new ArgumentException(
                        $"A package can only transition to the {nameof(PackageStatus.Available)} or " +
                        $"{nameof(PackageStatus.FailedValidation)} states.", nameof(packageStatus));
            }
        }

        private async Task MakePackageFailedValidationAsync(Package package, PackageValidationSet validationSet)
        {
            var fromStatus = package.PackageStatusKey;

            await _galleryPackageService.UpdatePackageStatusAsync(package, PackageStatus.FailedValidation, commitChanges: true);

            if (fromStatus != PackageStatus.FailedValidation)
            {
                _telemetryService.TrackPackageStatusChange(fromStatus, PackageStatus.FailedValidation);
            }
        }

        private async Task MakePackageAvailableAsync(Package package, PackageValidationSet validationSet)
        {
            // 1) Operate on blob storage.
            var copied = await UpdatePublicPackageAsync(validationSet, package);

            // 2) Operate on the database.
            var fromStatus = await MarkPackageAsAvailableAsync(validationSet, package, copied);

            // 3) Emit telemetry and clean up.
            if (fromStatus != PackageStatus.Available)
            {
                _telemetryService.TrackPackageStatusChange(fromStatus, PackageStatus.Available);

                _logger.LogInformation("Deleting from the source for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);

                await _packageFileService.DeleteValidationPackageFileAsync(package.PackageRegistration.Id, package.Version);
            }

            // 4) Verify the package still exists (we've had bugs here before).
            if (package.PackageStatusKey == PackageStatus.Available
                && !await _packageFileService.DoesPackageFileExistAsync(package))
            {
                var validationPackageAvailable = await _packageFileService.DoesValidationPackageFileExistAsync(package);

                _logger.LogWarning("Package {PackageId} {PackageVersion} is marked as available, but does not exist " +
                    "in public container. Does package exist in validation container: {ExistsInValidation}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationPackageAvailable);

                // Report missing package, don't try to fix up anything. This shouldn't happen and needs an investigation.
                _telemetryService.TrackMissingNupkgForAvailablePackage(
                    validationSet.PackageId,
                    validationSet.PackageNormalizedVersion,
                    validationSet.ValidationTrackingId.ToString());
            }
        }

        private async Task<PackageStatus> MarkPackageAsAvailableAsync(PackageValidationSet validationSet, Package package, bool copied)
        {

            // Use whatever package made it into the packages container. This is what customers will consume so the DB
            // record must match.
            using (var packageStream = await _packageFileService.DownloadPackageFileToDiskAsync(package))
            {
                var stopwatch = Stopwatch.StartNew();
                var hash = CryptographyService.GenerateHash(packageStream, CoreConstants.Sha512HashAlgorithmId);
                _telemetryService.TrackDurationToHashPackage(
                    stopwatch.Elapsed,
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    CoreConstants.Sha512HashAlgorithmId,
                    packageStream.GetType().FullName);

                var streamMetadata = new PackageStreamMetadata
                {
                    Size = packageStream.Length,
                    Hash = hash,
                    HashAlgorithm = CoreConstants.Sha512HashAlgorithmId,
                };

                // We don't immediately commit here. Later, we will commit these changes as well as the new package
                // status as part of the same transaction.
                if (streamMetadata.Size != package.PackageFileSize
                    || streamMetadata.Hash != package.Hash
                    || streamMetadata.HashAlgorithm != package.HashAlgorithm)
                {
                    await _galleryPackageService.UpdatePackageStreamMetadataAsync(
                        package,
                        streamMetadata,
                        commitChanges: false);
                }
            }

            _logger.LogInformation("Marking package {PackageId} {PackageVersion}, validation set {ValidationSetId} as {PackageStatus} in DB",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId,
                PackageStatus.Available);

            var fromStatus = package.PackageStatusKey;

            try
            {
                // Make the package available and commit any other pending changes (e.g. updated hash).
                await _galleryPackageService.UpdatePackageStatusAsync(package, PackageStatus.Available, commitChanges: true);
            }
            catch (Exception e)
            {
                _logger.LogError(
                    Error.UpdatingPackageDbStatusFailed,
                    e,
                    "Failed to update package status in Gallery Db. Package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);

                // If this execution was not the one to copy the package, then don't delete the package on failure.
                // This prevents a missing passing in the (unlikely) case where two actors attempt the DB update, one
                // succeeds and one fails. We don't want an available package record with nothing in the packages
                // container!
                if (copied && fromStatus != PackageStatus.Available)
                {
                    await _packageFileService.DeletePackageFileAsync(package.PackageRegistration.Id, package.Version);
                }

                throw;
            }

            return fromStatus;
        }

        private async Task<bool> UpdatePublicPackageAsync(PackageValidationSet validationSet, Package package)
        {
            _logger.LogInformation("Copying .nupkg to public storage for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                package.PackageRegistration.Id,
                package.NormalizedVersion,
                validationSet.ValidationTrackingId);

            // If the validation set contains any processors, we must use the copy of the package that is specific to
            // this validation set. We can't use the original validation package because it does not have any of the
            // changes that the processors made. If the validation set package does not exist for some reason and there
            // are processors in the validation set, this indicates a bug and an exception will be thrown by the copy
            // operation below. This will cause the validation queue message to eventually dead-letter at which point
            // the on-call person should investigate.
            bool copied;
            if (validationSet.PackageValidations.Any(x => _validatorProvider.IsProcessor(x.Type)) ||
                await _packageFileService.DoesValidationSetPackageExistAsync(validationSet))
            {
                IAccessCondition destAccessCondition;

                // The package etag will be null if this validation set is expecting the package to not yet exist in
                // the packages container.
                if (validationSet.PackageETag == null)
                {
                    // This will fail with HTTP 409 if the package already exists. This means that another validation
                    // set has completed and moved the package into the Available state first, with different package
                    // content.
                    destAccessCondition = AccessConditionWrapper.GenerateIfNotExistsCondition();

                    _logger.LogInformation(
                        "Attempting to copy validation set {ValidationSetId} package {PackageId} {PackageVersion} to" +
                        " the packages container, assuming that the package does not already exist.",
                        validationSet.ValidationTrackingId,
                        package.PackageRegistration.Id,
                        package.NormalizedVersion);
                }
                else
                {
                    // This will fail with HTTP 412 if the package has been modified by another validation set. This
                    // would only happen if this validation set and another validation set are operating on a package
                    // already in the Available state.
                    destAccessCondition = AccessConditionWrapper.GenerateIfMatchCondition(validationSet.PackageETag);

                    _logger.LogInformation(
                        "Attempting to copy validation set {ValidationSetId} package {PackageId} {PackageVersion} to" +
                        " the packages container, assuming that the package has etag {PackageETag}.",
                        validationSet.ValidationTrackingId,
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        validationSet.PackageETag);
                }

                // Failures here should result in an unhandled exception. This means that this validation set has
                // modified the package but is unable to copy the modified package into the packages container because
                // another validation set completed first.
                await _packageFileService.CopyValidationSetPackageToPackageFileAsync(
                    validationSet,
                    destAccessCondition);

                copied = true;
            }
            else
            {
                _logger.LogInformation(
                    "The package specific to the validation set does not exist. Falling back to the validation " +
                    "container for package {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                    package.PackageRegistration.Id,
                    package.NormalizedVersion,
                    validationSet.ValidationTrackingId);

                try
                {
                    await _packageFileService.CopyValidationPackageToPackageFileAsync(
                        validationSet.PackageId,
                        validationSet.PackageNormalizedVersion);

                    copied = true;
                }
                catch (InvalidOperationException)
                {
                    // The package already exists in the packages container. This can happen if the DB commit below fails
                    // and this flow is retried or another validation set for the package completed first. Either way, we
                    // will later attempt to use the hash from the package in the packages container (the destination).
                    // In other words, we don't care which copy wins when copying from the validation package because
                    // we know the package has not been modified.
                    _logger.LogInformation(
                        "Package already exists in packages container for {PackageId} {PackageVersion}, validation set {ValidationSetId}",
                        package.PackageRegistration.Id,
                        package.NormalizedVersion,
                        validationSet.ValidationTrackingId);

                    copied = false;
                }
            }

            return copied;
        }
    }
}
