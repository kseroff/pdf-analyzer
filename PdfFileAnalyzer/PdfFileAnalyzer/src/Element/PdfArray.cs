using System.Collections.Generic;

namespace PdfFileAnalyzer.Element
	{

	public class PdfArray : PdfBase
		{
		public List<PdfBase> Items;

		public PdfArray(params PdfBase[] ArrayItems)
			{
			this.Items = new List<PdfBase>(ArrayItems);
			return;
			}

		public void Add(PdfBase Obj)
			{
			Items.Add(Obj);
			return;
			}

		public PdfBase[] ArrayItems
			{
			get
				{
				return Items.ToArray();
				}
			}

		public override string TypeToString()
			{
			return "Array";
			}
		}
	}
