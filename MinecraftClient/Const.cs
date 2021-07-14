using System;

namespace MinecraftClient {
    public static class Const {
        public static readonly string TranslationsFile_FromMCDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + @"\.minecraft\assets\objects\eb\ebf762c137bd91ab2496397f2504e250f3c5d1ba"; //MC 1.16 en_GB.lang
        public const string TranslationsFile_Website_Index = "https://launchermeta.mojang.com/v1/packages/bdb68de96a44ec1e9ed6d9cfcd2ee973be618c3a/1.16.json";
        public const string TranslationsFile_Website_Download = "http://resources.download.minecraft.net";
        public const string MCLowestVersion = "1.4.6";
        public const string MCHighestVersion = "1.17";
    }
}