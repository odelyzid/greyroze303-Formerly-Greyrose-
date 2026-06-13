using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Greyrose.Networking;

namespace Greyrose.UI
{
    public class GamePacketLogPanel : UserControl
    {
        readonly ListBox _list;
        readonly CheckBox _enabled;
        readonly CheckBox _hexDump;
        readonly Button _clearBtn;
        readonly Button _copyBtn;

        public GamePacketLogPanel()
        {
            Dock = DockStyle.Fill;
            var top = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                Padding = new Padding(4)
            };

            _enabled = new CheckBox
            {
                Text = "Enable trace",
                Checked = GamePacketTrace.Enabled,
                AutoSize = true,
                Padding = new Padding(0, 6, 8, 0)
            };
            _enabled.CheckedChanged += (s, e) => GamePacketTrace.Enabled = _enabled.Checked;

            _hexDump = new CheckBox
            {
                Text = "Include hex dump",
                Checked = GamePacketTrace.IncludeHexDump,
                AutoSize = true,
                Padding = new Padding(0, 6, 8, 0)
            };
            _hexDump.CheckedChanged += (s, e) => GamePacketTrace.IncludeHexDump = _hexDump.Checked;

            _clearBtn = new Button { Text = "Clear", Width = 70 };
            _copyBtn = new Button { Text = "Copy all", Width = 70 };
            _clearBtn.Click += (s, e) =>
            {
                GamePacketTrace.Clear();
                _list.Items.Clear();
            };
            _copyBtn.Click += (s, e) => CopyAll();

            top.Controls.AddRange(new Control[] { _enabled, _hexDump, _clearBtn, _copyBtn });

            _list = new ListBox
            {
                Dock = DockStyle.Fill,
                Font = new Font(FontFamily.GenericMonospace, 8.5f),
                HorizontalScrollbar = true,
                IntegralHeight = false
            };

            Controls.Add(_list);
            Controls.Add(top);

            GamePacketTrace.OnLine += OnTraceLine;
            HandleDestroyed += (s, e) => GamePacketTrace.OnLine -= OnTraceLine;

            Load += (s, e) => ReloadSnapshot();
        }

        void OnTraceLine(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(OnTraceLine), line);
                return;
            }

            _list.Items.Add(line);
            if (_list.Items.Count > 2500)
                _list.Items.RemoveAt(0);
            _list.TopIndex = _list.Items.Count - 1;
        }

        void ReloadSnapshot()
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (string line in GamePacketTrace.Snapshot())
                _list.Items.Add(line);
            if (_list.Items.Count > 0)
                _list.TopIndex = _list.Items.Count - 1;
            _list.EndUpdate();
        }

        void CopyAll()
        {
            var sb = new StringBuilder();
            foreach (string item in _list.Items)
            {
                sb.AppendLine(item);
                sb.AppendLine();
            }
            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }
    }
}
