using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Greyrose.Branding;
using Greyrose.Data;

namespace Greyrose.UI
{
    public class MainForm : Form
    {
        readonly ServerController _server = new ServerController();
        TextBox _logBox;
        Label _loginStatus;
        Label _patchStatus;
        Label _gameStatus;
        ToolStripLabel _dbPathLabel;
        AccountEditorPanel _accounts;
        CharacterEditorPanel _characters;
        PlayerStateEditorPanel _playerState;
        GamePacketLogPanel _gamePackets;
        InventoryEditorPanel _inventory;
        Button _startBtn;
        Button _stopBtn;

        public MainForm()
        {
            Text = "GreyrOze303";
            Width = 960;
            Height = 720;
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = IconFactory.LoadIcon(BrandingCommand.GetIcoPath()); }
            catch { /* branding assets optional at dev time */ }

            var menu = new ToolStrip();
            menu.Items.Add(new ToolStripButton("Initialize database", null, (s, e) => InitializeDatabase()));
            menu.Items.Add(new ToolStripButton("Open DB folder", null, (s, e) => OpenDbFolder()));
            _dbPathLabel = new ToolStripLabel { Text = "DB: " + Database.Path };
            menu.Items.Add(_dbPathLabel);

            var tabs = new TabControl { Dock = DockStyle.Fill };

            var serverTab = new TabPage("Server");
            var serverLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
            serverLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            serverLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            serverLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 36 };
            _startBtn = new Button { Text = "Start", Width = 80 };
            _stopBtn = new Button { Text = "Stop", Width = 80, Enabled = false };
            _startBtn.Click += (s, e) => StartServers();
            _stopBtn.Click += (s, e) => StopServers();
            buttons.Controls.Add(_startBtn);
            buttons.Controls.Add(_stopBtn);

            var statusPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 28 };
            _loginStatus = new Label { Text = "Login (12000): stopped", AutoSize = true, Padding = new Padding(0, 6, 16, 0) };
            _patchStatus = new Label { Text = "Patch (12500): stopped", AutoSize = true, Padding = new Padding(0, 6, 16, 0) };
            _gameStatus = new Label { Text = "Game (12170): stopped", AutoSize = true, Padding = new Padding(0, 6, 16, 0) };
            statusPanel.Controls.AddRange(new Control[] { _loginStatus, _patchStatus, _gameStatus });

            _logBox = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font(FontFamily.GenericMonospace, 9f),
                BackColor = Color.Black,
                ForeColor = Color.LightGray
            };

            serverLayout.Controls.Add(buttons, 0, 0);
            serverLayout.Controls.Add(statusPanel, 0, 1);
            serverLayout.Controls.Add(_logBox, 0, 2);
            serverTab.Controls.Add(serverLayout);

            _accounts = new AccountEditorPanel();
            _characters = new CharacterEditorPanel();
            _playerState = new PlayerStateEditorPanel();
            _gamePackets = new GamePacketLogPanel();
            _inventory = new InventoryEditorPanel();
            _accounts.AccountsChanged += () =>
            {
                _characters.ReloadAccounts();
                _playerState.ReloadCharacters();
                _inventory.ReloadCharacters();
            };

            tabs.TabPages.Add(serverTab);
            tabs.TabPages.Add(new TabPage("Game Packets") { Controls = { _gamePackets } });
            tabs.TabPages.Add(new TabPage("Accounts") { Controls = { _accounts } });
            tabs.TabPages.Add(new TabPage("Characters") { Controls = { _characters } });
            tabs.TabPages.Add(new TabPage("Player State") { Controls = { _playerState } });
            tabs.TabPages.Add(new TabPage("Inventory") { Controls = { _inventory } });

            Controls.Add(tabs);
            Controls.Add(menu);

            ServerLog.OnLine += AppendLog;
            FormClosed += (s, e) => ServerLog.OnLine -= AppendLog;

            AppendLog("Greyrose ready. Edit the database, then click Start.");
            UpdatePortStatus(false);
        }

        void AppendLog(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(AppendLog), line);
                return;
            }
            if (_logBox.TextLength > 200000)
                _logBox.Clear();
            _logBox.AppendText(line + Environment.NewLine);
        }

        void StartServers()
        {
            _server.Start();
            _startBtn.Enabled = false;
            _stopBtn.Enabled = true;
            UpdatePortStatus(true);
            AppendLog("Servers starting...");
        }

        void StopServers()
        {
            _server.Stop();
            _startBtn.Enabled = true;
            _stopBtn.Enabled = false;
            UpdatePortStatus(false);
            AppendLog("Servers stopped.");
        }

        void UpdatePortStatus(bool running)
        {
            string state = running ? "listening" : "stopped";
            _loginStatus.Text = $"Login (12000): {state}";
            _patchStatus.Text = $"Patch (12500): {state}";
            _gameStatus.Text = $"Game (12170): {state}";
        }

        void InitializeDatabase()
        {
            DataStore.EnsureSeeded();
            _dbPathLabel.Text = "DB: " + Database.Path;
            _accounts.Reload();
            _characters.ReloadAccounts();
            _playerState.ReloadCharacters();
            _inventory.ReloadCharacters();
            AppendLog("Database initialized (seed applied if empty).");
        }

        void OpenDbFolder()
        {
            var dir = Path.GetDirectoryName(Database.Path);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
            {
                MessageBox.Show(this, "Database folder not found.", "Greyrose", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_server.IsRunning)
                StopServers();
            base.OnFormClosing(e);
        }
    }
}
