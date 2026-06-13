using System;
using System.IO;

namespace Greyrose.Branding
{
    static class BrandingCommand
    {
        public static int Run(string[] args)
        {
            if (!OperatingSystem.IsWindows())
            {
                Console.Error.WriteLine("--apply-branding requires Windows (System.Drawing + PE icon resources).");
                return 1;
            }

            string root = ResolveClientRoot(args);
            if (root == null)
            {
                Console.Error.WriteLine("Could not find Wizard101 client install. Pass --client-root <path>.");
                return 1;
            }

            string png = ResolveAssetPath("greyrose303.png");
            string ico = Path.Combine(Path.GetDirectoryName(png) ?? ".", "greyrose303.ico");

            Console.WriteLine("Creating {0}", ico);
            IconFactory.CreateIcoFromPng(png, ico);

            string bankA = Path.Combine(root, "PatchClient", "BankA");
            string bankB = Path.Combine(root, "PatchClient", "BankB");
            string bin = Path.Combine(root, "Bin");

            foreach (string bank in new[] { bankA, bankB })
            {
                if (!Directory.Exists(bank))
                    continue;

                string skf = Path.Combine(bank, "wizard101.skf");
                if (File.Exists(skf))
                {
                    string backup = skf + ".orig";
                    if (!File.Exists(backup))
                        File.Copy(skf, backup, false);
                    Console.WriteLine("Patching skin {0}", skf);
                    try
                    {
                        SkfBrandingPatcher.Patch(skf, png);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("WARN: Skin patch failed for {0}: {1}", skf, ex.Message);
                    }
                }

                TryApplyExeIcon(Path.Combine(bank, "WizardLauncher.exe"), ico);
            }

            TryApplyExeIcon(Path.Combine(bin, "WizardGraphicalClient.exe"), ico);

            Console.WriteLine("Branding applied under {0}", root);
            return 0;
        }

        static void TryApplyExeIcon(string exePath, string icoPath)
        {
            if (!File.Exists(exePath))
                return;
            try
            {
                Console.WriteLine("Setting icon {0}", exePath);
                ExeIconResource.ApplyIcon(exePath, icoPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine("WARN: Could not update {0}: {1}", exePath, ex.Message);
            }
        }

        static string ResolveClientRoot(string[] args)
        {
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--client-root" && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return Path.GetFullPath(args[i + 1]);
            }

            string[] candidates =
            {
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Wizard101 April of 2019")),
                @"d:\Wizard101_client_04_2019\Wizard101 April of 2019"
            };

            foreach (string c in candidates)
            {
                if (Directory.Exists(Path.Combine(c, "PatchClient", "BankB")))
                    return c;
            }

            return null;
        }

        static string ResolveAssetPath(string fileName)
        {
            string[] bases =
            {
                Path.Combine(AppContext.BaseDirectory, "Assets", fileName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Assets", fileName),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Assets", fileName)
            };

            foreach (string p in bases)
            {
                string full = Path.GetFullPath(p);
                if (File.Exists(full))
                    return full;
            }

            throw new FileNotFoundException("Branding asset not found: " + fileName);
        }

        public static string GetIcoPath()
        {
            string png = ResolveAssetPath("greyrose303.png");
            string ico = Path.Combine(Path.GetDirectoryName(png) ?? ".", "greyrose303.ico");
            if (!File.Exists(ico))
                IconFactory.CreateIcoFromPng(png, ico);
            return ico;
        }
    }
}
