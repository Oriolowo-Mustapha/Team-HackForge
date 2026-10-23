namespace FarmHand.Application.Common.Interfaces;

/// <summary>Encrypts sensitive values (e.g. payout account numbers) before they are stored.</summary>
public interface IFieldEncryptor
{
    string Encrypt(string plaintext);
    string Decrypt(string ciphertext);
}
