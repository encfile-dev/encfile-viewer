# ENCFILE
**Secure encrypted container library, CLI, and desktop image viewer for .NET**

ENCFILE is a cross-platform .NET project built around a forward-compatible, chunk-based encrypted container format for `.enc` files.

This repository now contains three main parts:

- `EncFile.Lib`: the core encryption/decryption library
- `EncFile.Cli`: a command-line tool for working with `.enc` files
- `ImageFanReloaded`: a desktop image viewer with support for browsing encrypted image containers

`ImageFanReloaded` in this repository is forked from [mihnea-radulescu/imagefanreloaded](https://github.com/mihnea-radulescu/imagefanreloaded).

---

## Project Structure

```text
encfile-cs/
├── src/
│   ├── EncFile.Lib/           # .NET Standard 2.1 core library
│   ├── EncFile.Cli/           # .NET 10 command-line interface
│   └── ImageFanReloaded/      # Avalonia desktop image viewer
└── README.md
```

---

## What This Repository Provides

### EncFile.Lib

`EncFile.Lib` provides authenticated encryption for files, streams, and in-memory byte arrays using AES-256-GCM, modern KDFs, and optional embedded metadata.

Core capabilities:

- encrypt and decrypt files
- encrypt and decrypt streams
- encrypt and decrypt byte arrays
- embed JSON metadata inside `.enc` containers
- inspect embedded metadata without full decryption
- support Argon2id and PBKDF2-based key derivation

### EncFile.Cli

`EncFile.Cli` is the command-line interface for creating, inspecting, and decrypting `.enc` containers.

Core capabilities:

- encrypt files into `.enc`
- decrypt `.enc` files back to plaintext
- inspect container/header information
- pass metadata at encryption time
- use stdin-friendly password input
- manage passwords in the local keyring fallback

### ImageFanReloaded

`ImageFanReloaded` is a cross-platform Avalonia desktop image viewer integrated with `EncFile.Lib`.

In this repository, it supports:

- browsing normal image files and encrypted `.enc` image files
- filtering `.enc` files by embedded MIME metadata so image containers appear in the viewer
- password prompt on opening locked `.enc` images
- in-memory decryption for image viewing
- tab-based folder browsing
- fast concurrent thumbnail generation
- thumbnail caching
- full-screen and windowed viewing modes
- keyboard and mouse navigation
- zoom, pan, slideshow, and image info display
- image editing features inherited from the upstream project

---

## Prerequisites

- .NET SDK 8.0+
- Git
- a terminal with `dotnet` in `PATH`

The repository targets:

- `netstandard2.1` for `EncFile.Lib`
- `net10.0` for the CLI and desktop viewer

---

## Getting Started

### Build

```bash
git clone <repository-url>
cd encfile-cs
dotnet restore
dotnet build --configuration Release
```

### Run the CLI

```bash
dotnet run --project src/EncFile.Cli -- --help
```

### Run ImageFanReloaded

```bash
dotnet run --project src/ImageFanReloaded/ImageFanReloaded
```

You can also pass a folder or file path directly to the viewer, including supported `.enc` image files.

---

## CLI Usage

| Command | Description | Example |
|---------|-------------|---------|
| `encrypt` | Encrypt a file to `.enc` | `dotnet run -- encrypt input.txt output.enc --kdf-profile 1 --metadata "@meta.json"` |
| `decrypt` | Decrypt `.enc` to file | `dotnet run -- decrypt output.enc decrypted.txt` |
| `info` | Inspect container header | `dotnet run -- info output.enc` |
| `key save` | Store password in local keyring | `dotnet run -- key save comfyui_default --stdin` |
| `key get` | Retrieve password | `dotnet run -- key get comfyui_default` |
| `key list` | List saved slots | `dotnet run -- key list` |
| `key delete` | Remove a slot | `dotnet run -- key delete comfyui_default` |

Common options:

- `--kdf-profile`: `0` for PBKDF2-SHA256, `1` for Argon2id
- `--chunk-size`: `256KB`, `1MB`, `4MB`
- `--stdin`: read password from standard input
- `--metadata`: JSON string or `@file.json`

---

## Library Usage

Add the library to your project:

```bash
dotnet add package EncFile.Lib
```

Example:

```csharp
using System.Text;
using EncFile.Lib.Core;
using EncFile.Lib.Crypto;

var password = Encoding.UTF8.GetBytes("MySecretPassphrase");

EncFile.EncryptFile(
    "input.dat",
    "output.enc",
    password,
    KdfProfile.Argon2Id,
    1_048_576);

var meta = EncFile.DecryptFile("output.enc", "decrypted.dat", password);

using var input = File.OpenRead("large.bin");
using var output = File.Create("large.enc");
EncFile.EncryptStream(input, output, password, chunkSize: 4_194_304);

var peeked = EncFile.PeekMetadata("output.enc");
```

Key namespaces:

| Namespace | Purpose |
|-----------|---------|
| `EncFile.Lib.Core` | `EncFile`, header packing, constants |
| `EncFile.Lib.Crypto` | key derivation, cipher helpers, `KdfProfile` |
| `EncFile.Lib.Keyring` | local keyring integration |

---

## ENCFILE v1 Format

| Section | Size | Description |
|---------|------|-------------|
| Magic | 8B | `ENCFILE1` |
| Version | 1B | `0x01` |
| Flags | 1B | Bit 0 = metadata present |
| Payload Offset | 4B | Big-endian absolute offset to first chunk |
| Mode | 1B | `0x01` = chunked streaming |
| Reserved | 5B | must be `0x00` |
| Salt | 16B | random KDF salt |
| Base Nonce | 12B | AES-GCM nonce prefix |
| KDF Profile | 1B | `0x00` PBKDF2, `0x01` Argon2id |
| Metadata | 4B + JSON | optional length-prefixed UTF-8 JSON |
| Chunks | `4B + N + 16B` | `[Length][Ciphertext][Tag]...` |

---

## Security Notes

- AES-256-GCM is used per chunk with unique nonces
- Argon2id is the default KDF
- PBKDF2-SHA256 is available as a fallback profile
- metadata is bounded and validated
- authentication failures stop decryption immediately
- encrypted images in `ImageFanReloaded` are decrypted in memory for viewing

---

## Interoperability

- The `.enc` container format is compatible across implementations that follow the same spec
- ComfyUI/Python workflows may use different credential storage than this .NET project
- When keyring storage is not shared, provide the password manually or via stdin

---

## Upstream Attribution

The `ImageFanReloaded` desktop viewer included here is based on the upstream project:

- Upstream repository: [mihnea-radulescu/imagefanreloaded](https://github.com/mihnea-radulescu/imagefanreloaded)

This fork extends the viewer to work with ENCFILE `.enc` image containers alongside standard image formats.
