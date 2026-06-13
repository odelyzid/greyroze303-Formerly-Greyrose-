using System;
using System.Windows.Forms;
using Greyrose.Data;

namespace Greyrose.UI
{
    public class AccountEditorPanel : UserControl
    {
        DataGridView _grid;
        Button _addBtn;
        Button _editBtn;
        Button _deleteBtn;
        Button _refreshBtn;

        public event Action AccountsChanged;

        public AccountEditorPanel()
        {
            Dock = DockStyle.Fill;

            var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 36, Padding = new Padding(4) };
            _addBtn = new Button { Text = "Add", Width = 70 };
            _editBtn = new Button { Text = "Edit", Width = 70 };
            _deleteBtn = new Button { Text = "Delete", Width = 70 };
            _refreshBtn = new Button { Text = "Refresh", Width = 70 };
            top.Controls.AddRange(new Control[] { _addBtn, _editBtn, _deleteBtn, _refreshBtn });

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

            _addBtn.Click += (s, e) => EditAccount(null);
            _editBtn.Click += (s, e) =>
            {
                if (_grid.CurrentRow?.DataBoundItem is AccountRecord a)
                    EditAccount(a);
            };
            _deleteBtn.Click += (s, e) => DeleteSelected();
            _refreshBtn.Click += (s, e) => Reload();

            Load += (s, e) => Reload();
        }

        public void Reload()
        {
            _grid.DataSource = null;
            _grid.DataSource = DataStore.GetAllAccounts();
            if (_grid.Columns.Count > 0)
            {
                _grid.Columns["Id"].Visible = true;
                _grid.Columns["UserGid"].HeaderText = "User GID";
                _grid.Columns["PassKey"].HeaderText = "Pass Key";
                _grid.Columns["PurchasedSlots"].HeaderText = "Purchased Slots";
            }
        }

        void EditAccount(AccountRecord existing)
        {
            using var form = new Form
            {
                Text = existing == null ? "Add Account" : "Edit Account",
                Width = 420,
                Height = 260,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var gid = new TextBox { Left = 120, Top = 16, Width = 260, Text = existing?.UserGid.ToString() ?? "" };
            var user = new TextBox { Left = 120, Top = 52, Width = 260, Text = existing?.Username ?? "" };
            var pass = new TextBox { Left = 120, Top = 88, Width = 260, Text = existing?.PassKey ?? "" };
            var slots = new NumericUpDown { Left = 120, Top = 124, Width = 100, Minimum = 0, Maximum = 99, Value = existing?.PurchasedSlots ?? 0 };

            form.Controls.AddRange(new Control[]
            {
                new Label { Left = 16, Top = 20, Width = 100, Text = "User GID" }, gid,
                new Label { Left = 16, Top = 56, Width = 100, Text = "Username" }, user,
                new Label { Left = 16, Top = 92, Width = 100, Text = "Pass Key" }, pass,
                new Label { Left = 16, Top = 128, Width = 100, Text = "Slots" }, slots
            });

            var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 220, Top = 170, Width = 75 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 305, Top = 170, Width = 75 };
            form.Controls.AddRange(new Control[] { ok, cancel });
            form.AcceptButton = ok;
            form.CancelButton = cancel;

            if (form.ShowDialog(FindForm()) != DialogResult.OK)
                return;

            if (!long.TryParse(gid.Text.Trim(), out long userGid))
            {
                MessageBox.Show(FindForm(), "Invalid User GID.", "Accounts", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (existing == null)
            {
                DataStore.InsertAccount(new AccountRecord
                {
                    UserGid = userGid,
                    Username = user.Text.Trim(),
                    PassKey = pass.Text.Trim(),
                    PurchasedSlots = (int)slots.Value
                });
            }
            else
            {
                existing.UserGid = userGid;
                existing.Username = user.Text.Trim();
                existing.PassKey = pass.Text.Trim();
                existing.PurchasedSlots = (int)slots.Value;
                DataStore.UpdateAccount(existing);
            }

            Reload();
            AccountsChanged?.Invoke();
        }

        void DeleteSelected()
        {
            if (_grid.CurrentRow?.DataBoundItem is not AccountRecord a)
                return;
            if (MessageBox.Show(FindForm(), $"Delete account '{a.Username}' and all characters?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            DataStore.DeleteAccount(a.Id);
            Reload();
            AccountsChanged?.Invoke();
        }
    }
}
