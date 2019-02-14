﻿using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Logging;
using Wavefront.SDK.CSharp.Entities.Metrics;

namespace Wavefront.SDK.CSharp.Common.Application
{
    /// <summary>
    /// Service that periodically reports component heartbeats to Wavefront.
    /// </summary>
    public class HeartbeaterService : IDisposable
    {
        private static readonly ILogger Logger =
            Logging.LoggerFactory.CreateLogger<HeartbeaterService>();

        private readonly IWavefrontMetricSender wavefrontMetricSender;
        private readonly IList<IDictionary<string, string>> heartbeatMetricTagsList;
        private readonly string source;
        private Timer timer;

        /// <summary>
        /// Construct a HeartbeaterService that periodically reports heartbeats for one component.
        /// </summary>
        /// <param name="wavefrontMetricSender">
        /// Sender that handles sending of heartbeat metrics to Wavefront.
        /// </param>
        /// <param name="applicationTags">Application tags.</param>
        /// <param name="component">The component to send heartbeats for.</param>
        /// <param name="source">The source (or host).</param>
        public HeartbeaterService(
            IWavefrontMetricSender wavefrontMetricSender,
            ApplicationTags applicationTags,
            string component,
            string source)
            : this(wavefrontMetricSender, applicationTags, new List<string> { component }, source)
        { }

        /// <summary>
        /// Construct a HeartbeaterService that periodically reports heartbeats for multiple
        /// components.
        /// </summary>
        /// <param name="wavefrontMetricSender">
        /// Sender that handles sending of heartbeat metrics to Wavefront.
        /// </param>
        /// <param name="applicationTags">Application tags.</param>
        /// <param name="components">List of components to send heartbeats for.</param>
        /// <param name="source">The source (or host).</param>
        public HeartbeaterService(
            IWavefrontMetricSender wavefrontMetricSender,
            ApplicationTags applicationTags,
            IList<string> components,
            string source)
        {
            this.wavefrontMetricSender = wavefrontMetricSender;
            this.source = source;
            heartbeatMetricTagsList = new List<IDictionary<string, string>>();
            foreach (string component in components)
            {
                heartbeatMetricTagsList.Add(new Dictionary<string, string>
                {
                    { Constants.ApplicationTagKey, applicationTags.Application },
                    { Constants.ClusterTagKey, applicationTags.Cluster ?? Constants.NullTagValue },
                    { Constants.ServiceTagKey, applicationTags.Service },
                    { Constants.ShardTagKey, applicationTags.Shard ?? Constants.NullTagValue },
                    { Constants.ComponentTagKey, component }
                });
            }
        }

        /// <summary>
        /// Start sending hearbeats once every 5 minutes, with an initial delay of 1 minute.
        /// </summary>
        public void Start()
        {
            timer = new Timer(SendHeartbeat, null, TimeSpan.FromMinutes(1),
                TimeSpan.FromMinutes(5));
        }

        private void SendHeartbeat(object state)
        {
            foreach (var heartbeatMetricTags in heartbeatMetricTagsList)
            {
                try
                {
                    wavefrontMetricSender.SendMetric(Constants.HeartbeatMetric, 1.0,
                                                     DateTimeUtils.UnixTimeMilliseconds(DateTime.UtcNow),
                                                     source, heartbeatMetricTags);
                }
                catch (Exception)
                {
                    Logger.LogWarning($"Cannot report {Constants.HeartbeatMetric} to Wavefront");
                }
            }
        }

        /// <summary>
        /// Stop sending heartbeats.
        /// </summary>
        public void Stop()
        {
            timer?.Change(Timeout.Infinite, 0);
        }

        public void Dispose()
        {
            timer?.Dispose();
        }
    }
}
