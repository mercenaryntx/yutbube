using System;
using System.Collections.Generic;
using Microsoft.ApplicationInsights;
using Microsoft.WindowsAzure.Storage.Blob.Protocol;

namespace Yutbube.Conversion
{
    public class AppInsightsClient
    {
        private readonly TelemetryClient _telemetryClient;

        public AppInsightsClient()
        {
            var key = Environment.GetEnvironmentVariable("APPINSIGHTS_INSTRUMENTATIONKEY", EnvironmentVariableTarget.Process);
            if (!string.IsNullOrEmpty(key))
                _telemetryClient = new TelemetryClient
                {
                    InstrumentationKey = key
                };
        }

        public AppInsightsClient SetOperation(string id, string name)
        {
            if (_telemetryClient == null) return this;
            _telemetryClient.Context.Operation.Id = id;
            _telemetryClient.Context.Operation.Name = name;
            return this;
        }

        public AppInsightsClient SetSessionId(string id)
        {
            if (_telemetryClient == null) return this;
            _telemetryClient.Context.Session.Id = id;
            return this;
        }

        public AppInsightsClient TrackException(Exception ex, IDictionary<string, string> properties = null)
        {
            if (_telemetryClient == null) return this;
            _telemetryClient.TrackException(ex, properties);
            return this;
        }

        public AppInsightsClient TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            if (_telemetryClient == null) return this;
            _telemetryClient.TrackEvent(eventName, properties, metrics);
            return this;
        }
    }
}