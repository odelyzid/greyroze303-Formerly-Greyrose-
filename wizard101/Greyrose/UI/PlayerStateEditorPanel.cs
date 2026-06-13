using System;
using System.Linq;
using System.Windows.Forms;
using Greyrose.Data;

namespace Greyrose.UI
{
    public class PlayerStateEditorPanel : UserControl
    {
        ComboBox _characterPicker;
        NumericUpDown _x;
        NumericUpDown _y;
        NumericUpDown _z;
        NumericUpDown _rot;
        NumericUpDown _mx;
        NumericUpDown _my;
        NumericUpDown _mz;
        NumericUpDown _mrot;
        TextBox _loginHex;
        TextBox _zoneHex;

        public PlayerStateEditorPanel()
        {
            Dock = DockStyle.Fill;
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8) };

            int y = 8;
            panel.Controls.Add(new Label { Left = 8, Top = y, Width = 100, Text = "Character" });
            _characterPicker = new ComboBox { Left = 120, Top = y, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
            _characterPicker.SelectedIndexChanged += (s, e) => LoadSelected();
            panel.Controls.Add(_characterPicker);
            y += 32;

            _x = AddNumeric(panel, "X", y); y += 28;
            _y = AddNumeric(panel, "Y", y); y += 28;
            _z = AddNumeric(panel, "Z", y); y += 28;
            _rot = AddNumeric(panel, "Rotation", y, 9999, 4); y += 28;
            _mx = AddNumeric(panel, "Marker X", y, 65535, 0); y += 28;
            _my = AddNumeric(panel, "Marker Y", y, 65535, 0); y += 28;
            _mz = AddNumeric(panel, "Marker Z", y, 65535, 0); y += 28;
            _mrot = AddNumeric(panel, "Marker Rot", y, 255, 0); y += 32;

            panel.Controls.Add(new Label { Left = 8, Top = y, Width = 100, Text = "Login Blob Hex" });
            _loginHex = new TextBox { Left = 120, Top = y, Width = 520, Height = 100, Multiline = true, ScrollBars = ScrollBars.Vertical };
            panel.Controls.Add(_loginHex);
            y += 108;

            panel.Controls.Add(new Label { Left = 8, Top = y, Width = 100, Text = "Zone Blob Hex" });
            _zoneHex = new TextBox { Left = 120, Top = y, Width = 520, Height = 100, Multiline = true, ScrollBars = ScrollBars.Vertical };
            panel.Controls.Add(_zoneHex);
            y += 108;

            var saveBtn = new Button { Text = "Save", Left = 120, Top = y, Width = 80 };
            var reloadBtn = new Button { Text = "Reload", Left = 210, Top = y, Width = 80 };
            saveBtn.Click += (s, e) => SaveCurrent();
            reloadBtn.Click += (s, e) => ReloadCharacters();
            panel.Controls.Add(saveBtn);
            panel.Controls.Add(reloadBtn);

            Controls.Add(panel);
            Load += (s, e) => ReloadCharacters();
        }

        static NumericUpDown AddNumeric(Panel panel, string label, int y, decimal max = 999999, int decimals = 2)
        {
            panel.Controls.Add(new Label { Left = 8, Top = y + 4, Width = 100, Text = label });
            var n = new NumericUpDown
            {
                Left = 120,
                Top = y,
                Width = 140,
                DecimalPlaces = decimals,
                Minimum = -999999,
                Maximum = max
            };
            panel.Controls.Add(n);
            return n;
        }

        public void ReloadCharacters()
        {
            var selectedId = (_characterPicker.SelectedItem as CharacterRecord)?.Id;
            var chars = DataStore.GetAllAccounts()
                .SelectMany(a => DataStore.GetCharactersByAccountId(a.Id))
                .ToList();

            _characterPicker.DataSource = null;
            _characterPicker.DisplayMember = "";
            _characterPicker.ValueMember = "";
            _characterPicker.DataSource = chars;
            _characterPicker.DisplayMember = nameof(CharacterRecord.Name);
            _characterPicker.ValueMember = nameof(CharacterRecord.Id);

            if (selectedId.HasValue)
            {
                for (int i = 0; i < chars.Count; i++)
                {
                    if (chars[i].Id == selectedId.Value)
                    {
                        _characterPicker.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (_characterPicker.SelectedIndex < 0 && chars.Count > 0)
                _characterPicker.SelectedIndex = 0;
            else
                LoadSelected();
        }

        void LoadSelected()
        {
            if (_characterPicker.SelectedItem is not CharacterRecord character)
                return;

            var state = DataStore.GetPlayerState(character.Id);
            if (state == null)
            {
                state = new PlayerStateRecord
                {
                    CharacterId = character.Id,
                    X = 2572,
                    Y = 4376,
                    Z = -28,
                    Rot = 5.55f
                };
            }

            _x.Value = (decimal)state.X;
            _y.Value = (decimal)state.Y;
            _z.Value = (decimal)state.Z;
            _rot.Value = (decimal)state.Rot;
            _mx.Value = state.MarkerX;
            _my.Value = state.MarkerY;
            _mz.Value = state.MarkerZ;
            _mrot.Value = state.MarkerRot;
            _loginHex.Text = state.LoginBlobHex ?? "";
            _zoneHex.Text = state.ZoneBlobHex ?? "";
        }

        void SaveCurrent()
        {
            if (_characterPicker.SelectedItem is not CharacterRecord character)
                return;

            var state = new PlayerStateRecord
            {
                CharacterId = character.Id,
                X = (float)_x.Value,
                Y = (float)_y.Value,
                Z = (float)_z.Value,
                Rot = (float)_rot.Value,
                MarkerX = (ushort)_mx.Value,
                MarkerY = (ushort)_my.Value,
                MarkerZ = (ushort)_mz.Value,
                MarkerRot = (byte)_mrot.Value,
                LoginBlobHex = _loginHex.Text.Trim(),
                ZoneBlobHex = string.IsNullOrWhiteSpace(_zoneHex.Text) ? null : _zoneHex.Text.Trim()
            };
            DataStore.SavePlayerState(state);
            MessageBox.Show(FindForm(), "Player state saved.", "Player State", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
