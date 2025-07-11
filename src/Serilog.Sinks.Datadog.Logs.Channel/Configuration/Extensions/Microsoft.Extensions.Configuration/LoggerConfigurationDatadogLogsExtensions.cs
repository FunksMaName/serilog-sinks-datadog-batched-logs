﻿// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2019 Datadog, Inc.

using Microsoft.Extensions.Configuration;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Formatting;
using Serilog.Sinks.Datadog.Logs;
using System;

namespace Serilog
{
    /// <summary>
    /// Adds the WriteTo.Datadog() extension method to <see cref="LoggerConfiguration"/>.
    /// </summary>
    public static class LoggerConfigurationDatadogExtensions
    {
        /// <summary>
        /// Adds a sink that sends log events to Datadog.
        /// </summary>
        /// <param name="loggerConfiguration">The logger configuration.</param>
        /// <param name="apiKey">Your Datadog API key.</param>
        /// <param name="source">The integration name.</param>
        /// <param name="service">The service name.</param>
        /// <param name="host">The host name.</param>
        /// <param name="tags">Custom tags.</param>
        /// <param name="configuration">The Datadog logs client configuration.</param>
        /// <param name="configurationSection">A config section defining the datadog configuration.</param>
        /// <param name="restrictedToMinimumLevel">The minimum level for this sink</param>
        /// <param name="logLevel">Legacy parameter to set the minimum level for this sink</param>
        /// <param name="batchSizeLimit">The maximum number of events to emit in a single batch.</param>
        /// <param name="batchPeriod">The time to wait before emitting a new event batch.</param>
        /// <param name="queueLimit">
        /// Maximum number of events to hold in the sink's internal queue, or <c>null</c>
        /// for an unbounded queue. The default is <c>10000</c>
        /// </param>
        /// <param name="exceptionHandler">This function is called when an exception occurs when using 
        /// DatadogConfiguration.UseTCP=false (the default configuration)</param>
        /// <param name="detectTCPDisconnection">Detect when the TCP connection is lost and recreate a new connection.</param>
        /// <param name="client">A client implementation to send the logs.</param>
        /// <param name="formatter">A formatter implementation to change the format of the logs.</param>
        /// <param name="maxMessageSize">The maximum size in bytes of a message before it is split into chunks</param>
        /// <returns>Logger configuration</returns>
        /// <exception cref="ArgumentNullException">A required parameter is null.</exception>
        public static LoggerConfiguration DatadogLogs(
            this LoggerSinkConfiguration loggerConfiguration,
            string apiKey,
            string source = null,
            string service = null,
            string host = null,
            string[] tags = null,
            DatadogConfiguration configuration = null,
            IConfigurationSection configurationSection = null,
            LogEventLevel restrictedToMinimumLevel = LevelAlias.Minimum,
            LogEventLevel logLevel = LevelAlias.Minimum,
            int? batchSizeLimit = null,
            TimeSpan? batchPeriod = null,
            int? queueLimit = null,
            Action<Exception> exceptionHandler = null,
            bool detectTCPDisconnection = false, 
            IDatadogClient client = null,
            ITextFormatter formatter = null,
            int? maxMessageSize = null)
        {
            if (loggerConfiguration == null)
            {
                throw new ArgumentNullException(nameof(loggerConfiguration));
            }
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentNullException(nameof(apiKey));
            }

            var config = ApplyMicrosoftExtensionsConfiguration.ConfigureDatadogConfiguration(configuration, configurationSection);
            var (sink, batchingOptions) = DatadogSink.Create(apiKey, source, service, host, tags, config, batchSizeLimit, batchPeriod, queueLimit, exceptionHandler, detectTCPDisconnection, client, formatter, maxMessageSize);

            // Use restrictedToMinimumLevel if set, otherwise use logLevel
            var effectiveLevel = restrictedToMinimumLevel != LevelAlias.Minimum ? restrictedToMinimumLevel : logLevel;
            return loggerConfiguration.Sink(sink, batchingOptions, effectiveLevel);
        }
    }
}
