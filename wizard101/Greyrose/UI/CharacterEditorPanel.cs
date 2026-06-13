using System;
using System.Linq;
using System.Windows.Forms;
using Greyrose.Data;

namespace Greyrose.UI
{
    public class CharacterEditorPanel : UserControl
    {
        ComboBox _accountFilter;
        DataGridView _grid;
        Button _addBtn;
        Button _editBtn;
        Button _deleteBtn;
        Button _refreshBtn;

        public CharacterEditorPanel()
        {
            Dock = DockStyle.Fill;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(4) };
            top.Controls.Add(new Label { Text = "Account:", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
            _accountFilter = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
            _addBtn = new Button { Text = "Add", Width = 70 };
            _editBtn = new Button { Text = "Edit", Width = 70 };
            _deleteBtn = new Button { Text = "Delete", Width = 70 };
            _refreshBtn = new Button { Text = "Refresh", Width = 70 };
            top.Controls.AddRange(new Control[] { _accountFilter, _addBtn, _editBtn, _deleteBtn, _refreshBtn });

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            Controls.Add(_grid);
            Controls.Add(top);

            _accountFilter.SelectedIndexChanged += (s, e) => ReloadCharacters();
            _addBtn.Click += (s, e) => EditCharacter(null);
            _editBtn.Click += (s, e) =>
            {
                if (_grid.CurrentRow?.DataBoundItem is CharacterRecord c)
                    EditCharacter(c);
            };
            _deleteBtn.Click += (s, e) => DeleteSelected();
            _refreshBtn.Click += (s, e) => ReloadAll();

            Load += (s, e) => ReloadAll();
        }

        public void ReloadAccounts()
        {
            var selectedId = (_accountFilter.SelectedItem as AccountRecord)?.Id;
            _accountFilter.DataSource = null;
            _accountFilter.DisplayMember = "";
            _accountFilter.ValueMember = "";

            var accounts = DataStore.GetAllAccounts();
            _accountFilter.DataSource = accounts;
            _accountFilter.DisplayMember = nameof(AccountRecord.Username);
            _accountFilter.ValueMember = nameof(AccountRecord.Id);

            if (selectedId.HasValue)
            {
                for (int i = 0; i < accounts.Count; i++)
                {
                    if (accounts[i].Id == selectedId.Value)
                    {
                        _accountFilter.SelectedIndex = i;
                        break;
                    }
                }
            }
            if (_accountFilter.SelectedIndex < 0 && accounts.Count > 0)
                _accountFilter.SelectedIndex = 0;
            ReloadCharacters();
        }

        void ReloadAll()
        {
            ReloadAccounts();
        }

        void ReloadCharacters()
        {
            if (_accountFilter.SelectedItem is not AccountRecord account)
            {
                _grid.DataSource = null;
                return;
            }
            _grid.DataSource = null;
            _grid.DataSource = DataStore.GetCharactersByAccountId(account.Id);
            if (_grid.Columns.Count > 0)
            {
                _grid.Columns["CharGid"].HeaderText = "Char GID";
                _grid.Columns["ZoneName"].HeaderText = "Zone";
                _grid.Columns["ZoneGid"].HeaderText = "Zone GID";
                _grid.Columns["CharacterInfoHex"].HeaderText = "Char Info Hex";
            }
        }

        void EditCharacter(CharacterRecord existing)
        {
            AccountRecord account = null;
            if (existing == null)
            {
                if (_accountFilter.SelectedItem is not AccountRecord selected)
                {
                    MessageBox.Show(FindForm(), "Select an account first.", "Characters", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                account = selected;
            }

            using var form = new Form
            {
                Text = existing == null ? "Add Character" : "Edit Character",
                Width = 520,
                Height = 520,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            int y = 16;
            TextBox AddField(string label, string value, int height = 24)
            {
                form.Controls.Add(new Label { Left = 16, Top = y + 4, Width = 100, Text = label });
                var tb = new TextBox { Left = 120, Top = y, Width = 360, Height = height, Text = value ?? "", Multiline = height > 24, ScrollBars = height > 24 ? ScrollBars.Vertical : ScrollBars.None };
                form.Controls.Add(tb);
                y += height + 8;
                return tb;
            }

            var gid = AddField("Char GID", existing?.CharGid.ToString() ?? DefaultGameData.DefaultCharGid.ToString());
            var name = AddField("Name", existing?.Name ?? "");
            var slot = AddField("Slot", (existing?.Slot ?? 0).ToString());
            var zone = AddField("Zone Name", existing?.ZoneName ?? DefaultGameData.DefaultZoneName);
            var zoneGid = AddField("Zone GID", existing?.ZoneGid.ToString() ?? DefaultGameData.DefaultZoneGid.ToString());
            var location = AddField("Location", existing?.Location ?? DefaultGameData.DefaultLocation);
            var hex = AddField("Char Info Hex", existing?.CharacterInfoHex ?? DefaultGameData.DefaultCharacterInfoHex, 120);

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 320, Top = y + 8, Width = 75 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 405, Top = y + 8, Width = 75 };
            form.Controls.AddRange(new Control[] { ok, cancel });
            form.ClientSize = new System.Drawing.Size(500, y + 50);
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            if (form.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            if (!long.TryParse(gid.Text.Trim(), out long charGid) ||
                !int.TryParse(slot.Text.Trim(), out int slotNum) ||
                !long.TryParse(zoneGid.Text.Trim(), out long zGid))
            {
                MessageBox.Show(FindForm(), "Invalid numeric field.", "Characters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var record = existing ?? new CharacterRecord();
            if (existing == null)
                record.AccountId = account.Id;
            record.CharGid = charGid;
            record.Name = name.Text.Trim();
            record.Slot = slotNum;
            record.ZoneName = zone.Text.Trim();
            record.ZoneGid = zGid;
            record.Location = location.Text.Trim();
            record.CharacterInfoHex = hex.Text.Trim();
            record.CharacterInfoHex = CharacterInfoCodec.PrepareForClientHex(record);

            bool usedDefaultTemplate = CharacterInfoCodec.IsDefaultTemplate(hex.Text.Trim());
            if (usedDefaultTemplate)
            {
                MessageBox.Show(FindForm(),
                    "This character uses the default Ravenwood template blob. " +
                    "Appearance and name come from that binary data, not the Name field alone. " +
                    "Use NEW in-game to create a character with custom appearance/name.",
                    "Character template",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }

            if (existing == null)
                DataStore.InsertCharacter(record);
            else
                DataStore.UpdateCharacter(record);

            ReloadCharacters();
        }

        void DeleteSelected()
        {
            if (_grid.CurrentRow?.DataBoundItem is not CharacterRecord c)
                return;
            if (MessageBox.Show(FindForm(), $"Delete character '{c.Name}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            DataStore.DeleteCharacter(c.Id);
            DataStore.DeletePlayerState(c.Id);
            ReloadCharacters();
        }
    }
}
