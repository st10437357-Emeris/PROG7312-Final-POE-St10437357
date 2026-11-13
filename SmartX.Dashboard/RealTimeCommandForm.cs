using System;
using System.Drawing;
using System.Windows.Forms;
using System.Net.Http;
using System.Text.Json;
using System.Collections.Generic;

namespace SmartX.Dashboard
{
    public partial class RealTimeCommandForm : Form
    {
        private static readonly HttpClient client = new HttpClient { BaseAddress = new Uri("https://localhost:7103/") };

        // UI Components
        private DataGridView dgvTelemetry;
        private TextBox txtSearchMac;
        private Button btnSearchDevice;
        private TextBox txtCommandAction;
        private Button btnIssueCommand;
        private Button btnUndo;
        private ListBox lstInsights;
        private Button btnFetchInsights;

        public RealTimeCommandForm()
        {
            InitializeComponent();
            SetupDashboardUI();
        }

        private void SetupDashboardUI()
        {
            this.Text = "Real-Time Command Stream & History";
            this.Size = new Size(820, 600);

            // 1. Data Visualisation
            Label lblGrid = new Label { Text = "Live Sensor Telemetry:", Location = new Point(20, 20), AutoSize = true };
            dgvTelemetry = new DataGridView { Location = new Point(20, 45), Size = new Size(450, 250), ReadOnly = true };
            dgvTelemetry.Columns.Add("MacAddress", "MAC Address");
            dgvTelemetry.Columns.Add("Payload", "Payload Data");
            this.Controls.Add(lblGrid);
            this.Controls.Add(dgvTelemetry);

            // 2. Search & Filter
            Label lblSearch = new Label { Text = "Device Search (MAC):", Location = new Point(500, 20), AutoSize = true };
            txtSearchMac = new TextBox { Location = new Point(500, 45), Size = new Size(150, 25) };

            btnSearchDevice = new Button { Text = "Search", Location = new Point(660, 43), Size = new Size(100, 27) };
            btnSearchDevice.Click += async (s, e) => {
                try
                {
                    var response = await client.GetAsync($"api/devices/{txtSearchMac.Text.Trim()}");
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();
                        var deviceData = JsonSerializer.Deserialize<JsonElement>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                        string mac = deviceData.GetProperty("macAddress").GetString();
                        string category = deviceData.GetProperty("category").GetString();
                        MessageBox.Show($"Device Located!\nMAC: {mac}\nCategory: {category}", "Search Result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        dgvTelemetry.Rows.Add(mac, category);
                    }
                    else
                    {
                        MessageBox.Show("Device Not Found in live registry.", "Search Result", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch { MessageBox.Show("API Connection Error.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };

            this.Controls.Add(lblSearch);
            this.Controls.Add(txtSearchMac);
            this.Controls.Add(btnSearchDevice);

            // 3. Command Issuing & Undo
            Label lblCmd = new Label { Text = "Manual Override Command:", Location = new Point(500, 90), AutoSize = true };
            txtCommandAction = new TextBox { Location = new Point(500, 115), Size = new Size(260, 25) };

            btnIssueCommand = new Button { Text = "Issue Command", Location = new Point(500, 150), Size = new Size(125, 30) };
            btnIssueCommand.Click += async (s, e) => {
                var cmd = new { CommandId = Guid.NewGuid().ToString(), Action = txtCommandAction.Text };
                var content = new StringContent(JsonSerializer.Serialize(cmd), System.Text.Encoding.UTF8, "application/json");
                await client.PostAsync("api/commands/issue", content);
                MessageBox.Show("Command Issued!");
            };

            btnUndo = new Button { Text = "Undo Last Command", Location = new Point(635, 150), Size = new Size(125, 30) };
            btnUndo.Click += async (s, e) => {
                var response = await client.PostAsync("api/commands/undo", null);
                MessageBox.Show(response.IsSuccessStatusCode ? "Undo Successful" : "Nothing to undo");
            };

            this.Controls.Add(lblCmd);
            this.Controls.Add(txtCommandAction);
            this.Controls.Add(btnIssueCommand);
            this.Controls.Add(btnUndo);

            // Queue & State Management
            Label lblFixes = new Label { Text = "Queue & State Management:", Location = new Point(500, 195), AutoSize = true };

            Button btnProcessStd = new Button { Text = "Dequeue Std", Location = new Point(500, 220), Size = new Size(125, 30) };
            btnProcessStd.Click += async (s, e) => {
                var response = await client.PostAsync("api/telemetry/process/standard", null);
                MessageBox.Show(await response.Content.ReadAsStringAsync(), "Standard Queue");
            };

            Button btnProcessCrit = new Button { Text = "Dequeue Crit", Location = new Point(635, 220), Size = new Size(125, 30) };
            btnProcessCrit.Click += async (s, e) => {
                var response = await client.PostAsync("api/telemetry/process/critical", null);
                MessageBox.Show(await response.Content.ReadAsStringAsync(), "Critical Queue");
            };

            Button btnDisconnect = new Button { Text = "Report Disconnect", Location = new Point(500, 255), Size = new Size(125, 30) };
            btnDisconnect.Click += async (s, e) => {
                if (string.IsNullOrEmpty(txtSearchMac.Text)) { MessageBox.Show("Enter MAC above first."); return; }
                var response = await client.PostAsync($"api/devices/{txtSearchMac.Text.Trim()}/disconnect", null);
                MessageBox.Show(await response.Content.ReadAsStringAsync(), "HashSet Update");
            };

            Button btnResolve = new Button { Text = "Resolve Error", Location = new Point(635, 255), Size = new Size(125, 30) };
            btnResolve.Click += async (s, e) => {
                if (string.IsNullOrEmpty(txtSearchMac.Text)) { MessageBox.Show("Enter MAC above first."); return; }
                var response = await client.PostAsync($"api/devices/{txtSearchMac.Text.Trim()}/resolve", null);
                MessageBox.Show(await response.Content.ReadAsStringAsync(), "HashSet Update");
            };

            this.Controls.Add(lblFixes);
            this.Controls.Add(btnProcessStd);
            this.Controls.Add(btnProcessCrit);
            this.Controls.Add(btnDisconnect);
            this.Controls.Add(btnResolve);

            // ==========================================
            // 4. BULLETPROOF PREDICTIVE INSIGHTS
            // ==========================================
            Label lblInsights = new Label { Text = "Automated Insights & Suggested Actions:", Location = new Point(20, 320), AutoSize = true };
            lstInsights = new ListBox { Location = new Point(20, 345), Size = new Size(740, 150) };

            btnFetchInsights = new Button { Text = "Refresh Insights", Location = new Point(20, 510), Size = new Size(150, 30) };
            btnFetchInsights.Click += async (s, e) => {
                try
                {
                    var response = await client.GetAsync("api/insights");
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonResponse = await response.Content.ReadAsStringAsync();

                        // Safely parse the JSON using the DTO below to avoid strict casing errors
                        var insights = JsonSerializer.Deserialize<List<InsightDto>>(jsonResponse, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        lstInsights.Items.Clear();
                        if (insights != null && insights.Count > 0)
                        {
                            foreach (var insight in insights)
                            {
                                lstInsights.Items.Add($"💡 {insight.RecommendedAction} - {insight.Reason}");
                            }
                        }
                        else
                        {
                            lstInsights.Items.Add("No actionable insights at this time.");
                        }
                    }
                }
                catch { MessageBox.Show("API Connection Error"); }
            };

            this.Controls.Add(lblInsights);
            this.Controls.Add(lstInsights);
            this.Controls.Add(btnFetchInsights);
        }
    }

    // Helper class to guarantee the JSON string reads correctly
    public class InsightDto
    {
        public string RecommendedAction { get; set; }
        public string Reason { get; set; }
    }
}