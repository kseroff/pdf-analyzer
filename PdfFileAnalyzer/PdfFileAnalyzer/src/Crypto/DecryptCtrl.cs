using PdfFileAnalyzer.Element;

namespace PdfFileAnalyzer.Crypto
{
	// Decrypt control
	internal class DecryptCtrl
		{
		internal CryptoEngine Encryption;
		internal int ObjectNumber;
		internal PdfDictionary EncryptionDict;

		internal DecryptCtrl
				(
				CryptoEngine Encryption,
				int ObjectNumber,
				PdfDictionary EncryptionDict
				)
			{
			this.Encryption = Encryption;
			this.ObjectNumber = ObjectNumber;
			this.EncryptionDict = EncryptionDict;
			return;
			}
		}
	}
