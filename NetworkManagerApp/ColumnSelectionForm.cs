using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace NetworkManagerApp
{
    public partial class ColumnSelectionForm : Form
    {
        public List<string> SelectedColumns { get; private set; }

        public ColumnSelectionForm(List<string> allPossibleColumns, List<string> currentlySelectedColumns)
        {
            InitializeComponent();
            SelectedColumns = new List<string>(currentlySelectedColumns); // Initialize with current selection

            foreach (string colName in allPossibleColumns)
            {
                checkedListBoxColumns.Items.Add(colName, currentlySelectedColumns.Contains(colName));
            }

            this.btnOK.Click += BtnOK_Click;
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            SelectedColumns.Clear();
            foreach (var item in checkedListBoxColumns.CheckedItems)
            {
                SelectedColumns.Add(item.ToString());
            }
            // DialogResult is already set to OK for btnOK in the designer.
            // No need to explicitly call this.Close() or set DialogResult here if btnOK is AcceptButton
        }
    }
}
