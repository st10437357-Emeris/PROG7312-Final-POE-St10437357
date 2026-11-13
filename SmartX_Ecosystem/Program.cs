using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System;
using SmartX.Core;
using SmartX.Core.Processing;
using SmartX.Core.Management;
using SmartX.Core.Intelligence;

namespace SmartX_Ecosystem
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthorization();
            builder.Services.AddOpenApi();

            builder.Services.AddSingleton<TelemetryProcessor>();
            builder.Services.AddSingleton<DeviceStateManager>();
            builder.Services.AddSingleton<RecommendationEngine>();
            builder.Services.AddSingleton<MeshNetworkGraph>();

            var app = builder.Build();

            // ==========================================
            // PART 3 DEMO: MOCK SEEDING (Graph & History)
            // ==========================================
            var graph = app.Services.GetRequiredService<MeshNetworkGraph>();
            graph.AddNode("SENSOR-A");
            graph.AddNode("SENSOR-B");
            graph.AddNode("GATEWAY");

            graph.AddEdge("SENSOR-A", "SENSOR-B", 10);
            graph.AddEdge("SENSOR-B", "GATEWAY", 15);
            graph.AddEdge("SENSOR-A", "GATEWAY", 60);

            // Seed mock historical logs for the Sorted Dictionary demo
            var stateManager = app.Services.GetRequiredService<DeviceStateManager>();
            stateManager.AddHistoricalLog(DateTime.UtcNow.AddMinutes(-10), new TelemetryLog { Timestamp = DateTime.UtcNow.AddMinutes(-10), Payload = "SENSOR-A: Moisture normal (45%)" });
            stateManager.AddHistoricalLog(DateTime.UtcNow.AddMinutes(-5), new TelemetryLog { Timestamp = DateTime.UtcNow.AddMinutes(-5), Payload = "SENSOR-A: Moisture dropping (25%)" });
            stateManager.AddHistoricalLog(DateTime.UtcNow, new TelemetryLog { Timestamp = DateTime.UtcNow, Payload = "CRITICAL: SENSOR-A Moisture critically low (15%)" });
            // ==========================================

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();

            app.MapPost("/api/telemetry/ingest", (TelemetryPacket<string> packet, DeviceStateManager stateManager) =>
            {
                string[][] rawBatch = new string[1][];
                rawBatch[0] = new string[] { packet.MacAddress, packet.Location, packet.Payload };
                List<string> optimizedCollection = rawBatch[0].ToList();

                var newDevice = new DeviceProfile
                {
                    MacAddress = packet.MacAddress,
                    Category = packet.Payload
                };
                stateManager.RegisterDevice(newDevice);

                return Results.Ok(new
                {
                    Message = "Payload ingested and device registered successfully.",
                    Data = optimizedCollection
                });
            })
            .WithName("IngestTelemetry");

            // --- WIRED REAL ENGINE TO STANDARD QUEUE ---
            app.MapPost("/api/telemetry/standard", (TelemetryPacket<string> packet, TelemetryProcessor processor, DeviceStateManager stateManager, RecommendationEngine engine) =>
            {
                processor.EnqueueStandardTelemetry(new TelemetryPacket<string> { MacAddress = packet.MacAddress, Payload = packet.Payload });
                stateManager.AddHistoricalLog(DateTime.UtcNow, new TelemetryLog { Timestamp = DateTime.UtcNow, Payload = packet.Payload });

                // Engine learns from standard data
                engine.IngestEvent(new TelemetryEvent(packet.MacAddress, "Standard_Update", 50.0, DateTime.UtcNow));

                return Results.Ok(new { Status = "Standard Telemetry Queued" });
            });

            // --- WIRED REAL ENGINE TO CRITICAL QUEUE ---
            app.MapPost("/api/telemetry/critical", (TelemetryPacket<string> packet, int severity, TelemetryProcessor processor, DeviceStateManager stateManager, RecommendationEngine engine) =>
            {
                processor.EnqueueCriticalAlert(new TelemetryPacket<string> { MacAddress = packet.MacAddress, Payload = packet.Payload }, severity);
                stateManager.AddHistoricalLog(DateTime.UtcNow, new TelemetryLog { Timestamp = DateTime.UtcNow, Payload = $"CRITICAL: {packet.Payload}" });

                // Engine detects an anomaly based on your critical alert
                engine.IngestEvent(new TelemetryEvent(packet.MacAddress, "Critical_Drop", 15.0, DateTime.UtcNow));

                return Results.Ok(new { Status = "Critical Alert Queued for Immediate Processing" });
            });

            app.MapPost("/api/telemetry/process/standard", (TelemetryProcessor processor) => {
                var packet = processor.ProcessNextStandardTelemetry();
                return packet != null ? Results.Ok(packet) : Results.Ok(new { Message = "Standard queue is empty" });
            });

            app.MapPost("/api/telemetry/process/critical", (TelemetryProcessor processor) => {
                var packet = processor.ProcessNextCriticalAlert();
                return packet != null ? Results.Ok(packet) : Results.Ok(new { Message = "Critical queue is empty" });
            });

            // --- WIRED REAL ENGINE TO MANUAL COMMANDS ---
            app.MapPost("/api/commands/issue", (DeviceCommand command, TelemetryProcessor processor, RecommendationEngine engine) =>
            {
                processor.IssueCommand(command);

                // Engine learns your behavior based on the command you literally just typed
                engine.LearnUserBehavior("Critical_Drop", command.Action);

                return Results.Ok(new { Status = $"Command '{command.Action}' Issued" });
            });

            app.MapPost("/api/commands/undo", (TelemetryProcessor processor) =>
            {
                var reverted = processor.UndoLastCommand();
                return reverted != null
                    ? Results.Ok(new { Status = "Command Reverted", RevertedCommand = reverted })
                    : Results.BadRequest("No commands to undo.");
            });

            app.MapGet("/api/devices/{macAddress}", (string macAddress, DeviceStateManager stateManager) =>
            {
                var device = stateManager.GetDevice(macAddress);
                return device != null
                    ? Results.Ok(device)
                    : Results.NotFound(new { Message = $"Device with MAC {macAddress} not found in the live registry." });
            });

            app.MapPost("/api/devices/{macAddress}/disconnect", (string macAddress, DeviceStateManager stateManager) => {
                stateManager.ReportDeviceDisconnect(macAddress);
                return Results.Ok(new { Message = $"Disconnect reported for {macAddress}" });
            });

            app.MapPost("/api/devices/{macAddress}/resolve", (string macAddress, DeviceStateManager stateManager) => {
                stateManager.ResolveDeviceError(macAddress);
                return Results.Ok(new { Message = $"Error resolved for {macAddress}" });
            });

            app.MapGet("/api/telemetry/history", (DeviceStateManager stateManager) => {
                return Results.Ok(stateManager.GetHistoricalLogs());
            });

            // ==========================================
            // REAL DYNAMIC INSIGHTS ENDPOINT
            // ==========================================
            app.MapGet("/api/insights", (RecommendationEngine engine) =>
            {
                var insights = engine.GenerateRecommendations();

                if (insights == null || !insights.Any())
                {
                    return Results.Ok(new[] { new { recommendedAction = "Run Diagnostics", reason = "Awaiting new telemetry patterns to analyze." } });
                }

                return Results.Ok(insights);
            });

            app.MapPost("/api/network/node/{macAddress}", (string macAddress, MeshNetworkGraph graph) =>
            {
                graph.AddNode(macAddress);
                return Results.Ok(new { Message = $"Node {macAddress} added to the mesh network." });
            });

            app.MapPost("/api/network/edge", (NetworkEdge edge, MeshNetworkGraph graph) =>
            {
                graph.AddEdge(edge.SourceMac, edge.DestinationMac, edge.SignalStrength);
                return Results.Ok(new { Message = $"Edge connected between {edge.SourceMac} and {edge.DestinationMac}." });
            });

            app.MapGet("/api/network/mst", (MeshNetworkGraph graph) =>
            {
                var mst = graph.CalculateMinimumSpanningTree();
                return Results.Ok(mst);
            });

            app.MapGet("/api/network/route", (string startNode, string gatewayNode, MeshNetworkGraph graph) =>
            {
                var path = graph.GetRoutingPath(startNode, gatewayNode);
                return path.Any()
                    ? Results.Ok(path)
                    : Results.NotFound(new { Message = "No valid path found." });
            });

            app.Run();
        }
    }
}