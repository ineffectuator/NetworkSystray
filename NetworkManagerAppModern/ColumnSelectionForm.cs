using System;
using System.Collections.Generic;
using System.Windows.Forms; // Required for Form and other UI elements
using System.Linq; // Required for LINQ methods like .Contains()

namespace NetworkManagerAppModern
{
    public partial class ColumnSelectionForm : Form
    {
        public List<string> SelectedColumnKeys { get; private set; }

        // Constructor takes all possible column keys and the currently selected ones
        public ColumnSelectionForm(List<string> allPossibleColumnKeys, List<string> currentlySelectedColumnKeys)
        {
            InitializeComponent();
            SelectedColumnKeys = new List<string>(currentlySelectedColumnKeys); // Initialize with current selection

            foreach (string colKey in allPossibleColumnKeys)
            {
                // Add the key to the listbox, and check it if it's in the currently selected list
                checkedListBoxColumns.Items.Add(colKey, currentlySelectedColumnKeys.Contains(colKey));
            }

            // Attach event handler for OK button click
            this.btnOK.Click += BtnOK_Click;
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            SelectedColumnKeys.Clear(); // Clear previous selections
            // Add all checked items (which are column keys) to the SelectedColumnKeys list
            foreach (var item in checkedListBoxColumns.CheckedItems)
            {
                if (item != null) // Ensure item is not null before calling ToString()
                {
                    SelectedColumnKeys.Add(item.ToString()!); // Null-forgiving operator for item.ToString()
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
