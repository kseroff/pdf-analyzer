using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using PdfFileAnalyzer;
using PdfFileAnalyzer.Element;
using PdfFileAnalyzer.XObject;

namespace TestPdfFileAnalyzer
{
	public partial class AnalysisScreen : Form
	{
		private enum HeaderColumn
		{
			ObjectNo,
			Object,
			Type,
			Subtype,
			ParentObjectNo,
			ParentObjectIndex,
			FilePos,
			StreamPos,
			StreamLen,
			Columns,
		}

		private readonly PdfReader Reader;
		private DataGridView DataGrid;
		private TextBox filterBox;

		private HashSet<int> offOCGRefs = new HashSet<int>();

		private Dictionary<int, string> ocgNames = new Dictionary<int, string>();

		public AnalysisScreen(PdfDocument document)
		{
			this.Reader = document.Reader;
			InitializeComponent();
		}

		private void OnLoad ( object sender, EventArgs e)
		{
			filterBox = new TextBox
			{
				Name = "filterBox",
				Text = "",
				Dock = DockStyle.Top,
				Height = 30,
				Font = new Font("Arial", 10),
				ForeColor = Color.Gray 
			};

			filterBox.TextChanged += FilterBox_TextChanged;

			Controls.Add(filterBox);

			// Сбор списка выключенных OCG
			if (Reader.Catalog != null && Reader.Catalog.Dictionary != null)
			{
				var rootDict = Reader.Catalog.Dictionary;
				var ocPropsBase = rootDict.FindValue("/OCProperties");
				if (ocPropsBase.IsDictionary)
				{
					var ocProps = ocPropsBase.ToDictionary;
					var defaultBase = ocProps.FindValue("/D");
					if (defaultBase.IsDictionary)
					{
						var defaultDict = defaultBase.ToDictionary;
						var offItems = defaultDict.FindValue("/OFF").ToArrayItems;
						if (offItems != null)
						{
							foreach (var item in offItems)
								if (item.IsReference)
									offOCGRefs.Add(item.ToObjectRefNo);
						}
					}

					var allArray = ocProps.FindValue("/OCGs").ToArrayItems;
					if (allArray != null)
					{
						foreach (var item in allArray)
						{
							if (item.IsReference)
							{
								int no = ((PdfReference)item).ObjectNumber;
								var ocObj = Reader.ToPdfIndirectObject((PdfReference)item);
								if (ocObj?.Dictionary != null)
								{
									string name = ocObj.Dictionary.FindValue("/Name").ToName ?? no.ToString();
									ocgNames[no] = name;
								}
							}
						}
					}
				}
			}

			AddDataGrid();
			LoadDataGrid();
			OnResize(this, null);

            return;
		}

		private void AddDataGrid()
		{
			// add data grid
			DataGrid = new DataGridView
			{
				Name = "DataGrid",
				AllowUserToAddRows = false,
				AllowUserToDeleteRows = false,
				AllowUserToOrderColumns = true,
				AllowUserToResizeRows = false,
				RowHeadersVisible = false,
				MultiSelect = true,
				SelectionMode = DataGridViewSelectionMode.FullRowSelect,
				BackgroundColor = SystemColors.GradientInactiveCaption,
				BorderStyle = BorderStyle.None,
				ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableWithoutHeaderText,
				ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
				EditMode = DataGridViewEditMode.EditProgrammatically,
				AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
				TabStop = true
			};
			DataGrid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
			DataGrid.CellFormatting += new DataGridViewCellFormattingEventHandler(OnCellFormatting);
			DataGrid.CellMouseDoubleClick += new DataGridViewCellMouseEventHandler(OnMouseDoubleClick);

			// add columns
			DataGrid.Columns.Add("ObjectNo", "Object\nNo.");
			DataGrid.Columns.Add("Object", "Object");
			DataGrid.Columns.Add("Type", "Type");
			DataGrid.Columns.Add("Subtype", "Subtype");
			DataGrid.Columns.Add("ParentNo", "ObjStm\nObjNo");
			DataGrid.Columns.Add("ParentIndex", "ObjStm\nIndex");
			DataGrid.Columns.Add("ObjectPos", "Object Pos");
			DataGrid.Columns.Add("StreamPos", "Stream Pos");
			DataGrid.Columns.Add("StreamLen", "Stream Len");
			//DataGrid.Columns.Add("OCGLayer", "OCG Layer");
			//DataGrid.Columns.Add("HasFiles", "Embedded Files");

			DataGridViewCellStyle ObjNoCellStyle = new DataGridViewCellStyle();
			ObjNoCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
			ObjNoCellStyle.WrapMode = DataGridViewTriState.False;
			DataGrid.Columns[(int)HeaderColumn.ObjectNo].DefaultCellStyle = ObjNoCellStyle;

			DataGridViewCellStyle ParentCellStyle = new DataGridViewCellStyle();
			ParentCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
			ParentCellStyle.WrapMode = DataGridViewTriState.False;
			DataGrid.Columns[(int)HeaderColumn.ParentObjectNo].DefaultCellStyle = ParentCellStyle;
			DataGrid.Columns[(int)HeaderColumn.ParentObjectIndex].DefaultCellStyle = ParentCellStyle;

			Controls.Add(DataGrid);

			// force resize
			OnResize(this, null);
			return;
		}

		private void AddInfoRow()
		{
			var infoObj = Reader.TrailerSubDict("/Info");

			if (infoObj == null || infoObj.ObjectType != ObjectType.Dictionary)
				return;

			foreach (DataGridViewRow row in DataGrid.Rows)
			{
				if ((int)row.Cells[(int)HeaderColumn.ObjectNo].Value == infoObj.ObjectNumber)
				{
					// Модернизируем строку
					row.Cells[(int)HeaderColumn.Type].Value = "/Info";
					row.Cells[(int)HeaderColumn.Object].Value = "Dictionary";
					row.DefaultCellStyle.BackColor = Color.LightSteelBlue;

					DataGrid.Rows.Remove(row);
					DataGrid.Rows.Insert(0, row);

					return;
				}
			}
		}

		private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
		{
			// format position and length as integer annd hex
			if (e.Value != null &&
				(e.ColumnIndex == (int)HeaderColumn.FilePos ||
				e.ColumnIndex == (int)HeaderColumn.StreamPos ||
				e.ColumnIndex == (int)HeaderColumn.StreamLen))
			{
				e.Value = string.Format("{0:#,###} (0x{0:X})", (int)e.Value);
				e.FormattingApplied = true;
			}
			return;
		}

		private void LoadDataGrid()
		{
			DataGrid.Rows.Clear();

			foreach (var kv in Reader.ObjectTable.OrderBy(k => k.Key))
			{
				foreach (var obj in kv.Value.OrderBy(o => o.Generation))
				{
					LoadDataGridRow(obj);
				}
			}

			AddInfoRow();

			AdjustParent(20, 680, 0, 472);
			OnResize(null, null);
		}

		private void LoadDataGridRow(PdfIndirectObject ReaderObject)
		{
			int Row = DataGrid.Rows.Add();
			DataGridViewRow ViewRow = DataGrid.Rows[Row];
			ViewRow.Tag = ReaderObject;

			// Установка значений ячеек, как было ранее
			ViewRow.Cells[(int)HeaderColumn.ObjectNo].Value = ReaderObject.ObjectNumber;
			ViewRow.Cells[(int)HeaderColumn.Object].Value = ReaderObject.ObjectDescription();
			if (ReaderObject.PdfObjectType != null)
				ViewRow.Cells[(int)HeaderColumn.Type].Value = ReaderObject.PdfObjectType;

			string ObjectSubtypeStr = ReaderObject.ObjectSubtypeToString();
			if (ObjectSubtypeStr != null)
				ViewRow.Cells[(int)HeaderColumn.Subtype].Value = ObjectSubtypeStr;

			if (ReaderObject.ParentObjectNo != 0)
			{
				ViewRow.Cells[(int)HeaderColumn.ParentObjectNo].Value = ReaderObject.ParentObjectNo;
				if (ReaderObject.PdfObjectType != "/ObjStm")
					ViewRow.Cells[(int)HeaderColumn.ParentObjectIndex].Value = ReaderObject.ParentObjectIndex;
			}

			ViewRow.Cells[(int)HeaderColumn.FilePos].Value = ReaderObject.FilePosition;

			// Добавим обработку /XObject (для изображений)
			if (ReaderObject.PdfObjectType == "/XObject")
			{
				var objectDict = ReaderObject.Dictionary;

				// Обработка /Subtype = /Image
				if (objectDict != null && objectDict.Exists("/Subtype"))
				{
					string subtype = objectDict.FindValue("/Subtype").ToName;
					if (subtype == "/Image")
					{
						// Обрабатываем поток изображения
						var imageStream = objectDict.FindValue("/Filter");
						ViewRow.Cells[(int)HeaderColumn.StreamPos].Value = "Image Stream";
						if (!imageStream.IsEmpty)
						{
							ViewRow.Cells[(int)HeaderColumn.StreamLen].Value = imageStream.ToString();
						}
						else
						{
							ViewRow.Cells[(int)HeaderColumn.StreamLen].Value = "No Stream";
						}
					}
					else if (subtype == "/Form")
					{
						// Для /Form создаём вложенный контекст
						ViewRow.Cells[(int)HeaderColumn.StreamPos].Value = "Form Stream";
					}
				}
			}

			// Далее продолжаем обработку
			if (ReaderObject.ObjectType == ObjectType.Stream)
			{
				ViewRow.Cells[(int)HeaderColumn.StreamPos].Value = ReaderObject.StreamFilePosition;
				ViewRow.Cells[(int)HeaderColumn.StreamLen].Value = ReaderObject.StreamLength;
			}

			bool hidden = false;
			bool jscontains = false;
			var dict = ReaderObject.Dictionary;
			if (dict != null)
			{
				// 1) Аннотации: флаг /F, бит 2 — Hidden
				var fBase = dict.FindValue("/F");
				if (fBase.IsInteger && (((int)((PdfInteger)fBase).IntValue) & 2) != 0)
					hidden = true;

				// 2) Optional Content Group
				var ocBase = dict.FindValue("/OC");
				if (ocBase.IsReference && offOCGRefs.Contains(ocBase.ToObjectRefNo))
					hidden = true;

                // 3) JS — флаг из нового свойства PdfIndirectObject.ContainsJavaScript
                if (ReaderObject.ContainsJavaScript)
					jscontains = true;

			}

			if (hidden || ReaderObject.IsHidden)
			{
				ViewRow.DefaultCellStyle.BackColor = Color.Red;
				ViewRow.DefaultCellStyle.ForeColor = Color.White;
			}
			if (jscontains)
			{
				// например, желтым
				ViewRow.DefaultCellStyle.BackColor = Color.Yellow;
			}
			// Embedded Files
			if (ReaderObject.ContainsEmbeddedFile)
			{
				ViewRow.DefaultCellStyle.BackColor = Color.Red;
				ViewRow.DefaultCellStyle.ForeColor = Color.White;
			}
			// Launch Action
			if (ReaderObject.ContainsLaunchAction)
			{
				ViewRow.DefaultCellStyle.BackColor = Color.Orange;
			}
		}

		private void AdjustParent(int ExtraWidth, int MinWidth, int ExtraHeight, int MinHeight)
		{
			// Вычисляем ширину колонок с учётом дополнительной ширины
			int ReqWidth = ColumnsWidth() + ExtraWidth;

			// Убедимся, что ширина не меньше минимальной требуемой
			if (ReqWidth < MinWidth) 
				ReqWidth = MinWidth;

			// Вычисляем требуемую высоту
			int ReqHeight = DataGrid.ColumnHeadersHeight + ExtraHeight;
			if (DataGrid.Rows.Count == 0)
				ReqHeight += 2 * DataGrid.ColumnHeadersHeight;
			else 
				ReqHeight += (DataGrid.Rows.Count < 4 ? 4 : DataGrid.Rows.Count) * 
					(DataGrid.Rows[0].Height + DataGrid.Rows[0].DividerHeight);

			// Убедимся, что высота не меньше минимальной
			if (ReqHeight < MinHeight) 
				ReqHeight = MinHeight;

			// Получаем форму, на которой расположена таблица
			Form ParentForm = FindForm();

			// Учитываем пространство вне клиентской области формы
			ReqWidth += ParentForm.Bounds.Width - ParentForm.ClientRectangle.Width;
			ReqHeight += ParentForm.Bounds.Height - ParentForm.ClientRectangle.Height;

			// Получаем рабочую область экрана
			Rectangle ScreenWorkingArea = Screen.FromControl(ParentForm).WorkingArea;

			// Убедимся, что требуемая ширина не превышает ширину экрана
			if (ReqWidth > ScreenWorkingArea.Width) 
				ReqWidth = ScreenWorkingArea.Width;

			// Убедимся, что требуемая высота не превышает высоту экрана
			if (ReqHeight > ScreenWorkingArea.Height) 
				ReqHeight = ScreenWorkingArea.Height;

			// Устанавливаем размеры родительской формы
			ParentForm.SetBounds(ScreenWorkingArea.Left + (ScreenWorkingArea.Width - ReqWidth) / 2,
				ScreenWorkingArea.Top + (ScreenWorkingArea.Height - ReqHeight) / 2, ReqWidth, ReqHeight);
			return;
		}

		// Вычисление ширины колонок
		private int ColumnsWidth()
		{
			Graphics GR = CreateGraphics();
			Font GridFont = Font;

			// Определяем дополнительную ширину
			int ExtraWidth = (int)Math.Ceiling(GR.MeasureString("0", GridFont).Width);
			int TotalWidth = 0;

			for (int ColNo = 0; ColNo < (int)HeaderColumn.Columns; ColNo++)
			{
				// Короткое имя для колонки
				DataGridViewTextBoxColumn Col = (DataGridViewTextBoxColumn)DataGrid.Columns[ColNo];
				int ColWidth = (int)Math.Ceiling(GR.MeasureString(Col.HeaderText, GridFont).Width);

				// Проходим по всем строкам в одной колонке
				for (int Row = 0; Row < DataGrid.Rows.Count; Row++)
				{
					// Ширина ячейки
					int CellWidth = (int)Math.Ceiling(GR.MeasureString((string)DataGrid[ColNo, Row].FormattedValue, GridFont).Width);
					if (CellWidth > ColWidth) 
						ColWidth = CellWidth;
				}

				ColWidth += ExtraWidth;
				Col.Width = ColWidth;
				Col.FillWeight = ColWidth;
				Col.MinimumWidth = ColWidth / 2;

				TotalWidth += ColWidth;
			}

			return TotalWidth + SystemInformation.VerticalScrollBarWidth + 1;
		}

		private void OnMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
		{
			if (e.Button != MouseButtons.Left || e.RowIndex < 0) return;
			var obj = (PdfIndirectObject)DataGrid.Rows[e.RowIndex].Tag;
			// Если это XObject /Image — показываем превью без текстовой разбивки
			if (obj.PdfObjectType == "/XObject" && obj.Dictionary.FindValue("/Subtype").ToName == "/Image")
			{
				var xobj = PdfXObject.Create(obj, Reader) as PdfImageXObject;
				var bmp = xobj.GetBitmap();
				var dlg = new DisplayText(DisplayMode.Image, bmp, xobj);
				dlg.ShowDialog();
			}
			else
			{
				var dlg = new DisplayText(DisplayMode.ObjectSummary, Reader, obj, null);
				dlg.ShowDialog();
			}
		}

        private void OnView(object sender, EventArgs e)
        {
            var sel = DataGrid.SelectedRows;
            if (sel == null || sel.Count == 0) return;
            var obj = (PdfIndirectObject)sel[0].Tag;
            if (obj.PdfObjectType == "/XObject" && obj.Dictionary.FindValue("/Subtype").ToName == "/Image")
            {
                var xobj = PdfXObject.Create(obj, Reader) as PdfImageXObject;
                var bmp = xobj.GetBitmap();
                var dlg = new DisplayText(DisplayMode.Image, bmp, xobj);
                dlg.ShowDialog();
            }
            else
            {
                var dlg = new DisplayText(DisplayMode.ObjectSummary, Reader, obj, null);
                dlg.ShowDialog();
            }
        }

		private void OnSummary(object sender, EventArgs e)
		{
			DisplayText Dialog = new DisplayText(DisplayMode.PdfSummary, Reader, null, null);
			Dialog.ShowDialog();
			return;
		}

		private void OnResize (object sender, EventArgs e)
		{

			if (ClientSize.Width == 0) return;

			ButtonsGroupBox.Left = (ClientSize.Width - ButtonsGroupBox.Width) / 2;
			ButtonsGroupBox.Top = ClientSize.Height - ButtonsGroupBox.Height - 4;

			if (DataGrid != null)
			{
				DataGrid.Left = 2;
				DataGrid.Top = filterBox.Bottom + 5;
				DataGrid.Width = ClientSize.Width - 4;
				DataGrid.Height = ButtonsGroupBox.Top - DataGrid.Top - 10;
			}

			return;
		}

		private void FilterBox_TextChanged(object sender, EventArgs e)
		{
			string filterText = filterBox.Text.ToLower();
			FilterDataGrid(filterText);
		}

		private void FilterDataGrid(string filterText)
		{
			foreach (DataGridViewRow row in DataGrid.Rows)
			{
				string objectDescription = row.Cells[(int)HeaderColumn.Object].Value?.ToString().ToLower() ?? "";
				string objectType = row.Cells[(int)HeaderColumn.Type].Value?.ToString().ToLower() ?? "";
				string objectSubtype = row.Cells[(int)HeaderColumn.Subtype].Value?.ToString().ToLower() ?? "";

				if (objectDescription.Contains(filterText) || objectType.Contains(filterText) || objectSubtype.Contains(filterText))
				{
					row.Visible = true;
				}
				else
				{
					row.Visible = false;
				}
			}
		}

		private void OnExit(object sender, EventArgs e)
		{
			Close();
			return;
		}
	}
}

