using System.Collections.Generic;
using System.Text;
using Rune = System.Rune;

namespace MinecraftClient.ConsoleGui {
    public static class Helpers {
        public static string ToRuneString(this List<Rune> runes) {
            StringBuilder b = new StringBuilder();
            foreach (var rune in runes)
                b.Append((char)rune);
            return b.ToString();
        }
        public static bool IsValidFormattingChar(this char character) {
            switch (character) {
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case 'a':
                case 'b':
                case 'c':
                case 'd':
                case 'e':
                case 'f':
                case 'k':
                case 'l':
                case 'm':
                case 'n':
                case 'o':
                case 'r':
                    return true;
                default:
                    return false;
            }
        }
        public static List<Rune> TrimFormatting(this List<Rune> runes) {
            if (runes.Count == 0)
                return runes;
            
            List<Rune> backingStore = new(runes); //fixme alloc issue
            for (int i = 0; i < backingStore.Count; i++) {
                switch (backingStore[i]) {
                    case '§':
                        if (i+1 >= backingStore.Count)
                            break;
                        
                        bool brk = false;
                        switch (backingStore[i+1]) {
                            case '0':
                            case '1':
                            case '2':
                            case '3':
                            case '4':
                            case '5':
                            case '6':
                            case '7':
                            case '8':
                            case '9':
                            case 'a':
                            case 'b':
                            case 'c':
                            case 'd':
                            case 'e':
                            case 'f':
                            case 'k':
                            case 'l':
                            case 'm':
                            case 'n':
                            case 'o':
                            case 'r':
                                backingStore.RemoveAt(i);
                                backingStore.RemoveAt(i);
                                break;
                            default:
                                brk = true;
                                break;
                        }

                        if (brk)
                            break;
                        else
                            i--;
                        break;
                    case '\n':
                        backingStore.RemoveAt(i);
                        break;
                }
            }
            return backingStore;
        }
        public static string TrimFormatting(this string str) {
            StringBuilder b = new StringBuilder(str);
            b.Replace("§0", "") // Black
             .Replace("§1", "") // Dark Blue
             .Replace("§2", "") // Dark Green
             .Replace("§3", "") // Dark Aqua
             .Replace("§4", "") // Dark Red
             .Replace("§5", "") // Dark Purple
             .Replace("§6", "") // Gold
             .Replace("§7", "") // Gray
             .Replace("§8", "") // Dark Gray
             .Replace("§9", "") // Blue
             .Replace("§a", "") // Green
             .Replace("§b", "") // Aqua
             .Replace("§c", "") // Red
             .Replace("§d", "") // Light Purple
             .Replace("§e", "") // Yellow
             .Replace("§f", "") // White
             .Replace("§k", "") // Obfuscated
             .Replace("§l", "") // Bold
             .Replace("§m", "") // Strikethrough
             .Replace("§n", "") // Underline
             .Replace("§o", "") // Italic
             .Replace("§r", "");// Reset
            return b.ToString();
        }
    }
}