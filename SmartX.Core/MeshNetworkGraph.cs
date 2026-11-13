using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartX.Core.Processing
{
    // Represents a single connection between two sensor nodes
    public class NetworkEdge : IComparable<NetworkEdge>
    {
        public string SourceMac { get; set; }
        public string DestinationMac { get; set; }
        public int SignalStrength { get; set; } // Lower is better/faster for MST (representing power cost)

        public int CompareTo(NetworkEdge other)
        {
            return SignalStrength.CompareTo(other.SignalStrength);
        }
    }

    public class MeshNetworkGraph
    {
        private readonly HashSet<string> _nodes;
        private readonly List<NetworkEdge> _edges;

        public MeshNetworkGraph()
        {
            _nodes = new HashSet<string>();
            _edges = new List<NetworkEdge>();
        }

        // Register a sensor node to the graph
        public void AddNode(string macAddress)
        {
            _nodes.Add(macAddress);
        }

        // Connect two sensor nodes
        public void AddEdge(string sourceMac, string destMac, int signalStrength)
        {
            _nodes.Add(sourceMac);
            _nodes.Add(destMac);
            _edges.Add(new NetworkEdge { SourceMac = sourceMac, DestinationMac = destMac, SignalStrength = signalStrength });
            // Add reverse edge for undirected graph
            _edges.Add(new NetworkEdge { SourceMac = destMac, DestinationMac = sourceMac, SignalStrength = signalStrength });
        }

        public List<string> GetNodes() => _nodes.ToList();
        public List<NetworkEdge> GetEdges() => _edges;

        // Kruskal's Algorithm to find the Minimum Spanning Tree (Most efficient routing)
        public List<NetworkEdge> CalculateMinimumSpanningTree()
        {
            var mst = new List<NetworkEdge>();
            var edges = _edges.OrderBy(e => e.SignalStrength).ToList();
            var parent = new Dictionary<string, string>();

            // Initialize disjoint set
            foreach (var node in _nodes)
            {
                parent[node] = node;
            }

            string Find(string node)
            {
                if (parent[node] != node)
                    parent[node] = Find(parent[node]);
                return parent[node];
            }

            void Union(string node1, string node2)
            {
                string root1 = Find(node1);
                string root2 = Find(node2);
                if (root1 != root2)
                    parent[root2] = root1;
            }

            foreach (var edge in edges)
            {
                if (Find(edge.SourceMac) != Find(edge.DestinationMac))
                {
                    mst.Add(edge);
                    Union(edge.SourceMac, edge.DestinationMac);
                }
            }

            return mst;
        }

        // Simple BFS to find the routing path of a specific sensor back to the gateway
        public List<string> GetRoutingPath(string startNode, string gatewayNode)
        {
            if (!_nodes.Contains(startNode) || !_nodes.Contains(gatewayNode)) return new List<string>();

            var queue = new Queue<List<string>>();
            var visited = new HashSet<string>();

            queue.Enqueue(new List<string> { startNode });
            visited.Add(startNode);

            var mstEdges = CalculateMinimumSpanningTree();

            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                var current = path.Last();

                if (current == gatewayNode) return path;

                // Find connected nodes in the MST
                var neighbors = mstEdges.Where(e => e.SourceMac == current).Select(e => e.DestinationMac).ToList();

                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        var newPath = new List<string>(path) { neighbor };
                        queue.Enqueue(newPath);
                    }
                }
            }

            return new List<string>(); // No path found
        }
    }
}