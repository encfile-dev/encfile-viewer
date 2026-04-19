using System.Security.Cryptography;

namespace ImageFanReloaded.Core.Security;

public interface ISessionManager
{
	byte[]? Password { get; }
	void SetPassword(byte[] password);
	void ClearPassword();
	bool IsLocked { get; }
}

public class SessionManager : ISessionManager
{
	private byte[]? _password;

	public byte[]? Password => _password;

	public void SetPassword(byte[] password)
	{
		ClearPassword();
		_password = (byte[])password.Clone();
	}

	public void ClearPassword()
	{
		if (_password != null)
		{
			CryptographicOperations.ZeroMemory(_password);
			_password = null;
		}
	}

	public bool IsLocked => _password == null;
}
