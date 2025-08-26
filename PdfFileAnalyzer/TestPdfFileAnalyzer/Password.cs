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
	public partial class Password : Form
	{
		public string PasswordStr;

		public Password()
		{
			InitializeComponent();
		}

		private void OnLoad(object sender, EventArgs e)
		{
			OK_Button.Enabled = false;
			return;
		}

		private void OnTextChanged(object sender, EventArgs e)
		{
			OK_Button.Enabled = !string.IsNullOrEmpty(PasswordTextBox.Text);
			return;
		}

		private void OnClosing(object sender, FormClosingEventArgs e)
		{
			if (DialogResult == DialogResult.OK) PasswordStr = PasswordTextBox.Text;
			return;
		}

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
