using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;

namespace SmartX.Dashboard
{
    public partial class NetworkTopologyForm : Form
    {
        // FIX: Hardcoded to match your exact working API port from RealTimeCommandForm
        private static readonly HttpClient client = new HttpClient { BaseAddress = new Uri("https://localhost:7103/") };

        public NetworkTopologyForm()
        {
            InitializeComponent();
            BuildUI();
        }

        private void BuildUI()
        {
            this.Text = "Network Topology & Mesh Routing";
            this.Size = new Size(850, 650);
            this.StartPosition = FormStartPosition.CenterScreen;

            // --- Section 1: Network Graph Visualization (MST) ---
            this.Controls.Add(new Label { Text = "Active Mesh Network (MST View):", Location = new Point(20, 20), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold) });
            ListBox lstNetwork = new ListBox { Location = new Point(20, 45), Size = new Size(400, 200) };
            this.Controls.Add(lstNetwork);

            Button btnRefreshNetwork = new Button { Text = "Calculate MST Topology", Location = new Point(20, 255), Size = new Size(180, 30) };
            btnRefreshNetwork.Click += async (s, e) => {
                try
                {
                    var response = await client.GetAsync("api/network/mst");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();

                        // Safely parse the JSON using the DTO below
                        var edges = JsonSerializer.Deserialize<List<NetworkEdgeDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        lstNetwork.Items.Clear();
                        if (edges != null && edges.Count > 0)
                        {
                            foreach (var edge in edges)
                            {
                                lstNetwork.Items.Add($"{edge.SourceMac} <---> {edge.DestinationMac} (Power Cost: {edge.SignalStrength})");
                            }
                        }
                        else
                        {
                            lstNetwork.Items.Add("No edges found in the MST.");
                        }
                    }
                    else
                    {
                        MessageBox.Show($"API returned an error: {response.StatusCode}");
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Connection Error: Is the API running on port 7103?\n\nDetails: {ex.Message}"); }
            };
            this.Controls.Add(btnRefreshNetwork);

            // --- Section 2: Sensor Routing Path Tracker ---
            this.Controls.Add(new Label { Text = "Track Sensor Routing Path:", Location = new Point(450, 20), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold) });
            this.Controls.Add(new Label { Text = "Sensor MAC:", Location = new Point(450, 50), AutoSize = true });
            TextBox txtStart = new TextBox { Location = new Point(540, 48), Size = new Size(150, 25) };
            this.Controls.Add(new Label { Text = "Gateway MAC:", Location = new Point(450, 85), AutoSize = true });
            TextBox txtEnd = new TextBox { Location = new Point(540, 83), Size = new Size(150, 25) };

            ListBox lstPath = new ListBox { Location = new Point(450, 120), Size = new Size(350, 125) };
            Button btnTrack = new Button { Text = "Find Optimal Route", Location = new Point(450, 255), Size = new Size(150, 30) };

            btnTrack.Click += async (s, e) => {
                try
                {
                    var response = await client.GetAsync($"api/network/route?startNode={txtStart.Text.Trim()}&gatewayNode={txtEnd.Text.Trim()}");
                    lstPath.Items.Clear();

                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var path = JsonSerializer.Deserialize<List<string>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        lstPath.Items.Add("Path to Gateway:");
                        if (path != null)
                        {
                            foreach (var node in path) lstPath.Items.Add($"-> {node}");
                        }
                    }
                    else
                    {
                        lstPath.Items.Add("No route found in MST.");
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Error tracking route: {ex.Message}"); }
            };

            this.Controls.Add(txtStart); this.Controls.Add(txtEnd);
            this.Controls.Add(lstPath); this.Controls.Add(btnTrack);

            // --- Section 3: Well-Organised Historical Log ---
            this.Controls.Add(new Label { Text = "Historical Telemetry Logs (Sorted Dictionary):", Location = new Point(20, 310), AutoSize = true, Font = new Font("Arial", 10, FontStyle.Bold) });
            DataGridView dgvHistory = new DataGridView { Location = new Point(20, 335), Size = new Size(780, 220), ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            dgvHistory.Columns.Add("Timestamp", "Timestamp (UTC)");
            dgvHistory.Columns.Add("Payload", "Data Payload / Event");
            this.Controls.Add(dgvHistory);

            Button btnFetchHistory = new Button { Text = "Load Device History", Location = new Point(20, 565), Size = new Size(180, 30) };
            btnFetchHistory.Click += async (s, e) => {
                try
                {
                    var response = await client.GetAsync("api/telemetry/history");
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();

                        var logs = JsonSerializer.Deserialize<Dictionary<DateTime, TelemetryLogDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        dgvHistory.Rows.Clear();
                        if (logs != null)
                        {
                            foreach (var log in logs)
                            {
                                dgvHistory.Rows.Add(log.Key.ToString("g"), log.Value.Payload);
                            }
                        }
                    }
                }
                catch (Exception ex) { MessageBox.Show($"Error loading history: {ex.Message}"); }
            };
            this.Controls.Add(btnFetchHistory);
        }
    }

    // ==============================================================
    // HELPER CLASSES: Guarantee JSON strings read perfectly
    // ==============================================================
    public class NetworkEdgeDto
    {
        public string SourceMac { get; set; }
        public string DestinationMac { get; set; }
        public int SignalStrength { get; set; }
    }

    public class TelemetryLogDto
    {
        public DateTime Timestamp { get; set; }
        public string Payload { get; set; }
    }
}