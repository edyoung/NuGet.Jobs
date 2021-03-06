﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading.Tasks;

namespace NuGet.Services.Validation.PackageSigning.ProcessSignature
{
    /// <summary>
    /// Kicks off package signature verification.
    /// </summary>
    public interface IProcessSignatureEnqueuer
    {
        /// <summary>
        /// Kicks off the package verification process for the given request. Verification will begin when the
        /// <see cref="ValidationEntitiesContext"/> has a <see cref="ValidatorStatus"/> that matches the
        /// <see cref="IValidationRequest"/>'s validationId. Once verification completes, the <see cref="ValidatorStatus"/>'s
        /// State will be updated to "Succeeded" or "Failed".
        /// </summary>
        /// <param name="request">The request that details the package to be verified.</param>
        /// <returns>A task that will complete when the verification process has been queued.</returns>
        Task EnqueueVerificationAsync(IValidationRequest request);
    }
}
