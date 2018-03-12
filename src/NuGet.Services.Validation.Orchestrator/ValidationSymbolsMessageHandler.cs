// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NuGet.Services.ServiceBus;
using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public class ValidatedPackage 
    {
       public string Id { get; set; }
       public string NormalizedVersion { get; set; }

        public int Key { get; set; }

        public DateTime Created { get; set; } 

        public ValidatedPackage(string id, string vers, int key, DateTime created)
        {
            Id = id;
            NormalizedVersion = vers;
            Key = key;
            Created = created;
        }

       public static ValidatedPackage CreateFrom(Package p)
        {
            return new ValidatedPackage(p.PackageRegistration.Id, p.NormalizedVersion, p.Key, p.Created);
        }
    }

    public class ValidationSymbolsMessageHandler : IMessageHandler<PackageValidationMessageData>
    {
        private readonly ICorePackageService _galleryPackageService;
        private readonly IValidationSetProvider _validationSetProvider;
        private readonly IValidationSetProcessor _validationSetProcessor;
        private readonly IValidationOutcomeProcessor _validationOutcomeProcessor;
        private readonly ILogger<ValidationMessageHandler> _logger;

        public ValidationSymbolsMessageHandler(
            ICorePackageService galleryPackageService,
            IValidationSetProvider validationSetProvider,
            IValidationSetProcessor validationSetProcessor,
            IValidationOutcomeProcessor validationOutcomeProcessor,
            ILogger<ValidationMessageHandler> logger)
        {
            _galleryPackageService = galleryPackageService ?? throw new ArgumentNullException(nameof(galleryPackageService));
            _validationSetProvider = validationSetProvider ?? throw new ArgumentNullException(nameof(validationSetProvider));
            _validationSetProcessor = validationSetProcessor ?? throw new ArgumentNullException(nameof(validationSetProcessor));
            _validationOutcomeProcessor = validationOutcomeProcessor ?? throw new ArgumentNullException(nameof(validationOutcomeProcessor));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> HandleAsync(PackageValidationMessageData message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            using (_logger.BeginScope("Handling message for {PackageId} {PackageVersion} validation set {ValidationSetId}",
                message.PackageId,
                message.PackageVersion,
                message.ValidationTrackingId))
            {
                var symbolPackage = _galleryPackageService.FindPackageByIdAndVersionStrict(message.PackageId, message.PackageVersion);

                if (symbolPackage == null)
                {
                    // no package in DB yet. Might have received message a bit early, need to retry later
                    _logger.LogInformation("Did not find information in DB for package {PackageId} {PackageVersion}",
                        message.PackageId,
                        message.PackageVersion);
                    return false;
                }

                var valSymbP = ValidatedPackage.CreateFrom(symbolPackage);
                var validationSet = await _validationSetProvider.TryGetOrCreateValidationSetAsync(message.ValidationTrackingId, valSymbP);

                if (validationSet == null)
                {
                    _logger.LogInformation("The validation request for {PackageId} {PackageVersion} validation set {ValidationSetId} is a duplicate. Discarding.",
                        message.PackageId,
                        message.PackageVersion,
                        message.ValidationTrackingId);
                    return true;
                }

                await _validationSetProcessor.ProcessValidationsAsync(validationSet, valSymbP);
                await _validationOutcomeProcessor.ProcessValidationOutcomeAsync(validationSet, symbolPackage);
            }
            return true;
        }
    }
}
