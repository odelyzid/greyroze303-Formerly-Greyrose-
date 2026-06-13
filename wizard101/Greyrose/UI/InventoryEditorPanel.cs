using System;
using System.Linq;
using System.Windows.Forms;
using Greyrose.Data;

namespace Greyrose.UI
{
    public class InventoryEditorPanel : UserControl
    {
        ComboBox _characterPicker;
        DataGridView _slotsGrid;
        CheckBox _stripInventory;
        TextBox _statsHex;
        TextBox _loginHex;
        Label _status;
        byte[] _workingBlob;
        long _characterId;
        bool _isCreated;

        public InventoryEditorPanel()
        {
            Dock = DockStyle.Fill;
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 220
            };

            var topPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };

            int y = 8;
            topPanel.Controls.Add(new Label { Left = 8, Top = y, Width = 100, Text = "Character" });
            _characterPicker = new ComboBox { Left = 120, Top = y, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
            _characterPicker.SelectedIndexChanged += (s, e) => LoadSelected();
            topPanel.Controls.Add(_characterPicker);
            y += 32;

            _stripInventory = new CheckBox
            {
                Left = 120,
                Top = y,
                Width = 400,
                Text = "Strip inventory section for zone login (truncate at inventory marker)"
            };
            topPanel.Controls.Add(_stripInventory);
            y += 28;

            var hint = new Label
            {
                Left = 120,
                Top = y,
                Width = 520,
                Height = 32,
                Text = "Zone capture file: " + (CreatedZoneLoginBlob.IsAvailable() ? "present" : "missing")
            };
            topPanel.Controls.Add(hint);
            y += 36;

            _slotsGrid = new DataGridView
            {
                Left = 8,
                Top = y,
                Width = 640,
                Height = 120,
                ReadOnly = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _slotsGrid.Columns.Add("Index", "Slot");
            _slotsGrid.Columns.Add("Offset", "Offset");
            _slotsGrid.Columns.Add("TemplateHex", "Template (hex)");
            _slotsGrid.Columns[0].ReadOnly = true;
            _slotsGrid.Columns[1].ReadOnly = true;
            topPanel.Controls.Add(_slotsGrid);
            y += 128;

            topPanel.Controls.Add(new Label { Left = 8, Top = y, Width = 110, Text = "Stats prefix (ro)" });
            _statsHex = new TextBox
            {
                Left = 120,
                Top = y,
                Width = 520,
                Height = 48,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical
            };
            topPanel.Controls.Add(_statsHex);

            split.Panel1.Controls.Add(topPanel);

            var bottom = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            bottom.Controls.Add(new Label { Left = 8, Top = 8, Width = 110, Text = "Login blob hex" });
            _loginHex = new TextBox
            {
                Left = 8,
                Top = 28,
                Width = 640,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, 8.5f)
            };
            bottom.Controls.Add(_loginHex);

            _status = new Label { Left = 8, Top = 154, Width = 640, Height = 40 };
            bottom.Controls.Add(_status);

            var reloadBtn = new Button { Text = "Reload", Left = 8, Top = 200, Width = 80 };
            var saveBtn = new Button { Text = "Save", Left = 96, Top = 200, Width = 80 };
            var validateBtn = new Button { Text = "Validate", Left = 184, Top = 200, Width = 80 };
            var rebuildBtn = new Button { Text = "Rebuild", Left = 272, Top = 200, Width = 80 };
            reloadBtn.Click += (s, e) => LoadSelected();
            saveBtn.Click += (s, e) => SaveCurrent();
            validateBtn.Click += (s, e) => RunValidate();
            rebuildBtn.Click += (s, e) => RebuildFromBuilder();
            bottom.Controls.Add(reloadBtn);
            bottom.Controls.Add(saveBtn);
            bottom.Controls.Add(validateBtn);
            bottom.Controls.Add(rebuildBtn);

            split.Panel2.Controls.Add(bottom);
            Controls.Add(split);

            Load += (s, e) => ReloadCharacters();
        }

        public void ReloadCharacters()
        {
            _characterPicker.Items.Clear();
            foreach (var ch in DataStore.GetAllCharacters().OrderBy(c => c.Id))
                _characterPicker.Items.Add(new CharacterItem(ch));
            if (_characterPicker.Items.Count > 0)
                _characterPicker.SelectedIndex = 0;
        }

        void LoadSelected()
        {
            if (!(_characterPicker.SelectedItem is CharacterItem item))
                return;

            _characterId = item.Character.Id;
            _isCreated = CharacterInfoCodec.UsesZoneLoginCapture(item.Character);

            byte[] fresh = LoginBlobBuilder.BuildLoginBlob(item.Character, null, DefaultLoginBlob.GetBytes());
            var state = DataStore.GetPlayerState(_characterId);
            byte[] stored = null;
            if (state != null && !string.IsNullOrWhiteSpace(state.LoginBlobHex))
                stored = CharacterInfoCodec.HexToBytes(state.LoginBlobHex);

            _workingBlob = fresh;
            string note = "fresh build";
            if (stored != null && stored.Length > 0 && StoredMatchesFresh(stored, fresh))
            {
                _workingBlob = stored;
                note = "from DB";
            }
            else if (stored != null && stored.Length > 0)
                note = "fresh build (DB has stale " + stored.Length + " B — click Save to update)";

            ApplyBlobToUi(_workingBlob);
            _status.Text = "Loaded " + _workingBlob.Length + " bytes (" + note + ").";
        }

        void ApplyBlobToUi(byte[] blob)
        {
            var inspection = LoginBlobInspector.Parse(blob);
            _statsHex.Text = LoginBlobInspector.BytesToHex(blob, 0, inspection.StatsPrefixLength);
            _loginHex.Text = CharacterInfoCodec.BytesToHex(blob);

            _slotsGrid.Rows.Clear();
            foreach (var slot in inspection.EquipmentSlots)
            {
                _slotsGrid.Rows.Add(slot.Index, slot.Offset, slot.TemplateId.ToString("X8"));
            }

            _stripInventory.Checked = inspection.InventoryOffset >= 0;
        }

        byte[] CollectBlobFromUi()
        {
            string hex = CharacterInfoCodec.NormalizeHex(_loginHex.Text);
            if (string.IsNullOrEmpty(hex))
                return _workingBlob ?? Array.Empty<byte>();

            byte[] blob = CharacterInfoCodec.HexToBytes(hex);
            if (_stripInventory.Checked)
                blob = LoginBlobInspector.StripInventory(blob);

            for (int row = 0; row < _slotsGrid.Rows.Count; row++)
            {
                if (_slotsGrid.Rows[row].IsNewRow)
                    continue;

                string templateHex = _slotsGrid.Rows[row].Cells["TemplateHex"].Value?.ToString();
                if (string.IsNullOrWhiteSpace(templateHex))
                    continue;

                templateHex = templateHex.Replace(" ", "").Replace("0x", "").Replace("0X", "");
                if (templateHex.Length == 0)
                    continue;

                if (!uint.TryParse(templateHex, System.Globalization.NumberStyles.HexNumber, null, out uint templateId))
                    continue;

                if (LoginBlobInspector.TrySetEquipmentTemplate(blob, row, templateId, out byte[] updated))
                    blob = updated;
            }

            return blob;
        }

        void SaveCurrent()
        {
            if (_characterId <= 0)
                return;

            byte[] blob = CollectBlobFromUi();
            var v = LoginBlobBuilder.Validate(blob, _isCreated);
            if (!v.Ok)
            {
                _status.Text = "Save blocked: " + v.Message;
                return;
            }

            var state = DataStore.GetPlayerState(_characterId) ?? new PlayerStateRecord { CharacterId = _characterId };
            state.LoginBlobHex = CharacterInfoCodec.BytesToHex(blob);
            DataStore.SavePlayerState(state);
            _workingBlob = blob;
            _status.Text = "Saved " + blob.Length + " bytes. " + v.Message;
        }

        void RunValidate()
        {
            byte[] blob = CollectBlobFromUi();
            var inspection = LoginBlobInspector.Parse(blob);
            _status.Text = LoginBlobInspector.FormatInspectionReport(inspection, _isCreated);
        }

        void RebuildFromBuilder()
        {
            if (!(_characterPicker.SelectedItem is CharacterItem item))
                return;

            byte[] blob = LoginBlobBuilder.BuildLoginBlob(item.Character, null, DefaultLoginBlob.GetBytes());
            _workingBlob = blob;
            ApplyBlobToUi(blob);
            _status.Text = "Rebuilt via LoginBlobBuilder (" + blob.Length + " bytes).";
        }

        static bool StoredMatchesFresh(byte[] stored, byte[] fresh)
        {
            if (stored == null || fresh == null)
                return false;
            if (stored.Length != fresh.Length)
                return false;
            for (int i = 0; i < stored.Length; i++)
            {
                if (stored[i] != fresh[i])
                    return false;
            }
            return true;
        }

        sealed class CharacterItem
        {
            public CharacterRecord Character;
            public CharacterItem(CharacterRecord c) { Character = c; }
            public override string ToString() => Character.Name + " (id " + Character.Id + ")";
        }
    }
}
