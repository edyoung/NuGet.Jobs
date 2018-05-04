﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGetGallery;

namespace NuGet.Services.Validation.Orchestrator
{
    public interface IMessageService<T> where T : IEntity
    {
        void SendPublishedMessage(T entity);
        void SendValidationFailedMessage(T entity);
        void SendSignedValidationFailedMessage(T entity);
        void SendValidationTakingTooLongMessage(T entity);
    }
}
