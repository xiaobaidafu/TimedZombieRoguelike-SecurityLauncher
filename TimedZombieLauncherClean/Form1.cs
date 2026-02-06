using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace TimedZombieLauncherClean
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void btnLaunch_Click(object sender, EventArgs e)
        {
            btnLaunch.Enabled = false;
            try
            {
                RunChecksAndLaunch();
            }
            finally
            {
                btnLaunch.Enabled = true;
            }
        }
        // Change to the exe name of your Unity build
        private const string GameExeName = "TimedZombieRoguelike.exe";

        private static readonly HashSet<string> BannedProcesses =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            //delete chrome when use, this is for testing. You can remove it when you use, or add other processes you want to ban.
            { "cheatengine", "artmoney", "ollydbg", "x64dbg", "processhacker","chrome" };

        private void RunChecksAndLaunch()
        {
            SetStatus("Security Status: Checking...");
            Log("Starting security checks...");

            // 1) process scan
            if (IsBannedProcessRunning(out string proc))
            {
                Fail("Security Status: FAIL (Cheat detected)",
                     $"Detected banned process: {proc}");
                return;
            }
            Log("PASS: process scan.");

            // 2) integrity check (SHA-256)
            if (!VerifyExeHash(out string hashError))
            {
                Fail("Security Status: FAIL (Integrity check failed)", hashError);
                return;
            }
            Log("PASS: integrity check.");

            // 3) launch
            if (!TryLaunchGame(out string launchError))
            {
                Fail("Security Status: FAIL (Launch error)", launchError);
                return;
            }

            SetStatus("Security Status: PASS (Game launched)");
            Log("Game launched successfully.");
            Close();
        }

        private bool IsBannedProcessRunning(out string foundProcess)
        {
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string name = p.ProcessName.ToLower();
                    if (BannedProcesses.Contains(name))
                    {
                        foundProcess = name;
                        return true;
                    }
                }
                catch { }
            }

            foundProcess = null;
            return false;
        }


        private bool VerifyExeHash(out string error)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, GameExeName);
            string hashPath = exePath + ".hash";

            Log("EXE : " + exePath);
            Log("HASH: " + hashPath);

            if (!File.Exists(exePath))
            {
                error = "Game EXE not found. Put launcher in the same folder as the Unity build EXE.";
                return false;
            }

            if (!File.Exists(hashPath))
            {
                error = "Hash file missing. Expected: " + Path.GetFileName(hashPath) +
                        "\nCreate it using SHA-256 and place it next to the EXE.";
                return false;
            }

            string expected = File.ReadAllText(hashPath).Trim();
            if (string.IsNullOrWhiteSpace(expected))
            {
                error = "Hash file is empty. Please regenerate the SHA-256 hash.";
                return false;
            }

            string actual = ComputeSha256(exePath);
            Log("Expected: " + expected);
            Log("Actual  : " + actual);

            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                error = "Hash mismatch. The EXE may have been modified (tampered build).";
                return false;
            }

            error = "";
            return true;
        }

        private static string ComputeSha256(string filePath)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(filePath))
            {
                var hash = sha.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private bool TryLaunchGame(out string error)
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string exePath = Path.Combine(baseDir, GameExeName);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = baseDir,
                    UseShellExecute = true
                });

                error = "";
                return true;
            }
            catch (Exception ex)
            {
                error = "Failed to start the game:\n" + ex.Message;
                return false;
            }
        }

        private void SetStatus(string text)
        {
            lblStatus.Text = text;
        }

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}{Environment.NewLine}";
            try { txtLog.AppendText(line); } catch { }
            Debug.WriteLine(line);
        }

        private void Fail(string status, string message)
        {
            SetStatus(status);
            Log("FAILED: " + message);
            MessageBox.Show(message, "Launcher Security Check",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

    }
}
