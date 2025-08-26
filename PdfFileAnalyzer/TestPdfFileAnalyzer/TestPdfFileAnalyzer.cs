using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using PdfFileAnalyzer;

namespace TestPdfFileAnalyzer
{
    public partial class TestPdfFileAnalyzer : Form
    {
        private List<string> RecentFiles;
        private static readonly string RecentFilesName = "PdfFileAnalyzerFiles.txt";

        public TestPdfFileAnalyzer()
        {
            InitializeComponent();
            return;
        }

        private void OnLoad(object sender, EventArgs e)
        {
#if DEBUG
            // current directory
            string CurDir = Environment.CurrentDirectory;
            int Index = CurDir.IndexOf("bin\\Debug");
            if (Index > 0)
            {
                string WorkDir = string.Concat(CurDir.Substring(0, Index), "Work");
                if (Directory.Exists(WorkDir)) Environment.CurrentDirectory = WorkDir;
            }
#endif

            // program title
            Text = "PDFence — Умный PDF Анализатор от Kseroff";

            // copyright box
            CopyrightTextBox.Rtf =
                "{\\rtf1\\ansi\\deff0\\deftab720{\\fonttbl{\\f0\\fswiss\\fprq2 Verdana;}}" +
                "\\par\\plain\\fs24\\b PDFence\\plain \\fs20 \\par\\par" +
                "PDFence предназначен для чтения, синтаксического анализа и отображения внутренней структуры PDF-файлов.\\par\\par \n" +
                "Автор: Kseroff \\par\\par\n" +
                "Дипломная работа Макарова И.С.}";

            // recent files
            RecentFiles = new List<string>();
            if (File.Exists(RecentFilesName))
            {
                using (StreamReader Reader = new StreamReader(RecentFilesName, Encoding.ASCII))
                {
                    while (true)
                    {
                        string OneFile = Reader.ReadLine();
                        if (OneFile == null) break;
                        if (File.Exists(OneFile)) RecentFiles.Add(OneFile);
                    }
                }
            }
            if (RecentFiles.Count == 0) RecentFilesButton.Enabled = false;

            // exit
            return;
        }

        private void OnOpenPdfFile(object sender, EventArgs e)
        {
            // get file name
            OpenFileDialog OFD = new OpenFileDialog();
            OFD.InitialDirectory = ".";
            OFD.Filter = "PDF File (*.pdf)|*.PDF";
            OFD.RestoreDirectory = true;
            if (OFD.ShowDialog() != DialogResult.OK) return;

            // open the file
            OpenPdfFile(OFD.FileName);
            return;
        }

        private void OnRecentFiles(object sender, EventArgs e)
        {
            RecentFilesSelector Selector = new RecentFilesSelector(RecentFiles);
            if (Selector.ShowDialog(this) == DialogResult.OK)
            {
                OpenPdfFile(Selector.FileName);
            }
            return;
        }

        private void OpenPdfFile(string FileName)
        {
            // создаём обёртку-документ
            var document = new PdfDocument(FileName);

            // пытаемся загрузить (без пароля)
            bool success = document.Load();
            if (!success)
            {
                var status = document.Reader?.DecryptionStatus ?? DecryptionStatus.Unsupported;

                if (status == DecryptionStatus.Unsupported)
                {
                    MessageBox.Show("PDF документ не расшифрован. Не поодерживается метод данный метод шифрования");
                    return;
                }

                // спрашиваем пароль, пока не откроется
                while (true)
                {
                    Password PasswordDialog = new Password();
                    DialogResult DResult = PasswordDialog.ShowDialog();
                    if (DResult != DialogResult.OK) return;
                    if (document.Load(PasswordDialog.PasswordStr)) break;
                }
            }

            // достаём внутренний PdfReader
            var reader = document.Reader;

            int totalObjects = reader.ObjectTable.Values.Sum(list => list.Count);

            // формируем сообщение об открытии
            string OpenMsg = string.Format(
            "PDF-документ успешно загружен.\r\n\r\n" +
            "Версия PDF: {0}\r\n" +
            "Количество страниц: {1}\r\n" +
            "Количество объектов: {2:#,##0}",
            document.PdfVersion,
            document.PageCount,
            totalObjects);

            if (reader.DecryptionStatus == DecryptionStatus.OwnerPassword)
                OpenMsg += "\r\n\r\nДокумент расшифрован с помощью пароля владельца.";
            else if (reader.DecryptionStatus == DecryptionStatus.UserPassword)
                OpenMsg += "\r\n\r\nДокумент расшифрован с помощью пользовательского пароля.";
            if (reader.InvalidPdfFile)
                OpenMsg += "\r\n\r\nНекорректный PDF-документ. Не соответствует стандарту.";

            MessageBox.Show(OpenMsg);

            // отображаем экран анализа
            AnalysisScreen AnalyzerForm = new AnalysisScreen(document);
            AnalyzerForm.Text = FileName;
            AnalyzerForm.ShowDialog();

            // освобождаем ресурсы
            reader.Dispose();

            // обновляем список недавних
            RecentFiles.Insert(0, FileName);
            for (int Index = 1; Index < RecentFiles.Count; Index++)
            {
                if (string.Compare(RecentFiles[Index], FileName, true) != 0) continue;
                RecentFiles.RemoveAt(Index);
                break;
            }
            if (RecentFiles.Count > 20) RecentFiles.RemoveRange(20, RecentFiles.Count - 20);
            RecentFilesButton.Enabled = true;
            return;
        }

        private void OnClosing(object sender, FormClosingEventArgs e)
        {
            // сохраняем недавние
            using (StreamWriter Writer = new StreamWriter(RecentFilesName, false, Encoding.UTF8))
            {
                foreach (string FileName in RecentFiles) Writer.WriteLine(FileName);
            }
            return;
        }

        private void CopyrightTextBox_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
