﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Threading.Tasks;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using Test.Utility.Signing;
using Xunit;
using GeneralName = Org.BouncyCastle.Asn1.X509.GeneralName;
using BCCertificate = Org.BouncyCastle.X509.X509Certificate;

namespace Validation.PackageSigning.Core.Tests.Support
{
    /// <summary>
    /// This test fixture trusts the root certificate of checked in signed packages. This handles adding and removing
    /// the root certificate from the local machine trusted roots. Any tests with this fixture require admin elevation.
    /// </summary>
    public class CertificateIntegrationTestFixture : IDisposable
    {
        private readonly Lazy<Task<SigningTestServer>> _testServer;
        private readonly Lazy<Task<CertificateAuthority>> _rootCertificateAuthority;
        private readonly Lazy<Task<CertificateAuthority>> _certificateAuthority;
        private readonly Lazy<Task<TimestampService>> _timestampService;
        private readonly Lazy<Task<Uri>> _timestampServiceUrl;
        private readonly Lazy<Task<X509Certificate2>> _signingCertificate;
        private readonly Lazy<Task<string>> _signingCertificateThumbprint;
        private TrustedTestCert<X509Certificate2> _trustedRoot;
        private readonly DisposableList<IDisposable> _responders;

        public CertificateIntegrationTestFixture()
        {
            Assert.True(
                IsAdministrator(),
                "This test must be executing with administrator privileges since it installs a trusted root.");

            _testServer = new Lazy<Task<SigningTestServer>>(SigningTestServer.CreateAsync);
            _rootCertificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedRootCertificateAuthorityAsync);
            _certificateAuthority = new Lazy<Task<CertificateAuthority>>(CreateDefaultTrustedCertificateAuthorityAsync);
            _timestampService = new Lazy<Task<TimestampService>>(CreateDefaultTrustedTimestampServiceAsync);
            _timestampServiceUrl = new Lazy<Task<Uri>>(CreateDefaultTrustedTimestampServiceUrlAsync);
            _signingCertificate = new Lazy<Task<X509Certificate2>>(CreateDefaultTrustedSigningCertificateAsync);
            _signingCertificateThumbprint = new Lazy<Task<string>>(GetDefaultTrustedSigningCertificateThumbprintAsync);
            _responders = new DisposableList<IDisposable>();
        }

        public Task<SigningTestServer> GetTestServerAsync() => _testServer.Value;
        public Task<Uri> GetTimestampServiceUrlAsync() => _timestampServiceUrl.Value;

        public async Task<X509Certificate2> GetSigningCertificateAsync()
        {
            return new X509Certificate2(await _signingCertificate.Value);
        }

        public Task<string> GetSigningCertificateThumbprintAsync() => _signingCertificateThumbprint.Value;

        protected Task<CertificateAuthority> GetRootCertificateAuthority() => _rootCertificateAuthority.Value;
        protected Task<CertificateAuthority> GetCertificateAuthority() => _certificateAuthority.Value;
        protected DisposableList<IDisposable> GetResponders() => _responders;

        public void Dispose()
        {
            _trustedRoot?.Dispose();
            _responders.Dispose();

            if (_testServer.IsValueCreated)
            {
                _testServer.Value.Result.Dispose();
            }
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedRootCertificateAuthorityAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = CertificateAuthority.Create(testServer.Url);
            var rootCertificate = new X509Certificate2(rootCa.Certificate.GetEncoded());

            _trustedRoot = new TrustedTestCert<X509Certificate2>(
                rootCertificate,
                certificate => certificate,
                StoreName.Root,
                StoreLocation.LocalMachine);

            return rootCa;
        }

        private async Task<CertificateAuthority> CreateDefaultTrustedCertificateAuthorityAsync()
        {
            var testServer = await GetTestServerAsync();
            var rootCa = await GetRootCertificateAuthority();
            var intermediateCa = rootCa.CreateIntermediateCertificateAuthority();

            _responders.AddRange(testServer.RegisterResponders(intermediateCa));

            return intermediateCa;
        }

        private async Task<TimestampService> CreateDefaultTrustedTimestampServiceAsync()
        {
            var testServer = await GetTestServerAsync();
            var ca = await _certificateAuthority.Value;
            var timestampService = TimestampService.Create(ca);

            _responders.Add(testServer.RegisterResponder(timestampService));

            return timestampService;
        }

        private async Task<Uri> CreateDefaultTrustedTimestampServiceUrlAsync()
        {
            var timestampService = await _timestampService.Value;
            return timestampService.Url;
        }

        private async Task<X509Certificate2> CreateDefaultTrustedSigningCertificateAsync()
        {
            var ca = await _certificateAuthority.Value;
            return CreateSigningCertificate(ca);
        }

        public X509Certificate2 CreateSigningCertificate(CertificateAuthority ca)
        {
            void CustomizeAsSigningCertificate(X509V3CertificateGenerator generator)
            {
                generator.AddSigningEku();
                generator.AddAuthorityInfoAccess(ca, addOcsp: true, addCAIssuers: true);
            }

            return IssueCertificate(ca, "Signing", CustomizeAsSigningCertificate).certificate;
        }

        protected (BCCertificate publicCertificate, X509Certificate2 certificate) IssueCertificate(
            CertificateAuthority ca,
            string name,
            Action<X509V3CertificateGenerator> customizeCertificate)
        {
            var keyPair = SigningTestUtility.GenerateKeyPair(publicKeyLength: 2048);

            var publicCertificate = ca.IssueCertificate(new IssueCertificateOptions
            {
                CustomizeCertificate = customizeCertificate,
                NotAfter = DateTime.UtcNow.AddMinutes(10),
                NotBefore = DateTime.UtcNow.AddSeconds(-10),
                KeyPair = keyPair,

                SubjectName = new X509Name($"C=US,ST=WA,L=Redmond,O=NuGet,CN=NuGet Test ${name} Certificate ({Guid.NewGuid()})")
            });

            var certificate = new X509Certificate2(publicCertificate.GetEncoded());
            certificate.PrivateKey = DotNetUtilities.ToRSA(keyPair.Private as RsaPrivateCrtKeyParameters);

            return (publicCertificate, certificate);
        }

        private async Task<string> GetDefaultTrustedSigningCertificateThumbprintAsync()
        {
            var certificate = await GetSigningCertificateAsync();
            return certificate.ComputeSHA256Thumbprint();
        }

        /// <summary>
        /// Source: https://stackoverflow.com/a/11660205
        /// </summary>
        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
