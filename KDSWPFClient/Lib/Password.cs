using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace KDSWPFClient.Lib
{
	public static class Password
	{
        private static byte[] bt = new byte[] { 0x30, 0x32, 0x30, 0x36, 0x31, 0x39, 0x36, 0x37 };


		public static string Decrypt(string cryptedString)
		{
			string result;
			DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
			try
			{
				MemoryStream memoryStream = new MemoryStream(Convert.FromBase64String(cryptedString));
				CryptoStream cryptoStream = new CryptoStream(memoryStream, provider.CreateDecryptor(bt, bt), CryptoStreamMode.Read);
				result = (new StreamReader(cryptoStream)).ReadToEnd();
			}
			catch (Exception ex)
			{
				result = "Ошибка ввода";
			}
			return result;
		}

		public static void DecryptFile(string sInputFilename, string sOutputFilename)
		{
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
            try
            {
                FileStream fsread = new FileStream(sInputFilename, FileMode.Open, FileAccess.Read);
                CryptoStream cryptostreamDecr = new CryptoStream(fsread, provider.CreateDecryptor(bt, bt), CryptoStreamMode.Read);
                StreamWriter fsDecrypted = new StreamWriter(sOutputFilename);
                fsDecrypted.Write((new StreamReader(cryptostreamDecr)).ReadToEnd());
                fsDecrypted.Flush();
                fsDecrypted.Close();
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
		}

		public static string DecryptFileToString(string sInputFilename)
		{
			string result = null;
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
            try
            {
				FileStream fsread = new FileStream(sInputFilename, FileMode.Open, FileAccess.Read);
				CryptoStream cryptostreamDecr = new CryptoStream(fsread, provider.CreateDecryptor(bt, bt), CryptoStreamMode.Read);
				result = (new StreamReader(cryptostreamDecr)).ReadToEnd();
				fsread.Close();
				cryptostreamDecr.Close();
			}
			catch (Exception ex)
			{
				result = "ERROR: " + ex.Message;
			}
			return result;
		}

		public static string Encrypt(string originalString, string key)
		{
            string result = null;
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
            try
            {
                MemoryStream memoryStream = new MemoryStream();
                CryptoStream cryptoStream = new CryptoStream(memoryStream, provider.CreateEncryptor(bt, bt), CryptoStreamMode.Write);
                StreamWriter writer = new StreamWriter(cryptoStream);
                writer.Write(originalString);
                writer.Flush();
                cryptoStream.FlushFinalBlock();
                writer.Flush();
                result = Convert.ToBase64String(memoryStream.GetBuffer(), 0, (int)memoryStream.Length);
            }
            catch (Exception ex)
            {
                result = "ERROR: " + ex.Message;
            }
            return result;

        }

		public static void EncryptFile(string sInputFilename, string sOutputFilename)
		{
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider();
            try
            {
                FileStream fsInput = new FileStream(sInputFilename, FileMode.Open, FileAccess.Read);
                FileStream fsEncrypted = new FileStream(sOutputFilename, FileMode.Create, FileAccess.Write);
                CryptoStream cryptostream = new CryptoStream(fsEncrypted, provider.CreateEncryptor(bt, bt), CryptoStreamMode.Write);

                byte[] bytearrayinput = new byte[checked((int)fsInput.Length)];
                fsInput.Read(bytearrayinput, 0, (int)bytearrayinput.Length);
                cryptostream.Write(bytearrayinput, 0, (int)bytearrayinput.Length);
                cryptostream.Close();
                fsInput.Close();
                fsEncrypted.Close();
            }
            catch (Exception)
            {
                throw;
            }
		}

		public static void EncryptStringToFile(string name, string sOutputFilename)
		{
            DESCryptoServiceProvider provider = new DESCryptoServiceProvider();

            try
            {
                MemoryStream nameStream = new MemoryStream(Encoding.UTF8.GetBytes(name ?? ""));
                FileStream fsEncrypted = new FileStream(sOutputFilename, FileMode.Create, FileAccess.Write);
                CryptoStream cryptostream = new CryptoStream(fsEncrypted, provider.CreateEncryptor(bt, bt), CryptoStreamMode.Write);

                byte[] bytearrayinput = new byte[checked((int)nameStream.Length)];
                nameStream.Read(bytearrayinput, 0, (int)bytearrayinput.Length);
                cryptostream.Write(bytearrayinput, 0, (int)bytearrayinput.Length);
                cryptostream.Close();
                nameStream.Close();
                fsEncrypted.Close();
            }
            catch (Exception)
            {
                throw;
            }
		}

		public static string GenerateKey()
		{
			DESCryptoServiceProvider desCrypto = (DESCryptoServiceProvider)DES.Create();
			return Encoding.UTF8.GetString(desCrypto.Key);
		}


		[DllImport("KERNEL32.DLL", CharSet=CharSet.None, EntryPoint="RtlZeroMemory", ExactSpelling=false)]
		public static extern bool ZeroMemory(IntPtr Destination, int Length);
	}
}