using System;
using System.IO;
using ImageFanReloaded.Core.DiscAccess;
using ImageFanReloaded.Core.ImageHandling;
using ImageFanReloaded.Core.Security;
using ImageFanReloaded.Core.Settings;

namespace ImageFanReloaded.ImageHandling;

public class EncryptedImageFile : ImageFile
{
	private readonly ISessionManager _sessionManager;

	public EncryptedImageFile(
		IGlobalParameters globalParameters,
		IImageResizer imageResizer,
		IFileSizeEngine fileSizeEngine,
		IImageFileContentLogic imageFileContentLogic,
		ImageFileData imageFileData,
		ISessionManager sessionManager)
		: base(
			globalParameters,
			imageResizer,
			fileSizeEngine,
			imageFileContentLogic,
			imageFileData)
	{
		_sessionManager = sessionManager;
	}

	protected override IImage GetImageFromStream(
		Stream imageFileContentStream, bool applyImageOrientation)
	{
		if (_sessionManager.Password == null)
		{
			throw new UnauthorizedAccessException(
				"Session is locked. Please enter password.");
		}

		using var decryptedStream = new MemoryStream();
		try
		{
			EncFile.Lib.Core.EncFile.DecryptStream(
				imageFileContentStream, decryptedStream, _sessionManager.Password);
			decryptedStream.Position = 0;

			return BuildIndirectlySupportedImageFromStream(decryptedStream);
		}
		catch (Exception)
		{
			throw;
		}
	}
}
