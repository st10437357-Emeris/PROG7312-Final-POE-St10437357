using System;
using System.Collections.Generic;

namespace SmartX.Core.Processing
{
    // Command structure for the Undo stack
    public class DeviceCommand
    {
        public string CommandId { get; set; }
        public string Action { get; set; }
    }

    public class TelemetryProcessor
    {
        // 1. Message Queues (Queue<T>) for standard FIFO processing
        private readonly Queue<TelemetryPacket<string>> _standardTelemetryQueue;

        // 2. Priority Queues for critical alerts bypassing the standard queue
        private readonly PriorityQueue<TelemetryPacket<string>, int> _criticalAlertQueue;

        // 3. Stacks (Stack<T>) for the manual override "Undo" feature
        private readonly Stack<DeviceCommand> _commandHistory;

        public TelemetryProcessor()
        {
            _standardTelemetryQueue = new Queue<TelemetryPacket<string>>();
            _criticalAlertQueue = new PriorityQueue<TelemetryPacket<string>, int>();
            _commandHistory = new Stack<DeviceCommand>();
        }

        public void EnqueueStandardTelemetry(TelemetryPacket<string> packet)
        {
            _standardTelemetryQueue.Enqueue(packet);
            Console.WriteLine($"Standard packet queued for device {packet.MacAddress}");
        }

        public void EnqueueCriticalAlert(TelemetryPacket<string> packet, int severity)
        {
            _criticalAlertQueue.Enqueue(packet, severity);
            Console.WriteLine($"CRITICAL ALERT queued for device {packet.MacAddress} with severity {severity}");
        }

        // --- NEW METHOD TO FIX DEQUEUE ISSUE ---
        public TelemetryPacket<string> ProcessNextStandardTelemetry()
        {
            if (_standardTelemetryQueue.Count > 0)
            {
                var packet = _standardTelemetryQueue.Dequeue();
                Console.WriteLine($"Processed standard packet for {packet.MacAddress}");
                return packet;
            }
            Console.WriteLine("Standard queue is empty.");
            return null;
        }

        // --- NEW METHOD TO FIX DEQUEUE ISSUE ---
        public TelemetryPacket<string> ProcessNextCriticalAlert()
        {
            if (_criticalAlertQueue.Count > 0)
            {
                var packet = _criticalAlertQueue.Dequeue();
                Console.WriteLine($"Processed critical alert for {packet.MacAddress}");
                return packet;
            }
            Console.WriteLine("Critical alert queue is empty.");
            return null;
        }

        public void IssueCommand(DeviceCommand command)
        {
            _commandHistory.Push(command);
            Console.WriteLine($"Command '{command.Action}' issued and added to history stack.");
        }

        public DeviceCommand UndoLastCommand()
        {
            if (_commandHistory.Count > 0)
            {
                DeviceCommand lastCommand = _commandHistory.Pop();
                Console.WriteLine($"Reverting last command: {lastCommand.Action}");
                return lastCommand;
            }

            Console.WriteLine("No commands in history to undo.");
            return null;
        }
    }
}