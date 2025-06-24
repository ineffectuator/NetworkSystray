using System;
using System.Collections.Generic;
using System.Windows.Forms; // Required for Form and other UI elements
using System.Linq; // Required for LINQ methods like .Contains()

namespace NetworkManagerAppModern
{
    public partial class ColumnSelectionForm : Form
    {
        public List<string> SelectedColumnKeys { get; private set; }
        private List<string> _mandatoryColumnKeys;

        // Constructor takes all possible column keys and the currently selected ones
        public ColumnSelectionForm(List<string> allPossibleColumnKeys, List<string> currentlySelectedColumnKeys)
        {
            InitializeComponent();
            SelectedColumnKeys = new List<string>(currentlySelectedColumnKeys); // Initialize with current selection

            _mandatoryColumnKeys = new List<string> { "Name", "AdminState", "OperationalState" };

            for (int i = 0; i < allPossibleColumnKeys.Count; i++)
            {
                string colKey = allPossibleColumnKeys[i];
                bool isSelected = currentlySelectedColumnKeys.Contains(colKey);
                bool isMandatory = _mandatoryColumnKeys.Contains(colKey);

                checkedListBoxColumns.Items.Add(colKey, isMandatory || isSelected);

                if (isMandatory)
                {
                    // Ensure the item is checked
                    checkedListBoxColumns.SetItemChecked(checkedListBoxColumns.Items.Count - 1, true);
                    // To disable, we need to handle the ItemCheck event.
                    // The CheckedListBox doesn't have a direct way to disable individual items.
                }
            }
            // Handle ItemCheck to prevent unchecking mandatory columns
            // checkedListBoxColumns.ItemCheck += CheckedListBoxColumns_ItemCheck; // Replaced by MouseUp logic

            // Attach event handler for OK button click
            this.btnOK.Click += BtnOK_Click;
            // Attach event handler for MouseUp for single-click toggle
            this.checkedListBoxColumns.MouseUp += CheckedListBoxColumns_MouseUp;
        }

        private void CheckedListBoxColumns_MouseUp(object? sender, MouseEventArgs e)
        {
            int index = checkedListBoxColumns.IndexFromPoint(e.Location);
            if (index != CheckedListBox.NoMatches) // An item was clicked
            {
                string? itemText = checkedListBoxColumns.Items[index]?.ToString();
                if (itemText != null)
                {
                    if (_mandatoryColumnKeys.Contains(itemText))
                    {
                        // For mandatory items, ensure they are always checked.
                        // The click might have been on the text part, not the checkbox.
                        // The default behavior might try to uncheck it before ItemCheck prevents it.
                        // By setting it here, we ensure it remains checked visually.
                        if (!checkedListBoxColumns.GetItemChecked(index))
                        {
                            checkedListBoxColumns.SetItemChecked(index, true);
                        }
                        return; // Do not toggle mandatory columns
                    }

                    // For non-mandatory items, toggle their check state
                    bool currentCheckedState = checkedListBoxColumns.GetItemChecked(index);
                    checkedListBoxColumns.SetItemChecked(index, !currentCheckedState);
                }
            }
        }

        // Original ItemCheck logic might still be useful as a safeguard,
        // but the primary interaction is now handled by MouseUp.
        // If MouseUp correctly handles mandatory columns, this might be redundant or could be simplified.
        // For now, let's keep it to ensure mandatory columns cannot be unchecked by other means.
        private void CheckedListBoxColumns_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            // Prevent unchecking of mandatory columns
            string? itemText = checkedListBoxColumns.Items[e.Index]?.ToString();
            if (itemText != null && _mandatoryColumnKeys.Contains(itemText))
            {
                if (e.NewValue == CheckState.Unchecked)
                {
                    e.NewValue = CheckState.Checked; // Keep it checked
                }
            }
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            SelectedColumnKeys.Clear(); // Clear previous selections

            // Add mandatory columns first
            foreach (string mandatoryKey in _mandatoryColumnKeys)
            {
                if (!SelectedColumnKeys.Contains(mandatoryKey)) // Should always be true after clearing, but good practice
                {
                    SelectedColumnKeys.Add(mandatoryKey);
                }
            }

            // Add other checked items
            for (int i = 0; i < checkedListBoxColumns.Items.Count; i++)
            {
                string? itemText = checkedListBoxColumns.Items[i]?.ToString();
                if (itemText != null && !_mandatoryColumnKeys.Contains(itemText) && checkedListBoxColumns.GetItemChecked(i))
                {
                    if (!SelectedColumnKeys.Contains(itemText)) // Avoid duplicates if somehow added
                    {
                        SelectedColumnKeys.Add(itemText);
                    }
                }
            }

            // DialogResult is already set to OK for btnOK in the designer if it's the AcceptButton.
            // Explicitly setting it here is also fine and makes the intent clear.
            this.DialogResult = DialogResult.OK;
            this.Close(); // Close the form
        }

        // No specific handler needed for btnCancel if its DialogResult is set to Cancel in the designer,
        // as that will automatically close the form and return DialogResult.Cancel.
    }
}
