using System;
using AzureDevOpsTestConnector.Services.Interfaces;

namespace AzureDevOpsTestConnector.Services
{
    public class Base64Encoder : IBase64Encoder
    {
        public string EncodeString(string input)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(plainTextBytes);
        }
    }
}
