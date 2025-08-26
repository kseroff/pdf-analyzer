using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;


namespace TestPdfFileAnalyzer
{
	/// <summary>
	/// Recent files selector class
	/// </summary>
	public partial class RecentFilesSelector : Form
	{
		/// <summary>
		/// Selected file name
		/// </summary>
		public string FileName;

		private readonly List<string> RecentFiles;

		/// <summary>
		/// Recent files selector constructor
		/// </summary>
		/// <param name="RecentFiles"></param>
		public RecentFilesSelector
				(
				List<string> RecentFiles
				)
		{
			this.RecentFiles = RecentFiles;
			InitializeComponent();
			return;
		}

		private void OnLoad(object sender, EventArgs e)
		{
			foreach (string OneFile in RecentFiles) FilesListBox.Items.Add(OneFile);
			FilesListBox.SelectedIndex = 0;
			return;
		}

		private void ONOK_Button(object sender, EventArgs e)
		{
			FileName = (string)FilesListBox.SelectedItem;
			DialogResult = DialogResult.OK;
			Close();
			return;
		}

		private void OnMouseDoubleClick(object sender, MouseEventArgs e)
		{
			if (sender != FilesListBox) return;

			int Index = FilesListBox.IndexFromPoint(e.Location);
			if (Index >= 0 && Index < FilesListBox.Items.Count)
			{
				FileName = (string)FilesListBox.Items[Index];
				DialogResult = DialogResult.OK;
				Close();
			}
			return;
		}

		private void OnCancel_Button(object sender, EventArgs e)
		{
			DialogResult = DialogResult.Cancel;
			return;
		}
	}
}

