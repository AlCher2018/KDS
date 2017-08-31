using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace KDSWPFClient.Lib
{
	public class Password
	{
		public Password()
		{
		}

		public static string Decrypt(string cryptedString, string key)
		{
			string result;
			byte[] bytes = Encoding.UTF8.GetBytes(key);
			DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
			try
			{
				MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(cryptedString));
				CryptoStream cryptoStream = new CryptoStream(memoryStream, provider.CreateDecryptor(bytes, bytes), CryptoStreamMode.Read);
				result = (new StreamReader(cryptoStream)).ReadToEnd();
			}
			catch (Exception)
			{
				result = "Ошибка ввода";
			}
			return result;
		}

		public static void DecryptFile(string sInputFilename, string sOutputFilename, string sKey)
		{
			DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider()
			{
				Key = Encoding.UTF8.GetBytes(sKey),
				IV = Encoding.UTF8.GetBytes(sKey)
			};
			DESCryptoServiceProvider des = dESCryptoServiceProvider;
			FileStream fsread = new FileStream(sInputFilename, FileMode.Open, FileAccess.Read);
			CryptoStream cryptostreamDecr = new CryptoStream(fsread, des.CreateDecryptor(), CryptoStreamMode.Read);
			StreamWriter fsDecrypted = new StreamWriter(sOutputFilename);
			fsDecrypted.Write((new StreamReader(cryptostreamDecr)).ReadToEnd());
			fsDecrypted.Flush();
			fsDecrypted.Close();
		}

		public static string DecryptFileToString(string sInputFilename, string sKey)
		{
			string str1;
			try
			{
				DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider()
				{
					Key = Encoding.UTF8.GetBytes(sKey),
					IV = Encoding.UTF8.GetBytes(sKey)
				};
				DESCryptoServiceProvider des = dESCryptoServiceProvider;
				FileStream fsread = new FileStream(sInputFilename, FileMode.Open, FileAccess.Read);
				CryptoStream cryptostreamDecr = new CryptoStream(fsread, des.CreateDecryptor(), CryptoStreamMode.Read);
				string str = (new StreamReader(cryptostreamDecr)).ReadToEnd();
				fsread.Close();
				cryptostreamDecr.Close();
				str1 = str;
			}
			catch (Exception)
			{
				return "ERROR";
			}
			return str1;
		}

		public static string Encrypt(string originalString, string key)
		{
			DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider()
			{
				Key = Encoding.UTF8.GetBytes(key),
				IV = Encoding.UTF8.GetBytes(key)
			};
			ICryptoTransform desencrypt = dESCryptoServiceProvider.CreateEncryptor();
			MemoryStream memoryStream = new MemoryStream();
			CryptoStream cryptoStream = new CryptoStream(memoryStream, desencrypt, CryptoStreamMode.Write);
			StreamWriter writer = new StreamWriter(cryptoStream);
			writer.Write(originalString);
			writer.Flush();
			cryptoStream.FlushFinalBlock();
			writer.Flush();
			return Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
		}

		public static void EncryptFile(string sInputFilename, string sOutputFilename, string sKey)
		{
			FileStream fsInput = new FileStream(sInputFilename, FileMode.Open, FileAccess.Read);
			FileStream fsEncrypted = new FileStream(sOutputFilename, FileMode.Create, FileAccess.Write);
			DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider()
			{
				Key = Encoding.UTF8.GetBytes(sKey),
				IV = Encoding.UTF8.GetBytes(sKey)
			};
			CryptoStream cryptostream = new CryptoStream(fsEncrypted, dESCryptoServiceProvider.CreateEncryptor(), CryptoStreamMode.Write);
			byte[] bytearrayinput = new byte[checked((int)fsInput.Length)];
			fsInput.Read(bytearrayinput, 0, (int)bytearrayinput.Length);
			cryptostream.Write(bytearrayinput, 0, (int)bytearrayinput.Length);
			cryptostream.Close();
			fsInput.Close();
			fsEncrypted.Close();
		}

		public static void EncryptStringToFile(string name, string sOutputFilename, string sKey)
		{
			MemoryStream nameStream = new MemoryStream(Encoding.UTF8.GetBytes(name ?? ""));
			FileStream fsEncrypted = new FileStream(sOutputFilename, FileMode.Create, FileAccess.Write);
			DESCryptoServiceProvider dESCryptoServiceProvider = new DESCryptoServiceProvider()
			{
				Key = Encoding.UTF8.GetBytes(sKey),
				IV = Encoding.UTF8.GetBytes(sKey)
			};
			CryptoStream cryptostream = new CryptoStream(fsEncrypted, dESCryptoServiceProvider.CreateEncryptor(), CryptoStreamMode.Write);
			byte[] bytearrayinput = new byte[checked((int)nameStream.Length)];
			nameStream.Read(bytearrayinput, 0, (int)bytearrayinput.Length);
			cryptostream.Write(bytearrayinput, 0, (int)bytearrayinput.Length);
			cryptostream.Close();
			nameStream.Close();
			fsEncrypted.Close();
		}

		public static string GenerateKey()
		{
			DESCryptoServiceProvider desCrypto = (DESCryptoServiceProvider)DES.Create();
			return Encoding.UTF8.GetString(desCrypto.Key);
		}

		public static bool KeyRead(int id)
		{
			return true;
		}

		[DllImport("KERNEL32.DLL", CharSet=CharSet.None, EntryPoint="RtlZeroMemory", ExactSpelling=false)]
		public static extern bool ZeroMemory(IntPtr Destination, int Length);
	}
}