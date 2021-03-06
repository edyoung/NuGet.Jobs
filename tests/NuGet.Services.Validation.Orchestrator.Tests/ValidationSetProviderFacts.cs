﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NuGet.Services.Validation.Orchestrator.Telemetry;
using NuGetGallery;
using Xunit;

namespace NuGet.Services.Validation.Orchestrator.Tests
{
    public class ValidationSetProviderFacts
    {
        public Mock<IValidationStorageService> ValidationStorageMock { get; }
        public Mock<IValidationPackageFileService> PackageFileServiceMock { get; }
        public Mock<IValidatorProvider> ValidatorProvider { get; }
        public Mock<IOptionsSnapshot<ValidationConfiguration>> ConfigurationAccessorMock { get; }
        public Mock<ITelemetryService> TelemetryServiceMock { get; }
        public Mock<ILogger<ValidationSetProvider>> LoggerMock { get; }
        public ValidationConfiguration Configuration { get; }
        public string ETag { get; }
        public Package Package { get; }
        public PackageValidationSet ValidationSet { get; }

        [Fact]
        public async Task TriesToGetValidationSetFirst()
        {
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var set = await provider.TryGetOrCreateValidationSetAsync(ValidationSet.ValidationTrackingId, Package);

            ValidationStorageMock
                .Verify(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId), Times.Once());

            Assert.Same(ValidationSet, set);

            PackageFileServiceMock.Verify(
                x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            PackageFileServiceMock.Verify(
                x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            PackageFileServiceMock.Verify(
                x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>()),
                Times.Never);
            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(It.IsAny<TimeSpan>()),
                Times.Never);
        }

        [Fact]
        public async Task CopiesToValidationSetContainerBeforeAddingDbRecord()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem
                {
                    Name = validation1,
                    TrackAfter = TimeSpan.FromDays(1),
                    RequiredValidations = new List<string>{}
                }
            };

            Package.PackageStatusKey = PackageStatus.Available;

            var operations = new List<string>();

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageFileServiceMock
                .Setup(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .ReturnsAsync(ETag)
                .Callback<PackageValidationSet>(_ => operations.Add(nameof(IValidationPackageFileService.CopyPackageFileForValidationSetAsync)));
            PackageFileServiceMock
                .Setup(x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask)
                .Callback(() => operations.Add(nameof(IValidationPackageFileService.BackupPackageFileFromValidationSetPackageAsync)));
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(_ => operations.Add(nameof(IValidationStorageService.CreateValidationSetAsync)));

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();
            await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            Assert.Equal(new[]
            {
                nameof(IValidationPackageFileService.CopyPackageFileForValidationSetAsync),
                nameof(IValidationPackageFileService.BackupPackageFileFromValidationSetPackageAsync),
                nameof(IValidationStorageService.CreateValidationSetAsync),
            }, operations);
        }

        [Fact]
        public async Task DoesNotBackUpThePackageWhenThereAreNoValidators()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            ValidatorProvider
                .Setup(x => x.IsProcessor(validation1))
                .Returns(false);

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();

            var actual = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            PackageFileServiceMock.Verify(
                x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>()),
                Times.Never);
        }

        [Fact]
        public async Task CopiesPackageFromPackagesContainerWhenAvailable()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            Package.PackageStatusKey = PackageStatus.Available;
            
            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();

            var actual = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            PackageFileServiceMock.Verify(x => x.CopyPackageFileForValidationSetAsync(createdSet), Times.Once);
            PackageFileServiceMock.Verify(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);
            PackageFileServiceMock.Verify(x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            PackageFileServiceMock.Verify(x => x.BackupPackageFileFromValidationSetPackageAsync(Package, createdSet), Times.Once);
            Assert.Equal(ETag, actual.PackageETag);
        }

        [Theory]
        [InlineData(PackageStatus.Validating)]
        [InlineData(PackageStatus.FailedValidation)]
        public async Task CopiesPackageFromValidationContainerWhenNotAvailable(PackageStatus packageStatus)
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            Package.PackageStatusKey = packageStatus;

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            var provider = CreateProvider();

            var actual = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            PackageFileServiceMock.Verify(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            PackageFileServiceMock.Verify(x => x.CopyValidationPackageForValidationSetAsync(createdSet), Times.Once);
            PackageFileServiceMock.Verify(x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);
            PackageFileServiceMock.Verify(x => x.BackupPackageFileFromValidationSetPackageAsync(Package, createdSet), Times.Once);
            Assert.Null(actual.PackageETag);
        }

        [Fact]
        public async Task ThrowsIfPackageIdDoesNotMatchValidationSet()
        {
            ValidationSet.PackageId = string.Join("", ValidationSet.PackageId.Reverse());
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.TryGetOrCreateValidationSetAsync(ValidationSet.ValidationTrackingId, Package));
            Assert.Contains(ValidationSet.PackageId, ex.Message);
            Assert.Contains(Package.PackageRegistration.Id, ex.Message);
        }

        [Fact]
        public async Task ThrowsIfPackageVersionDoesNotMatchValidationSet()
        {
            ValidationSet.PackageNormalizedVersion = ValidationSet.PackageNormalizedVersion + ".42";
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.TryGetOrCreateValidationSetAsync(ValidationSet.ValidationTrackingId, Package));
            Assert.Contains(ValidationSet.PackageNormalizedVersion, ex.Message);
            Assert.Contains(Package.NormalizedVersion, ex.Message);
        }

        [Fact]
        public async Task ThrowsIfPackageKeyDoesNotMatchValidationSet()
        {
            ValidationSet.PackageKey += 1111;
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(ValidationSet.ValidationTrackingId))
                .ReturnsAsync(ValidationSet)
                .Verifiable();

            var provider = CreateProvider();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.TryGetOrCreateValidationSetAsync(ValidationSet.ValidationTrackingId, Package));
            Assert.Contains(ValidationSet.PackageKey.ToString(), ex.Message);
            Assert.Contains(Package.Key.ToString(), ex.Message);
        }

        [Fact]
        public async Task ProperlyConstructsValidationSet()
        {
            const string validation1 = "validation1";
            const string validation2 = "validation2";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ validation2 } },
                new ValidationConfigurationItem(){ Name = validation2, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), validationTrackingId))
                .ReturnsAsync(false);

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(1);

            var provider = new ValidationSetProvider(
                ValidationStorageMock.Object,
                PackageFileServiceMock.Object,
                ValidatorProvider.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

            var returnedSet = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);
            var endOfCallTimestamp = DateTime.UtcNow;

            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);

            Assert.NotNull(returnedSet);
            Assert.NotNull(createdSet);
            Assert.Same(createdSet, returnedSet);
            Assert.Equal(Package.PackageRegistration.Id, createdSet.PackageId);
            Assert.Equal(Package.NormalizedVersion, createdSet.PackageNormalizedVersion);
            Assert.Equal(Package.Key, createdSet.PackageKey);
            Assert.Equal(validationTrackingId, createdSet.ValidationTrackingId);
            Assert.True(createdSet.Created.Kind == DateTimeKind.Utc);
            Assert.True(createdSet.Updated.Kind == DateTimeKind.Utc);

            var allowedTimeDifference = TimeSpan.FromSeconds(5);
            Assert.True(endOfCallTimestamp - createdSet.Created < allowedTimeDifference);
            Assert.True(endOfCallTimestamp - createdSet.Updated < allowedTimeDifference);
            Assert.All(createdSet.PackageValidations, v => Assert.Same(createdSet, v.PackageValidationSet));
            Assert.All(createdSet.PackageValidations, v => Assert.Equal(ValidationStatus.NotStarted, v.ValidationStatus));
            Assert.All(createdSet.PackageValidations, v => Assert.True(endOfCallTimestamp - v.ValidationStatusTimestamp < allowedTimeDifference));
            Assert.Contains(createdSet.PackageValidations, v => v.Type == validation1);
            Assert.Contains(createdSet.PackageValidations, v => v.Type == validation2);

            PackageFileServiceMock.Verify(
                x => x.CopyValidationPackageForValidationSetAsync(returnedSet),
                Times.Once);
            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(createdSet.Created - Package.Created),
                Times.Once);
        }

        [Fact]
        public async Task DoesNotEmitTelemetryIfMultipleValidationSetsExist()
        {
            const string validation1 = "validation1";
            Configuration.Validations = new List<ValidationConfigurationItem>
            {
                new ValidationConfigurationItem(){ Name = validation1, TrackAfter = TimeSpan.FromDays(1), RequiredValidations = new List<string>{ } }
            };

            Guid validationTrackingId = Guid.NewGuid();
            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null)
                .Verifiable();

            PackageValidationSet createdSet = null;
            ValidationStorageMock
                .Setup(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns<PackageValidationSet>(pvs => Task.FromResult(pvs))
                .Callback<PackageValidationSet>(pvs => createdSet = pvs)
                .Verifiable();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetCountAsync(It.IsAny<int>()))
                .ReturnsAsync(2);

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(It.IsAny<int>(), It.IsAny<TimeSpan>(), It.IsAny<Guid>()))
                .ReturnsAsync(false);

            var provider = new ValidationSetProvider(
                ValidationStorageMock.Object,
                PackageFileServiceMock.Object,
                ValidatorProvider.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);

            var returnedSet = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Once);

            TelemetryServiceMock.Verify(
                x => x.TrackDurationToValidationSetCreation(It.IsAny<TimeSpan>()),
                Times.Never);
        }

        [Fact]
        public async Task GetOrCreateValidationSetAsyncDoesNotCreateDuplicateValidationSet()
        {
            Guid validationTrackingId = Guid.NewGuid();

            ValidationStorageMock
                .Setup(vs => vs.GetValidationSetAsync(validationTrackingId))
                .ReturnsAsync((PackageValidationSet)null);

            ValidationStorageMock
                .Setup(vs => vs.OtherRecentValidationSetForPackageExists(
                    Package.Key,
                    It.IsAny<TimeSpan>(),
                    validationTrackingId))
                .ReturnsAsync(true);

            var provider = CreateProvider();
            var result = await provider.TryGetOrCreateValidationSetAsync(validationTrackingId, Package);

            Assert.Null(result);
            ValidationStorageMock
                .Verify(
                    vs => vs.OtherRecentValidationSetForPackageExists(
                        Package.Key,
                        It.IsAny<TimeSpan>(),
                        validationTrackingId),
                    Times.Once);
            ValidationStorageMock
                .Verify(vs => vs.CreateValidationSetAsync(It.IsAny<PackageValidationSet>()), Times.Never);
            PackageFileServiceMock.Verify(
                x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
            PackageFileServiceMock.Verify(
                x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()),
                Times.Never);
        }

        public ValidationSetProviderFacts()
        {
            ValidationStorageMock = new Mock<IValidationStorageService>(MockBehavior.Strict);
            PackageFileServiceMock = new Mock<IValidationPackageFileService>(MockBehavior.Strict);
            ValidatorProvider = new Mock<IValidatorProvider>(MockBehavior.Strict);
            ConfigurationAccessorMock = new Mock<IOptionsSnapshot<ValidationConfiguration>>();
            TelemetryServiceMock = new Mock<ITelemetryService>();
            LoggerMock = new Mock<ILogger<ValidationSetProvider>>();

            PackageFileServiceMock
                .Setup(x => x.CopyPackageFileForValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .ReturnsAsync(() => ETag);

            PackageFileServiceMock
                .Setup(x => x.CopyValidationPackageForValidationSetAsync(It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask);

            PackageFileServiceMock
                .Setup(x => x.BackupPackageFileFromValidationSetPackageAsync(It.IsAny<Package>(), It.IsAny<PackageValidationSet>()))
                .Returns(Task.CompletedTask);

            ValidatorProvider
                .Setup(x => x.IsProcessor(It.IsAny<string>()))
                .Returns(true);

            Configuration = new ValidationConfiguration();
            ConfigurationAccessorMock
                .SetupGet(ca => ca.Value)
                .Returns(() => Configuration);

            ETag = "\"some-etag\"";
            Package = new Package
            {
                PackageRegistration = new PackageRegistration { Id = "package1" },
                Version = "1.2.3.456",
                NormalizedVersion = "1.2.3",
                Key = 42,
                Created = new DateTime(2010, 1, 2, 8, 30, 0, DateTimeKind.Utc),
                PackageStatusKey = PackageStatus.Validating,
            };
            Package.PackageRegistration.Packages = new List<Package> { Package };

            ValidationSet = new PackageValidationSet
            {
                PackageId = Package.PackageRegistration.Id,
                PackageNormalizedVersion = Package.NormalizedVersion,
                PackageKey = Package.Key,
                ValidationTrackingId = Guid.NewGuid(),
            };
        }

        private ValidationSetProvider CreateProvider()
        {
            return new ValidationSetProvider(
                ValidationStorageMock.Object,
                PackageFileServiceMock.Object,
                ValidatorProvider.Object,
                ConfigurationAccessorMock.Object,
                TelemetryServiceMock.Object,
                LoggerMock.Object);
        }
    }
}
