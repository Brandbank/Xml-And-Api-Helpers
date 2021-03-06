﻿using Brandbank.Api.Clients;
using Brandbank.Api.ExtractData;
using Brandbank.Xml.Helpers;
using Brandbank.Xml.Models.Message;
using Brandbank.Xml.Logging;
using System;
using System.ServiceModel;
using System.Xml;
using System.Xml.Schema;

namespace Brandbank.Api
{
    public class GetUnsentApi : IGetUnsentDownloader
    {
        private readonly Guid _authGuid;
        private readonly string _endpointAddress;
        private readonly string _schema;
        private readonly string _schemaNamespace;
        private readonly ILogger<IGetUnsentClient> _logger;
        private readonly ValidationEventHandler _validationEventHandler;

        public GetUnsentApi(Guid authGuid, string endpointAddress, string schema, string schemaNamespace, ILogger<IGetUnsentClient> logger, ValidationEventHandler validationEventHandler)
        {
            _authGuid = authGuid;
            _endpointAddress = endpointAddress;
            _schema = schema;
            _schemaNamespace = schemaNamespace;
            _logger = logger;
            _validationEventHandler = validationEventHandler;
        }

        public void GetUnsent(Func<MessageType, IBrandbankMessageSummary> productProcessor)
        {
            var client = new DataExtractSoapClient(BrandbankHttpsBinding("Data ExtractSoap"), BrandbankEndpointAddress(_endpointAddress));
            using (var unsentClient = new GetUnsentClientLogger(_logger, new GetUnsentClient(_authGuid, client)))
                GetUnsent(productProcessor, unsentClient);
        }

        private void GetUnsent(Func<MessageType, IBrandbankMessageSummary> productProcessor, IGetUnsentClient unsentClient)
        {
            while (
                unsentClient.GetUnsentProductData()
                    .ValidateXml(_schema, _schemaNamespace, _validationEventHandler)
                    .ConvertTo<MessageType>()
                    .Then(productProcessor)
                    .Then(unsentClient.AcknowledgeMessage)
                    .MessageHadProducts
                );
        }

        private BasicHttpsBinding BrandbankHttpsBinding(string name)
        {
            return new BasicHttpsBinding(BasicHttpsSecurityMode.Transport)
            {
                Name = name,
                MaxReceivedMessageSize = int.MaxValue,
                MaxBufferPoolSize = int.MaxValue,
                ReaderQuotas = new XmlDictionaryReaderQuotas
                {
                    MaxArrayLength = int.MaxValue,
                    MaxStringContentLength = int.MaxValue
                },
                Security = new BasicHttpsSecurity
                {
                    Mode = BasicHttpsSecurityMode.Transport
                }
            };
        }

        private EndpointAddress BrandbankEndpointAddress(string endpointAddress)
        {
            return new EndpointAddressBuilder { Uri = new Uri(endpointAddress) }.ToEndpointAddress();
        }
    }
}
