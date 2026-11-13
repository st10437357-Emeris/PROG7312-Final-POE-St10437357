README File (Application Suite Instructions)
Below is the exact text to save as README.md in your GitHub repository root:
Smart-X IoT Ecosystem - Full Application Suite This repository contains the complete Smart-X system, a decoupled .NET 10 application designed to manage, process, and route high-throughput IoT telemetry.
Prerequisites:
•	.NET 10 SDK
•	Visual Studio (with Desktop and Web workloads)
Setup and Compilation:
1.	Clone the repository: git clone [https://github.com/EMGPPT/prog7314-2026-poe-st10437357-emeris.git](https://github.com/EMGPPT/prog7314-2026-poe-st10437357-emeris.git)
2.	Open SmartX_Ecosystem.sln in Visual Studio.
3.	Set the solution to Multiple Startup Projects: Right-click the Solution -> Configure Startup Projects -> set both SmartX_Ecosystem (API) and SmartX.Dashboard (UI) to Start.
4.	Clean and Rebuild the solution to restore all NuGet packages.
Execution and Usage:
1.	Press Start (or F5) to launch both the API console and the Windows Forms Dashboard simultaneously.
2.	Part 1 (Ingestion): Use the "Sensor Data Ingestion" menu to register new sensors to the live dictionary.
3.	Part 2 (Real-Time Stream): Open the "Command Stream" to dequeue telemetry, issue commands (testing the Stack undo feature), and fetch predictive insights.
4.	Part 3 (Network Topology): Open the "Network Topology" menu to calculate the Minimum Spanning Tree (MST) of the mesh network, track data routing paths, and view chronologically sorted telemetry history.
