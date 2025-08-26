using PdfFileAnalyzer.Element;

namespace PdfFileAnalyzer
	{

	public enum ObjectType
		{
		Free,  // Object is not in use
		Other, // Object is other than a dictionary or a stream
		Dictionary, // Object is a dictionary
		Stream, // Object is a stream
	}

	/// <summary>
	/// PDF indirect object base class
	/// </summary>
	public class PdfObject
		{
		/// <summary>
		/// Gets indirect object number
		/// </summary>
		public int ObjectNumber { get; internal set; }

		/// <summary>
		/// Gets object's file position
		/// </summary>
		public int FilePosition { get; internal set; }

		/// <summary>
		/// Gets parent object number (for object stream)
		/// </summary>
		public int ParentObjectNo { get; internal set; }

		/// <summary>
		/// Gets parent object index (for object stream)
		/// </summary>
		public int ParentObjectIndex { get; internal set; }

		/// <summary>
		/// Gets object type
		/// </summary>
		public ObjectType ObjectType { get; internal set; }

		/// <summary>
		/// Gets object type
		/// </summary>
		public string PdfObjectType { get; internal set; }

		/// <summary>
		/// Object dictionary
		/// </summary>
		public PdfDictionary Dictionary { get; internal set; }

		/// <summary>
		/// Object value if ObjectType = Other
		/// </summary>
		public PdfBase Value { get; internal set; }

		/// <summary>
		/// PDF Object description
		/// </summary>
		public string ObjectDescription()
		{
			switch (ObjectType)
			{
				case ObjectType.Free:
					return "Free";
				case ObjectType.Other:
					return Value.TypeToString();
				case ObjectType.Dictionary:
					return "Dictionary";
				case ObjectType.Stream:
					return "Stream";
				default:
					return "Error";
			}
		}

	}
}
