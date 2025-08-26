namespace PdfFileAnalyzer.Element
{
	/// PDF contents operator class
	public class PdfOp : PdfBase
		{
		/// <summary>
		/// Gets operator enumeration
		/// </summary>
		public Operator OpValue;

		public PdfBase[] ArgumentArray;

		public PdfOp(Operator OpValue)
			{
			this.OpValue = OpValue;
			return;
			}

		/// <summary>
		/// Convert output control to byte array
		/// </summary>
		public override void ToByteArray(OutputCtrl Ctrl)
			{
			if(OpValue != Operator.BeginInlineImage)
				{
				// add arguments
				if(ArgumentArray != null) foreach(PdfBase Arg in ArgumentArray)
					{
					Arg.ToByteArray(Ctrl);
					Ctrl.Add(' ');
					}
				// add code
				Ctrl.AppendText(OpCtrl.OperatorCode(OpValue));
				Ctrl.Add(' ');
				Ctrl.Add('%');
				Ctrl.Add(' ');
				Ctrl.AppendText(OpValue.ToString());
				Ctrl.AddEol();
				return;
				}

			Ctrl.Add('B');
			Ctrl.Add('I');
			Ctrl.Add(' ');
			foreach(PdfKeyValue KeyValue in ((PdfDictionary) ArgumentArray[0]).KeyValueArray)
				{
				Ctrl.AppendText(KeyValue.Key);
				KeyValue.Value.ToByteArray(Ctrl);
				}

			Ctrl.Add(' ');
			Ctrl.Add('I');
			Ctrl.Add('D');
			
			Ctrl.AppendText("INLINE IMAGE DATA EI % InlineImage");
			Ctrl.AddEol();
			return;
			}

		/// <summary>
		/// Object type to string
		/// </summary>
		/// <returns>Operator</returns>
		public override string TypeToString()
			{
			return "Operator";
			}
		}
	}
