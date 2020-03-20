using System;
using System.Security.Cryptography;

namespace NCoreUtils.IO
{
    public abstract class EncryptionBase
    {
        static readonly byte[] _key = Convert.FromBase64String("QPLGrl3+OJclcFthN52FnW8DXXuDzjY99TDtn7N0jGM=");

        static readonly byte[] _iv = Convert.FromBase64String("JHi+3ilPs7lWx9g3FG1BWQ==");

        protected static Rijndael Create()
        {
            var instance = RijndaelManaged.Create();
            instance.Key = _key;
            instance.IV = _iv;
            return instance;
        }
    }
}