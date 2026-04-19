using System;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Text.Json;
using EncFile.Lib.Core;
using EncFile.Lib.Crypto;
using EncFile.Lib.Keyring;

namespace EncFile.Cli;

public class Program
{
    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand("ENCFILE v1 streaming encrypted container CLI");

        // ─── KEY COMMANDS ─────────────────────────────────────

        var keyCommand = new Command("key", "Manage passwords");
        rootCommand.Add(keyCommand);

        // save
        var slotArg = new Argument<string>("slot");
        var stdinOpt = new Option<bool>("--stdin");

        var keySaveCmd = new Command("save", "Save password")
        {
            slotArg,
            stdinOpt
        };

        keySaveCmd.SetHandler(
            (string slot, bool stdin) => HandleKeySave(slot, stdin),
            slotArg,
            stdinOpt
        );

        keyCommand.Add(keySaveCmd);

        // get
        var slotArg2 = new Argument<string>("slot");

        var keyGetCmd = new Command("get", "Get password")
        {
            slotArg2
        };

        keyGetCmd.SetHandler(
            (string slot) => HandleKeyGet(slot),
            slotArg2
        );

        keyCommand.Add(keyGetCmd);

        // delete
        var slotArg3 = new Argument<string>("slot");

        var keyDelCmd = new Command("delete", "Delete password")
        {
            slotArg3
        };

        keyDelCmd.SetHandler(
            (string slot) => HandleKeyDelete(slot),
            slotArg3
        );

        keyCommand.Add(keyDelCmd);

        // list
        var keyListCmd = new Command("list", "List slots");
        keyListCmd.SetHandler(() => HandleKeyList());
        keyCommand.Add(keyListCmd);

        // ─── ENCRYPT ─────────────────────────────────────────

        var inputArg = new Argument<string>("input-file");
        var outputArg = new Argument<string>("output-file");

        var kdfProfileOpt = new Option<int>("--kdf-profile");
        kdfProfileOpt.SetDefaultValue(1);

        var chunkSizeOpt = new Option<string>("--chunk-size");
        chunkSizeOpt.SetDefaultValue("1MB");

        var stdinOpt2 = new Option<bool>("--stdin");
        var passFdOpt = new Option<int?>("--pass-fd");
        var metadataOpt = new Option<string?>("--metadata");

        var encryptCmd = new Command("encrypt", "Encrypt file")
        {
            inputArg,
            outputArg,
            kdfProfileOpt,
            chunkSizeOpt,
            stdinOpt2,
            passFdOpt,
            metadataOpt
        };

        encryptCmd.SetHandler(
            (string inputFile, string outputFile, int kdfProfile, string chunkSize, bool stdin, int? passFd, string metadata) =>
                HandleEncrypt(inputFile, outputFile, kdfProfile, chunkSize, stdin, passFd, metadata),
            inputArg,
            outputArg,
            kdfProfileOpt,
            chunkSizeOpt,
            stdinOpt2,
            passFdOpt,
            metadataOpt
        );

        rootCommand.Add(encryptCmd);

        // ─── DECRYPT ─────────────────────────────────────────

        var inputArg2 = new Argument<string>("input-file");
        var outputArg2 = new Argument<string>("output-file");

        var stdinOpt3 = new Option<bool>("--stdin");
        var passFdOpt2 = new Option<int?>("--pass-fd");

        var decryptCmd = new Command("decrypt", "Decrypt file")
        {
            inputArg2,
            outputArg2,
            stdinOpt3,
            passFdOpt2
        };

        decryptCmd.SetHandler(
            (string inputFile, string outputFile, bool stdin, int? passFd) =>
                HandleDecrypt(inputFile, outputFile, stdin, passFd),
            inputArg2,
            outputArg2,
            stdinOpt3,
            passFdOpt2
        );

        rootCommand.Add(decryptCmd);

        // ─── INFO ────────────────────────────────────────────

        var fileArg = new Argument<string>("file");

        var infoCmd = new Command("info", "Show file info")
        {
            fileArg
        };

        infoCmd.SetHandler(
            (string file) => HandleInfo(file),
            fileArg
        );

        rootCommand.Add(infoCmd);

        return rootCommand.Invoke(args);
    }

    // ─── HANDLERS ───────────────────────────────────────────

    static void HandleKeySave(string slot, bool stdin)
    {
        try
        {
            string pwd = stdin
                ? new StreamReader(Console.OpenStandardInput()).ReadToEnd().Trim()
                : ReadPasswordInteractively();

            KeyringManager.SavePassword(slot, pwd);
            Console.WriteLine($"✅ Saved to slot: {slot}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ {ex.Message}");
        }
    }

    static void HandleKeyGet(string slot)
    {
        var pwd = KeyringManager.GetPassword(slot);
        Console.WriteLine(pwd ?? $"❌ Slot not found: {slot}");
    }

    static void HandleKeyDelete(string slot)
    {
        Console.WriteLine(KeyringManager.DeletePassword(slot)
            ? $"🗑️ Deleted: {slot}"
            : $"❌ Slot not found");
    }

    static void HandleKeyList()
    {
        var slots = KeyringManager.ListSlots();
        foreach (var s in slots) Console.WriteLine(s);
    }

    static void HandleEncrypt(string inputFile, string outputFile, int kdfProfile, string chunkSize, bool stdin, int? passFd, string metadata)
    {
        try
        {
            var pwd = ReadPassword(passFd, stdin);
            int size = ParseChunkSize(chunkSize);
            var meta = ParseMetadata(metadata);

            EncFile.Lib.Core.EncFile.EncryptFile(inputFile, outputFile, pwd, (KdfProfile)kdfProfile, size, meta);
            Console.WriteLine("✅ Encrypted");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ {ex.Message}");
        }
    }

    static void HandleDecrypt(string inputFile, string outputFile, bool stdin, int? passFd)
    {
        try
        {
            var pwd = ReadPassword(passFd, stdin);
            var meta = EncFile.Lib.Core.EncFile.DecryptFile(inputFile, outputFile, pwd);

            if (meta != null && meta.Count > 0)
                Console.WriteLine(JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));

            Console.WriteLine("✅ Decrypted");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ {ex.Message}");
        }
    }

    static void HandleInfo(string file)
    {
        try
        {
            using var f = File.OpenRead(file);

            var headerBuf = new byte[20];
            f.Read(headerBuf, 0, 20);
            var header = HeaderPacking.UnpackHeader(headerBuf);

            Console.WriteLine($"Version: {header.Version}");
            Console.WriteLine($"Mode: {header.Mode}");
            Console.WriteLine($"PayloadOffset: {header.PayloadOffset}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"❌ {ex.Message}");
        }
    }

    // ─── HELPERS ───────────────────────────────────────────

    static byte[] ReadPassword(int? passFd, bool stdin)
    {
        if (stdin)
        {
            using var reader = new StreamReader(Console.OpenStandardInput());
            return Encoding.UTF8.GetBytes(reader.ReadToEnd().Trim());
        }

        Console.Write("Password: ");
        return Encoding.UTF8.GetBytes(ReadPasswordInteractively());
    }

    static string ReadPasswordInteractively()
    {
        var sb = new StringBuilder();
        ConsoleKeyInfo key;

        while ((key = Console.ReadKey(true)).Key != ConsoleKey.Enter)
        {
            sb.Append(key.KeyChar);
            Console.Write("*");
        }

        Console.WriteLine();
        return sb.ToString();
    }

    static int ParseChunkSize(string size)
    {
        return size.ToUpper() switch
        {
            "256KB" => 262144,
            "1MB" => 1048576,
            "4MB" => 4194304,
            _ => 1048576
        };
    }

    static Dictionary<string, object> ParseMetadata(string meta)
    {
        if (string.IsNullOrEmpty(meta)) return new Dictionary<string, object>();

        try
        {
            var json = meta.StartsWith("@") ? File.ReadAllText(meta.Substring(1)) : meta;
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json)
                   ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Invalid metadata: {ex.Message}");
        }
    }
}