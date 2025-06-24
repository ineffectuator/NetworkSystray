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
            checkedListBoxColumns.ItemCheck += CheckedListBoxColumns_ItemCheck;

            // Attach event handler for OK button click
            this.btnOK.Click += BtnOK_Click;
        }

        private void CheckedListBoxColumns_ItemCheck(object? sender, ItemCheckEventArgs e)
        {
            // Prevent unchecking of mandatory columns
            if (e.CurrentValue == CheckState.Checked && e.NewValue == CheckState.Unchecked)
            {
                string? itemText = checkedListBoxColumns.Items[e.Index]?.ToString();
                if (itemText != null && _mandatoryColumnKeys.Contains(itemText))
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
