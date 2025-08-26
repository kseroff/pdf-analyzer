
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace PdfFileAnalyzer.Crypto
	{
	public enum EncryptionType
		{
		Aes128, // AES 128 bits
		Standard128, // Standard 128 bits
		// Вместо HMAC-SHA256, используется HMAC-SHA1. 
		//Это не соответствует стандарту AES-256 в PDF 2.0, который требует именно SHA-256
		// Решается подключение BouncyCastle 
		//Aes256, // AES 256 bits 
		Unsupported, // No support for encryption method
	}

	/// <summary>
	/// PDF reader разрешающие флаги перечисления
	/// </summary>
	public enum Permission
		{
		None = 0, // No permission flags
		LowQalityPrint = 4,     // Low quality print (bit 3)
		ModifyContents = 8,     // Modify contents (bit 4)
		ExtractContents = 0x10, // Extract contents (bit 5)
		Annotation = 0x20,      // Annotation (bit 6)
		Interactive = 0x100,    // Interactive (bit 9)
		Accessibility = 0x200,  // Accessibility (bit 10)
		AssembleDoc = 0x400,    // Assemble document (bit 11)
		Print = 0x804,          // Print (bit 12 plus bit 3)
		All = 0xf3c,            // bits 3, 4, 5, 6, 9, 10, 11, 12
		}

	public class CryptoEngine : IDisposable
		{
		internal const int PermissionBase = unchecked((int)0xfffff0c0);

		internal DecryptionStatus DecryptionStatus;
		internal byte[] DocumentID;
		internal EncryptionType EncryptionType;
		internal int Permissions;
		internal byte[] UserKey;
		internal byte[] OwnerKey;
		internal byte[] MasterKey;
		internal MD5 MD5 = MD5.Create();
		internal Aes AES = Aes.Create();

		//AES256
		internal byte[] UserKeyEncrypted;
		internal byte[] OwnerKeyEncrypted;
		internal byte[] PermsEncrypted;
		internal byte[] UValidationSalt;
		internal byte[] UKeySalt;
		internal byte[] OValidationSalt;
		internal byte[] OKeySalt;

		internal static readonly byte[] PasswordPad =
			{
		(byte) 0x28, (byte) 0xBF, (byte) 0x4E, (byte) 0x5E, (byte) 0x4E, (byte) 0x75, (byte) 0x8A, (byte) 0x41,
		(byte) 0x64, (byte) 0x00, (byte) 0x4E, (byte) 0x56, (byte) 0xFF, (byte) 0xFA, (byte) 0x01, (byte) 0x08,
		(byte) 0x2E, (byte) 0x2E, (byte) 0x00, (byte) 0xB6, (byte) 0xD0, (byte) 0x68, (byte) 0x3E, (byte) 0x80,
		(byte) 0x2F, (byte) 0x0C, (byte) 0xA9, (byte) 0xFE, (byte) 0x64, (byte) 0x53, (byte) 0x69, (byte) 0x7A
		};

		internal static readonly byte[] Salt = { (byte)0x73, (byte)0x41, (byte)0x6c, (byte)0x54 };

		internal CryptoEngine
				(
				EncryptionType EncryptionType,
				byte[] DocumentID,
				int Permissions,
				byte[] UserKey = null,
				byte[] OwnerKey = null
				)
			{
			this.DocumentID = DocumentID;
			this.Permissions = Permissions;
			this.UserKey = UserKey;
			this.OwnerKey = OwnerKey;
			this.EncryptionType = EncryptionType;
			return;
			}

		internal byte[] ProcessCrypto(byte[] input, Func<ICryptoTransform> transformer)
		{
			// Используем CryptoStream для шифрования/дешифрования
			using (var outputStream = new MemoryStream())
			using (var cryptoStream = new CryptoStream(outputStream, transformer(), CryptoStreamMode.Write))
			{
				cryptoStream.Write(input, 0, input.Length);
				cryptoStream.FlushFinalBlock(); // Завершаем поток

				return outputStream.ToArray();
			}
		}

		internal byte[] EncryptByteArray(int objectNumber, byte[] plainText)
		{
			// Генерируем ключ шифрования
			byte[] encryptionKey = CreateEncryptionKey(objectNumber);

			if (EncryptionType == EncryptionType.Aes128)
			{
				AES.Key = encryptionKey;

				// Создаём новый IV (вектор инициализации)
				AES.GenerateIV();
				byte[] cipherText = new byte[plainText.Length + 16];  // Увеличиваем размер на 16 байт для хранения IV

				// Копируем IV в начало результата
				Array.Copy(AES.IV, 0, cipherText, 0, 16);

				// Шифруем
				return ProcessCrypto(plainText, () => AES.CreateEncryptor());
			}
			else
			{
				byte[] cipherText = (byte[])plainText.Clone(); // Клонируем массив
				EncryptRC4(encryptionKey, cipherText); // Используем RC4
				return cipherText;
			}
		}

		internal byte[] DecryptByteArray(int objectNumber, byte[] cipherText)
		{
			// Генерируем ключ шифрования
			byte[] encryptionKey = CreateEncryptionKey(objectNumber);

			if (EncryptionType == EncryptionType.Aes128)
			{
				AES.Key = encryptionKey;

				// Извлекаем IV из первых 16 байт
				byte[] iv = new byte[16];
				Array.Copy(cipherText, 0, iv, 0, 16);
				AES.IV = iv;

				// Дешифруем
				byte[] encryptedData = new byte[cipherText.Length - 16];
				Array.Copy(cipherText, 16, encryptedData, 0, encryptedData.Length);

				return ProcessCrypto(encryptedData, () => AES.CreateDecryptor());
			}
			else
			{
				byte[] decryptedData = (byte[])cipherText.Clone(); // Клонируем массив
				EncryptRC4(encryptionKey, decryptedData); // Используем RC4
				return decryptedData;
			}
		}

		internal byte[] CreateEncryptionKey(int ObjectNumber)
			{
			byte[] HashInput = new byte[MasterKey.Length + 5 + (EncryptionType == EncryptionType.Aes128 ? Salt.Length : 0)];
			int Ptr = 0;
			Array.Copy(MasterKey, 0, HashInput, Ptr, MasterKey.Length);
			Ptr += MasterKey.Length;
			HashInput[Ptr++] = (byte)ObjectNumber;
			HashInput[Ptr++] = (byte)(ObjectNumber >> 8);
			HashInput[Ptr++] = (byte)(ObjectNumber >> 16);
			HashInput[Ptr++] = 0;   // Для этой библиотеки значение Generation всегда равно нулю
			HashInput[Ptr++] = 0;   // Для этой библиотеки значение Generation всегда равно нулю
			if (EncryptionType == EncryptionType.Aes128) Array.Copy(Salt, 0, HashInput, Ptr, Salt.Length);
			byte[] EncryptionKey = MD5.ComputeHash(HashInput);
			if (EncryptionKey.Length > 16) Array.Resize<byte>(ref EncryptionKey, 16);
			return EncryptionKey;
			}

		internal static int ProcessPermissions(Permission UserPermissions)
			{
			return ((int)UserPermissions & (int)Permission.All) | PermissionBase;
			}

		internal static byte[] ProcessPassword(string StringPassword)
			{
			// если нет user password
			if (string.IsNullOrEmpty(StringPassword)) 
				return (byte[])PasswordPad.Clone();

			// преобразовать пароль в массив байт
			byte[] BinaryPassword = new byte[32];
			int IndexEnd = Math.Min(StringPassword.Length, 32);
			for (int Index = 0; Index < IndexEnd; Index++)
				{
				char PWChar = StringPassword[Index];
				if (PWChar > 255) throw new ApplicationException("Owner or user Password has invalid character (allowed 0-255)");
				BinaryPassword[Index] = (byte)PWChar;
				}

			// если пароль пользователя короче 32 байт
			if (IndexEnd < 32) 
				Array.Copy(PasswordPad, 0, BinaryPassword, IndexEnd, 32 - IndexEnd);

			return BinaryPassword;
			}

		internal byte[] CreateOwnerKey(byte[] UserBinaryPassword, byte[] OwnerBinaryPassword)
			{
			// создать хэш-массив для owner password
			byte[] OwnerHash = MD5.ComputeHash(OwnerBinaryPassword);

			// повторяет цикл 50 раз, создавая хэш из хэша
			for (int Index = 0; Index < 50; Index++) 
				OwnerHash = MD5.ComputeHash(OwnerHash);

			byte[] ownerKey = (byte[])UserBinaryPassword.Clone();
			byte[] TempKey = new byte[16];
			for (int Index = 0; Index < 20; Index++)
				{
				for (int Tindex = 0; Tindex < 16; Tindex++) TempKey[Tindex] = (byte)(OwnerHash[Tindex] ^ Index);
				EncryptRC4(TempKey, ownerKey);
				}

			// зашифрованый ключ
			return ownerKey;
			}

		internal void CreateMasterKey(byte[] UserBinaryPassword, byte[] OwnerKey, bool EncryptMetadata = true)
			{
			// входной массив для хэш-функции MD5
			byte[] HashInput = new byte[UserBinaryPassword.Length + OwnerKey.Length + DocumentID.Length + (EncryptMetadata ? 4 : 8)];
			int Ptr = 0;
			Array.Copy(UserBinaryPassword, 0, HashInput, Ptr, UserBinaryPassword.Length);
			Ptr += UserBinaryPassword.Length;
			Array.Copy(OwnerKey, 0, HashInput, Ptr, OwnerKey.Length);
			Ptr += OwnerKey.Length;
			HashInput[Ptr++] = (byte)Permissions;
			HashInput[Ptr++] = (byte)(Permissions >> 8);
			HashInput[Ptr++] = (byte)(Permissions >> 16);
			HashInput[Ptr++] = (byte)(Permissions >> 24);
			Array.Copy(DocumentID, 0, HashInput, Ptr, DocumentID.Length);
			if (!EncryptMetadata)
				{
				HashInput[Ptr++] = (byte)255;
				HashInput[Ptr++] = (byte)255;
				HashInput[Ptr++] = (byte)255;
				HashInput[Ptr++] = (byte)255;
				}
			MasterKey = MD5.ComputeHash(HashInput);

			// повторяет цикл 50 раз, создавая хэш из хэша
			for (int Index = 0; Index < 50; Index++) 
				MasterKey = MD5.ComputeHash(MasterKey);

			return;
			}

		internal byte[] CreateUserKey()
			{
			// входной массив для хэш-функции MD5
			byte[] HashInput = new byte[PasswordPad.Length + DocumentID.Length];
			Array.Copy(PasswordPad, 0, HashInput, 0, PasswordPad.Length);
			Array.Copy(DocumentID, 0, HashInput, PasswordPad.Length, DocumentID.Length);
			byte[] UserKey = MD5.ComputeHash(HashInput);
			byte[] TempKey = new byte[16];

			for (int Index = 0; Index < 20; Index++)
				{
				for (int Tindex = 0; Tindex < 16; Tindex++) TempKey[Tindex] = (byte)(MasterKey[Tindex] ^ Index);
				EncryptRC4(TempKey, UserKey);
				}
			Array.Resize<byte>(ref UserKey, 32);
			return UserKey;
			}

		internal bool TestPassword(string Password)
			{
			// преобразовать пароль из нулевого значения или строки в массив байтов
			byte[] BinaryPassword = ProcessPassword(Password);

			// предположим, что пароль является паролем owner
			byte[] OwnerBinaryPassword = CreateOwnerKey(OwnerKey, BinaryPassword);

			CreateMasterKey(OwnerBinaryPassword, OwnerKey);

			// user key
			byte[] UserKey1 = CreateUserKey();

			// сравнить вычисленный ключ с пользовательским ключом словаря
			if (ArrayCompare(UserKey1, UserKey))
				{
				DecryptionStatus = DecryptionStatus.OwnerPassword;
				return true;
				}

			// предположим, что пароль является паролем user
			CreateMasterKey(BinaryPassword, OwnerKey);

			UserKey1 = CreateUserKey();

			// compare calculated key to dictionary user // сравнить вычисленный ключ с пользовательским ключом словаря
			if (ArrayCompare(UserKey1, UserKey))
				{
				DecryptionStatus = DecryptionStatus.UserPassword;
				return true;
				}

			// если пароль неверный
			DecryptionStatus = DecryptionStatus.InvalidPassword;
			return false;
			}

		private bool TestPasswordAES256(string password)
		{
			byte[] pwdBytes = Encoding.UTF8.GetBytes(password);

			// Попытка user-пароля
			byte[] uKey = ComputeHash256(pwdBytes, UValidationSalt, "/U");
			if (ArrayCompare(uKey, UserKey.Take(32).ToArray()))
			{
				MasterKey = DecryptAES256(pwdBytes, UKeySalt, "/U", UserKeyEncrypted);
				DecryptionStatus = DecryptionStatus.UserPassword;
				return true;
			}

			// Попытка owner-пароля
			byte[] oKey = ComputeHash256(pwdBytes, OValidationSalt, "/O");
			if (ArrayCompare(oKey, OwnerKey.Take(32).ToArray()))
			{
				MasterKey = DecryptAES256(pwdBytes, OKeySalt, "/O", OwnerKeyEncrypted);
				DecryptionStatus = DecryptionStatus.OwnerPassword;
				return true;
			}

			DecryptionStatus = DecryptionStatus.InvalidPassword;
			return false;
		}

		private byte[] ComputeHash256(byte[] password, byte[] salt, string type)
		{
			byte[] input = new byte[password.Length + salt.Length + type.Length];
			Array.Copy(password, 0, input, 0, password.Length);
			Array.Copy(salt, 0, input, password.Length, salt.Length);
			Array.Copy(Encoding.ASCII.GetBytes(type), 0, input, password.Length + salt.Length, type.Length);
			return SHA256.Create().ComputeHash(input);
		}

		private byte[] DecryptAES256(byte[] password, byte[] salt, string type, byte[] encryptedKey)
		{
			byte[] key = PBKDF2(password, salt, 10000, 32);
			using (var aes = Aes.Create())
			{
				aes.Key = key;
				aes.IV = new byte[16]; // IV == 0
				aes.Padding = PaddingMode.PKCS7;
				using (var decryptor = aes.CreateDecryptor())
				using (var ms = new MemoryStream(encryptedKey))
				using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
				{
					byte[] decrypted = new byte[encryptedKey.Length];
					int len = cs.Read(decrypted, 0, decrypted.Length);
					Array.Resize(ref decrypted, len);
					return decrypted;
				}
			}
		}

		private byte[] PBKDF2(byte[] password, byte[] salt, int iterations, int length)
		{
			using (var rfc = new Rfc2898DeriveBytes(password, salt, iterations)) // не тот стандарт SHA256
			{
				return rfc.GetBytes(length);
			}
		}

		// Сравнение двух байтовых массивов
		internal static bool ArrayCompare(byte[] Array1, byte[] Array2)
		{
			for (int Index = 0; Index < Array1.Length; Index++) if (Array1[Index] != Array2[Index]) return false;
			return true;
		}

		internal static void EncryptRC4(byte[] Key, byte[] Data)
			{
			byte[] State = new byte[256];
			for (int Index = 0; Index < 256; Index++) State[Index] = (byte)Index;

			int Index1 = 0;
			int Index2 = 0;
			for (int Index = 0; Index < 256; Index++)
				{
				Index2 = (Key[Index1] + State[Index] + Index2) & 255;
				byte tmp = State[Index];
				State[Index] = State[Index2];
				State[Index2] = tmp;
				Index1 = (Index1 + 1) % Key.Length;
				}

			int x = 0;
			int y = 0;
			for (int Index = 0; Index < Data.Length; Index++)
				{
				x = (x + 1) & 255;
				y = (State[x] + y) & 255;
				byte tmp = State[x];
				State[x] = State[y];
				State[y] = tmp;
				Data[Index] = (byte)(Data[Index] ^ State[(State[x] + State[y]) & 255]);
				}
			return;
			}

		public void Dispose()
			{
			if (AES != null)
				{
				AES.Clear();
				AES.Dispose();
				AES = null;
				}

			if (MD5 != null)
				{
				MD5.Clear();
				MD5 = null;
				}

			GC.SuppressFinalize(this);
			return;
			}
		}
	}
