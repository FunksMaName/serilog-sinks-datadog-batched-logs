﻿// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0.
// This product includes software developed at Datadog (https://www.datadoghq.com/).
// Copyright 2019 Datadog, Inc.


using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Debugging;
using Serilog.Events;
using Serilog.Formatting;

[assembly:
    InternalsVisibleTo(
        "Serilog.Sinks.Datadog.Logs.Channel.Tests, PublicKey=0024000004800000940000000602000000240000525341310004000001000100a188c93acb61ca68b3b11e5047e3602ffea902e7413310ce96cdd8e31992d36d9276cd36ce55b7870a39379fec698b458bebaa0dc8c72b5e438c7418d640c9bc46a21af3f08a48b68aa8ec23fe0d01bcdcfa5126c66e7586ae08dc1c21142b2c7d49cb09649a2fc9ba767fc88fee6347536a51d28ff398eaabb760494db90dd0")]

namespace Serilog.Sinks.Datadog.Logs
{
    public class DatadogSink : IBatchedLogEventSink, IDisposable
    {
        private readonly IDatadogClient _client;
        private readonly Action<Exception> _exceptionHandler;

#if !NETSTANDARD1_0_OR_GREATER
        private static Task completedTask = Task.FromResult(false);
#endif


        /// <summary>
        /// The time to wait before emitting a new event batch.
        /// </summary>
        private static readonly TimeSpan DefaultBatchPeriod = TimeSpan.FromSeconds(2);

        /// <summary>
        /// The maximum number of events to emit in a single batch.
        /// </summary>
        private const int DefaultBatchSizeLimit = 50;

        /// <summary>
        /// The maximum size of a log event before truncation. 
        /// Should not exceed 1MB as per API documentation: https://docs.datadoghq.com/api/latest/logs/
        /// </summary>
        private const int DefaultMaxMessageSize = 256 * 1000;


        public DatadogSink(string apiKey, string source, string service, string host, string[] tags,
            DatadogConfiguration config, Action<Exception> exceptionHandler = null, bool detectTCPDisconnection = false,
            IDatadogClient client = null, ITextFormatter formatter = null, int? maxMessageSize = null)
        {
            formatter = formatter ?? new DatadogJsonFormatter();
            var enricher = new DatadogLogRenderer(source, service, host, tags, maxMessageSize ?? DefaultMaxMessageSize, formatter);
            _client = client ??
                      CreateDatadogClient(apiKey, enricher, config, detectTCPDisconnection);
            _exceptionHandler = exceptionHandler;
        }

        internal static (DatadogSink, BatchingOptions) Create(
            string apiKey,
            string source,
            string service,
            string host,
            string[] tags,
            DatadogConfiguration config,
            int? batchSizeLimit = null,
            TimeSpan? batchPeriod = null,
            int? queueLimit = null,
            Action<Exception> exceptionHandler = null,
            bool detectTCPDisconnection = false,
            IDatadogClient client = null,
            ITextFormatter formatter = null,
            int? maxMessageSize = null)
        {
            var options = new BatchingOptions()
            {
                BatchSizeLimit = batchSizeLimit ?? DefaultBatchSizeLimit,
                BufferingTimeLimit = batchPeriod ?? DefaultBatchPeriod,
            };

            if (queueLimit.HasValue)
            {
                options.QueueLimit = queueLimit.Value;
            }

            var sink = new DatadogSink(apiKey, source, service, host, tags, config, exceptionHandler,
                detectTCPDisconnection, client, formatter, maxMessageSize);
            
            return (sink, options);
        }

        /// <summary>
        /// Emit a batch of log events to Datadog logs-backend.
        /// </summary>
        /// <param name="events">The events to emit.</param>
        public async Task EmitBatchAsync(IReadOnlyCollection<LogEvent> events)
        {
            try
            {
                if (!events.Any())
                {
                    return;
                }

                var task = _client.WriteAsync(events);
                await RunTask(task);
            }
            catch (Exception e)
            {
                OnException(e);
            }
        }

        public Task OnEmptyBatchAsync()
        {
#if NETSTANDARD1_0_OR_GREATER
            return Task.CompletedTask;
#else
            return completedTask;
#endif
        }

        /// <summary>
        /// Free resources held by the sink.
        /// </summary>
        public void Dispose()
        {
            _client.Close();
        }

        private static IDatadogClient CreateDatadogClient(string apiKey, DatadogLogRenderer renderer, DatadogConfiguration configuration, bool detectTCPDisconnection)
        {
            if (configuration.UseTCP)
            {
                return new DatadogTcpClient(configuration, renderer, apiKey, detectTCPDisconnection);
            }
            else
            {
                var httpIntakeClient = new DatadogHttpIntakeClient(apiKey);
                return new DatadogHttpClient($"{configuration.Url}/api/v2/logs", renderer, httpIntakeClient, configuration.MaxRetries);
            }
        }

        private async Task RunTask(Task task)
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                if (task?.Exception != null)
                {
                    foreach (var innerException in task.Exception.InnerExceptions)
                    {
                        OnException(innerException);
                    }
                }
                else
                {
                    throw;
                }
            }
        }

        private void OnException(Exception e)
        {
            _exceptionHandler?.Invoke(e);

            SelfLog.WriteLine("{0}", e.Message);
        }
    }
}