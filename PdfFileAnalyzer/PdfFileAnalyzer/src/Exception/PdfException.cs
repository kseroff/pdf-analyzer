using System;
using System.Collections.Generic;

namespace PdfFileAnalyzer.Exceptions
{
    public class PdfException : Exception {

        public const String DocumentHasNoPdfCatalogObject = "Document has no PDF Catalog object.";

        public const String DocumentMustBePreClosed = "Document must be preClosed.";

        public const String DocumentForCopyToCannotBeNull = "Document for copyTo cannot be null.";

        public const String DocumentHasNoPages = "Document has no pages.";

        public const String InvalidXrefStream = "Invalid xref stream.";

        public const String InvalidXrefTable = "Invalid xref table.";

        public const String PdfStartxrefNotFound = "PDF startxref not found.";

        public const String PdfVersionNotValid = "PDF version is not valid.";

        public const String TrailerNotFound = "Trailer not found.";

        protected internal Object @object;

        private IList<Object> messageParams;

        public PdfException(String message)
            : base(message)
        {
        }


        public PdfException(Exception cause)
            : this("Unknown PDF exception", cause)
        {
        }

        public PdfException(String message, Object obj)
            : this(message)
        {
            this.@object = obj;
        }

        public PdfException(String message, Exception cause)
            : base(message, cause)
        {
        }
        public PdfException(String message, Exception cause, Object obj)
            : this(message, cause)
        {
            this.@object = obj;
        }

        public override String Message
        {
            get
            {
                if (messageParams == null || messageParams.Count == 0)
                {
                    return base.Message;
                }
                else
                {
                    return string.Format(base.Message, GetMessageParams());
                }
            }
        }

        protected internal virtual Object[] GetMessageParams()
        {
            Object[] parameters = new Object[messageParams.Count];
            for (int i = 0; i < messageParams.Count; i++)
            {
                parameters[i] = messageParams[i];
            }
            return parameters;
        }

    }
}
