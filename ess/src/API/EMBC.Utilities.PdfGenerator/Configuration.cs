﻿// -------------------------------------------------------------------------
//  Copyright © 2021 Province of British Columbia
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  https://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
// -------------------------------------------------------------------------

using System;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using EMBC.PDFGenerator;
using EMBC.Utilities.Configuration;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EMBC.ESS.Utilities.PdfGenerator
{
    public class Configuration : IConfigureComponentServices
    {
        public void ConfigureServices(ConfigurationServices configurationServices)
        {
            configurationServices.Services.AddGrpc(opts =>
            {
                opts.EnableDetailedErrors = configurationServices.Environment.IsDevelopment();
            });
            var pdfGeneratorUrl = configurationServices.Configuration.GetValue<Uri>("pdfGenerator:url");
            var allowUntrustedCertificates = configurationServices.Configuration.GetValue("pdfGenerator:allowInvalidServerCertificate", false);
            if (pdfGeneratorUrl == null)
            {
                configurationServices.Logger.LogWarning("PdfGenerator:url env var is not set, PdfGenerator will not be available");
                return;
            }

            var httpClientBuilder = configurationServices.Services.AddGrpcClient<Generator.GeneratorClient>(opts =>
            {
                opts.Address = pdfGeneratorUrl;
            }).ConfigureChannel(opts =>
            {
                if (pdfGeneratorUrl.Scheme == "dns")
                {
                    opts.Credentials = ChannelCredentials.SecureSsl;
                }
            }).ConfigurePrimaryHttpMessageHandler(() =>
            {
                if (!allowUntrustedCertificates) return new SocketsHttpHandler();
                return new SocketsHttpHandler
                {
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = DangerousCertificationValidation
                    }
                };
            });
            configurationServices.Services.TryAddTransient<IPdfGenerator, PdfGenerator>();
        }

        private static bool DangerousCertificationValidation(
            object sender,
            X509Certificate? certificate,
            X509Chain? chain,
            SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }
}
