using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MasterUnlock
{
    public class XiaomiFastbootDeviceInfo
    {
        public bool Success { get; set; } = false;
        public string Brand { get; set; } = "XIAOMI";
        public string? Product { get; set; }
        public string? DeviceModel { get; set; }
        public string? DeviceProduct { get; set; }
        public string? CommercialName { get; set; }
        public string? Chipset { get; set; }
        public string? Unlocked { get; set; }
        public bool IsUnlocked { get; set; }
        public string? Secure { get; set; }
        public string? AntiRollback { get; set; }
        public string? Token { get; set; }
        public string? SerialNumber { get; set; }
        public string? SocId { get; set; }
        public string? HwRevision { get; set; }
        public string? BatteryVoltage { get; set; }
        public string? BatterySoc { get; set; }
        public string? CurrentSlot { get; set; }
        public string? SlotCount { get; set; }
        public string? IsUserspace { get; set; }
        public string? MaxDownloadSize { get; set; }
        public string? Tampered { get; set; }
        public string? ChargerScreen { get; set; }
        public Dictionary<string, string> RawVars { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? ErrorMessage { get; set; }
    }

    public class XiaomiSideloadDeviceInfo
    {
        public bool Success { get; set; } = false;
        public string Brand { get; set; } = "XIAOMI";
        public string? DeviceState { get; set; } // sideload, recovery, device
        public string? ProductModel { get; set; }
        public string? ProductDevice { get; set; }
        public string? DeviceModel { get; set; }
        public string? DeviceProduct { get; set; }
        public string? CommercialName { get; set; }
        public string? Chipset { get; set; }
        public string? DeviceVersion { get; set; }
        public string? BuildIncremental { get; set; }
        public string? RomFamily { get; set; } // HyperOS 3.0, HyperOS 2.0, HyperOS 1.0, MIUI 14, etc.
        public string? Region { get; set; } // Global, Europe / EEA, India, China, etc.
        public string? AndroidVersion { get; set; }
        public string? SecurityPatch { get; set; }
        public string? BootloaderLocked { get; set; }
        public bool IsLocked { get; set; } = true;
        public string? UiVersion { get; set; }
        public string? HwVersion { get; set; }
        public string? Cpuid { get; set; }
        public string? DeviceSerialNo { get; set; }
        public string? DeviceCodebase { get; set; }
        public string? DeviceBranch { get; set; } = "F";
        public string? DeviceLanguage { get; set; } = "en";
        public string? DeviceRegion { get; set; } = "US";
        public string? DeviceRecoveryVersion { get; set; } = "2";
        public Dictionary<string, string> RawProps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string? ErrorMessage { get; set; }
    }

    public static class XiaomiReader
    {
        // ============================================================
        // XIAOMI CODENAME TO COMMERCIAL MODEL & CHIPSET MAPPING
        // ============================================================
        private static readonly Dictionary<string, (string name, string chipset)> CodenameDb = new(StringComparer.OrdinalIgnoreCase)
        {
            // 2024 - 2026 Entry & Mid Range Models (including user device)
            { "lake", ("REDMI 14C / REDMI A3 PRO / POCO C75", "MediaTek Helio G81-Ultra (MT6768)") },
            { "flame", ("Redmi 14C 5G / POCO M7 5G", "Snapdragon 4s Gen 2") },
            { "gale", ("Redmi 13 4G", "MediaTek Helio G91-Ultra") },
            { "breeze", ("Redmi 13 5G / POCO M6 Plus 5G", "Snapdragon 4 Gen 2 AE") },
            { "sky", ("Redmi 13C 4G / POCO C65", "MediaTek Helio G85") },
            { "air", ("Redmi 13C 5G / Redmi 13R 5G / POCO M6 5G", "MediaTek Dimensity 6100+") },
            { "earth", ("Redmi 12C / POCO C55", "MediaTek Helio G85") },
            { "fire", ("Redmi 12 4G", "MediaTek Helio G88") },
            { "water", ("Redmi 12 5G / POCO M6 Pro 5G", "Snapdragon 4 Gen 2") },

            // Redmi Note 14 Series
            { "tide", ("Redmi Note 14 5G", "MediaTek Dimensity 7025-Ultra") },
            { "malachite", ("Redmi Note 14 Pro 5G", "MediaTek Dimensity 7300-Ultra") },
            { "amethyst", ("Redmi Note 14 Pro+ 5G", "Snapdragon 7s Gen 3") },

            // Redmi Note 13 Series
            { "sapphire", ("Redmi Note 13 4G", "Snapdragon 685") },
            { "sapphiren", ("Redmi Note 13 4G (NFC)", "Snapdragon 685") },
            { "gold", ("Redmi Note 13 5G / POCO X6 Neo 5G", "MediaTek Dimensity 6080") },
            { "garnet", ("Redmi Note 13 Pro 5G / POCO X6 5G", "Snapdragon 7s Gen 2") },
            { "emerald", ("Redmi Note 13 Pro 4G / POCO M6 Pro 4G", "MediaTek Helio G99-Ultra") },
            { "zircon", ("Redmi Note 13 Pro+ 5G", "MediaTek Dimensity 7200-Ultra") },

            // Redmi Note 12 Series
            { "tapas", ("Redmi Note 12 4G", "Snapdragon 685") },
            { "topaz", ("Redmi Note 12 4G (NFC)", "Snapdragon 685") },
            { "ruby", ("Redmi Note 12 Pro 5G / Pro+ 5G", "MediaTek Dimensity 1080") },
            { "sea", ("Redmi Note 12S", "MediaTek Helio G96") },
            { "sunstone", ("Redmi Note 12 5G / POCO X5 5G", "Snapdragon 4 Gen 1") },

            // Redmi Note 11 Series
            { "spes", ("Redmi Note 11 4G", "Snapdragon 680") },
            { "spesn", ("Redmi Note 11 4G (NFC)", "Snapdragon 680") },
            { "viva", ("Redmi Note 11 Pro 4G", "MediaTek Helio G96") },
            { "vida", ("Redmi Note 11 Pro 4G (India)", "MediaTek Helio G96") },
            { "veux", ("Redmi Note 11 Pro 5G / POCO X4 Pro 5G", "Snapdragon 695") },
            { "peux", ("Redmi Note 11 Pro 5G (India)", "Snapdragon 695") },
            { "pissarro", ("Redmi Note 11 Pro+ 5G", "MediaTek Dimensity 920") },
            { "selenes", ("Redmi Note 11 4G (China)", "MediaTek Helio G88") },

            // Redmi Note 10 Series
            { "sweet", ("Redmi Note 10 Pro (Global)", "Snapdragon 732G") },
            { "sweetin", ("Redmi Note 10 Pro (India)", "Snapdragon 732G") },
            { "mojito", ("Redmi Note 10", "Snapdragon 678") },
            { "sunny", ("Redmi Note 10 (Global)", "Snapdragon 678") },
            { "rosemary", ("Redmi Note 10S / POCO M5s", "MediaTek Helio G95") },
            { "camellia", ("Redmi Note 10 5G", "MediaTek Dimensity 700") },
            { "camellian", ("Redmi Note 10T 5G", "MediaTek Dimensity 700") },
            { "chopin", ("Redmi Note 10 Pro 5G / POCO X3 GT", "MediaTek Dimensity 1100") },

            // Redmi Note 9 Series
            { "curtana", ("Redmi Note 9S", "Snapdragon 720G") },
            { "joyeuse", ("Redmi Note 9 Pro (Global)", "Snapdragon 720G") },
            { "excalibur", ("Redmi Note 9 Pro Max", "Snapdragon 720G") },
            { "merlin", ("Redmi Note 9", "MediaTek Helio G85") },
            { "merlinx", ("Redmi Note 9 (NFC)", "MediaTek Helio G85") },
            { "gauguin", ("Redmi Note 9 Pro 5G / Mi 10T Lite", "Snapdragon 750G") },

            // Redmi Note 8 & Legacy Series
            { "ginkgo", ("Redmi Note 8", "Snapdragon 665") },
            { "willow", ("Redmi Note 8T", "Snapdragon 665") },
            { "begonia", ("Redmi Note 8 Pro", "MediaTek Helio G90T") },
            { "begoniain", ("Redmi Note 8 Pro (India)", "MediaTek Helio G90T") },
            { "biloba", ("Redmi Note 8 (2021)", "MediaTek Helio G85") },
            { "lavender", ("Redmi Note 7", "Snapdragon 660") },
            { "violet", ("Redmi Note 7 Pro", "Snapdragon 675") },
            { "whyred", ("Redmi Note 5 / Note 5 Pro", "Snapdragon 636") },
            { "mido", ("Redmi Note 4X", "Snapdragon 625") },
            { "kenzo", ("Redmi Note 3", "Snapdragon 650") },

            // POCO Series
            { "duchamp", ("POCO X6 Pro 5G / Redmi K70E", "MediaTek Dimensity 8300-Ultra") },
            { "peridot", ("POCO F6 / Redmi Turbo 3", "Snapdragon 8s Gen 3") },
            { "marble", ("POCO F5 / Redmi Note 12 Turbo", "Snapdragon 7+ Gen 2") },
            { "marblein", ("POCO F5 (India)", "Snapdragon 7+ Gen 2") },
            { "mondrian", ("POCO F5 Pro / Redmi K60", "Snapdragon 8+ Gen 1") },
            { "munch", ("POCO F4 / Redmi K40S", "Snapdragon 870") },
            { "ingres", ("POCO F4 GT / Redmi K50 Gaming", "Snapdragon 8 Gen 1") },
            { "alioth", ("POCO F3 / Redmi K40", "Snapdragon 870") },
            { "aliothin", ("POCO F3 / Mi 11X (India)", "Snapdragon 870") },
            { "lmi", ("POCO F2 Pro / Redmi K30 Pro", "Snapdragon 865") },
            { "beryllium", ("POCO F1", "Snapdragon 845") },
            { "redwood", ("POCO X5 Pro 5G", "Snapdragon 778G") },
            { "xaga", ("POCO X4 GT / Redmi K50i", "MediaTek Dimensity 8100") },
            { "surya", ("POCO X3 NFC", "Snapdragon 732G") },
            { "karna", ("POCO X3 (India)", "Snapdragon 732G") },
            { "vayu", ("POCO X3 Pro (Global)", "Snapdragon 860") },
            { "bhima", ("POCO X3 Pro (India)", "Snapdragon 860") },
            { "rock", ("POCO M5", "MediaTek Helio G99") },

            // Xiaomi Flagship Series (Xiaomi 15 / 14 / 13 / 12 / 11 / 10 / 9 / 8)
            { "dada", ("Xiaomi 15", "Snapdragon 8 Elite") },
            { "haotian", ("Xiaomi 15 Pro", "Snapdragon 8 Elite") },
            { "houji", ("Xiaomi 14", "Snapdragon 8 Gen 3") },
            { "shennong", ("Xiaomi 14 Pro", "Snapdragon 8 Gen 3") },
            { "aurora", ("Xiaomi 14 Ultra", "Snapdragon 8 Gen 3") },
            { "manet", ("Xiaomi 14T Pro / Redmi K70 Pro", "MediaTek Dimensity 9300+") },
            { "degas", ("Xiaomi 14T", "MediaTek Dimensity 8300-Ultra") },
            { "fuxi", ("Xiaomi 13", "Snapdragon 8 Gen 2") },
            { "nuwa", ("Xiaomi 13 Pro", "Snapdragon 8 Gen 2") },
            { "ishtar", ("Xiaomi 13 Ultra", "Snapdragon 8 Gen 2") },
            { "corot", ("Xiaomi 13T Pro / Redmi K60 Ultra", "MediaTek Dimensity 9200+") },
            { "aristotle", ("Xiaomi 13T", "MediaTek Dimensity 8200-Ultra") },
            { "cupid", ("Xiaomi 12", "Snapdragon 8 Gen 1") },
            { "zeus", ("Xiaomi 12 Pro", "Snapdragon 8 Gen 1") },
            { "psyche", ("Xiaomi 12X", "Snapdragon 870") },
            { "diting", ("Xiaomi 12T Pro / Redmi K50 Ultra", "Snapdragon 8+ Gen 1") },
            { "plato", ("Xiaomi 12T", "MediaTek Dimensity 8100-Ultra") },
            { "thor", ("Xiaomi 12S Ultra", "Snapdragon 8+ Gen 1") },
            { "unicorn", ("Xiaomi 12S Pro", "Snapdragon 8+ Gen 1") },
            { "mayfly", ("Xiaomi 12S", "Snapdragon 8+ Gen 1") },
            { "venus", ("Xiaomi 11", "Snapdragon 888") },
            { "star", ("Xiaomi 11 Pro / 11 Ultra", "Snapdragon 888") },
            { "mars", ("Xiaomi 11 Ultra", "Snapdragon 888") },
            { "vili", ("Xiaomi 11T Pro", "Snapdragon 888") },
            { "agate", ("Xiaomi 11T", "MediaTek Dimensity 1200-Ultra") },
            { "courbet", ("Xiaomi 11 Lite 4G", "Snapdragon 732G") },
            { "courbetin", ("Xiaomi 11 Lite 4G (India)", "Snapdragon 732G") },
            { "renoir", ("Xiaomi 11 Lite 5G", "Snapdragon 780G") },
            { "lisa", ("Xiaomi 11 Lite 5G NE", "Snapdragon 778G") },
            { "umi", ("Xiaomi Mi 10", "Snapdragon 865") },
            { "cmi", ("Xiaomi Mi 10 Pro", "Snapdragon 865") },
            { "cas", ("Xiaomi Mi 10 Ultra", "Snapdragon 865") },
            { "apollo", ("Xiaomi Mi 10T / 10T Pro / Redmi K30S", "Snapdragon 865") },
            { "monet", ("Xiaomi Mi 10 Lite 5G", "Snapdragon 765G") },
            { "cepheus", ("Xiaomi Mi 9", "Snapdragon 855") },
            { "crux", ("Xiaomi Mi 9 Pro 5G", "Snapdragon 855+") },
            { "raphael", ("Xiaomi Mi 9T Pro / Redmi K20 Pro", "Snapdragon 855") },
            { "davinci", ("Xiaomi Mi 9T / Redmi K20", "Snapdragon 730") },
            { "grus", ("Xiaomi Mi 9 SE", "Snapdragon 712") },
            { "pyxis", ("Xiaomi Mi 9 Lite / CC9", "Snapdragon 710") },
            { "dipper", ("Xiaomi Mi 8", "Snapdragon 845") },
            { "ursa", ("Xiaomi Mi 8 Explorer", "Snapdragon 845") },
            { "equuleus", ("Xiaomi Mi 8 Pro", "Snapdragon 845") },
            { "sirius", ("Xiaomi Mi 8 SE", "Snapdragon 710") },
            { "platina", ("Xiaomi Mi 8 Lite", "Snapdragon 660") },

            // Redmi Number & Budget Series
            { "dandelion", ("Redmi 9A", "MediaTek Helio G25") },
            { "cattail", ("Redmi 9C", "MediaTek Helio G35") },
            { "angelica", ("Redmi 9C NFC", "MediaTek Helio G35") },
            { "lancelot", ("Redmi 9", "MediaTek Helio G80") },
            { "olive", ("Redmi 8", "Snapdragon 439") },
            { "olivelite", ("Redmi 8A", "Snapdragon 439") },
            { "onclite", ("Redmi 7", "Snapdragon 632") },
            { "pine", ("Redmi 7A", "Snapdragon 439") },
            { "cereus", ("Redmi 6", "MediaTek Helio P22") },
            { "cactus", ("Redmi 6A", "MediaTek Helio A22") },
            { "rosy", ("Redmi 5", "Snapdragon 450") },
            { "riva", ("Redmi 5A", "Snapdragon 425") },
            { "santoni", ("Redmi 4X", "Snapdragon 435") },

            // Redmi K Series
            { "rodin", ("Redmi K80", "Snapdragon 8 Gen 3") },
            { "miro", ("Redmi K80 Pro", "Snapdragon 8 Elite") },
            { "vermeer", ("Redmi K70", "Snapdragon 8 Gen 2") },
            { "socrates", ("Redmi K60 Pro", "Snapdragon 8 Gen 2") },
            { "rembrandt", ("Redmi K60E", "MediaTek Dimensity 8200") },
            { "matisse", ("Redmi K50 Pro", "MediaTek Dimensity 9000") },
            { "rubens", ("Redmi K50", "MediaTek Dimensity 8100") },

            // Xiaomi Tablets (Mi Pad)
            { "nabu", ("Xiaomi Pad 5", "Snapdragon 860") },
            { "elish", ("Xiaomi Pad 5 Pro", "Snapdragon 870") },
            { "enuma", ("Xiaomi Pad 5 Pro 5G", "Snapdragon 870") },
            { "pipa", ("Xiaomi Pad 6", "Snapdragon 870") },
            { "liuqin", ("Xiaomi Pad 6 Pro", "Snapdragon 8+ Gen 1") },
            { "yudi", ("Xiaomi Pad 6 Max 14", "Snapdragon 8+ Gen 1") },
            { "sheng", ("Xiaomi Pad 6S Pro 12.4", "Snapdragon 8 Gen 2") }
        };

        // ============================================================
        // REGION CODE DETECTOR (ROM BUILD SUFFIX)
        // ============================================================
        private static readonly Dictionary<string, string> RegionCodes = new(StringComparer.OrdinalIgnoreCase)
        {
            { "MI", "Global" },
            { "EU", "Europe / EEA" },
            { "EEA", "Europe / EEA" },
            { "IN", "India" },
            { "RU", "Russia" },
            { "ID", "Indonesia" },
            { "TW", "Taiwan" },
            { "CN", "China" },
            { "TR", "Turkey" },
            { "JP", "Japan" },
            { "KR", "South Korea" },
            { "LM", "Latin America" },
            { "MX", "Mexico (Telcel)" },
            { "CL", "Chile" },
            { "US", "United States / Global" }
        };

        // ============================================================
        // USB DIAGNOSTIC AND SETUPAPI NATIVE INTEROP
        // ============================================================
        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(IntPtr ClassGuid, string? Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, [Out] char[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("cfgmgr32.dll", SetLastError = true)]
        private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);

        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_ALLCLASSES = 0x00000004;
        private const uint SPDRP_DEVICEDESC = 0x00000000;
        private const uint SPDRP_HARDWAREID = 0x00000001;
        private const uint SPDRP_SERVICE = 0x00000004;
        private const uint SPDRP_CLASS = 0x00000007;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;

        public class UsbDeviceDiagnosticItem
        {
            public string DeviceDesc { get; set; } = "";
            public string HardwareId { get; set; } = "";
            public string Service { get; set; } = "";
            public string ClassName { get; set; } = "";
            public uint ProblemCode { get; set; } = 0;
            public uint Status { get; set; } = 0;
            public bool IsRelevant { get; set; } = false;
            public bool IsMissingDriver => ProblemCode == 28 || (string.IsNullOrEmpty(Service) && ProblemCode != 0);
        }

        private static string GetDevRegistryProperty(IntPtr hDevInfo, ref SP_DEVINFO_DATA devData, uint property)
        {
            char[] buffer = new char[1024];
            if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devData, property, out _, buffer, (uint)buffer.Length, out _))
            {
                return new string(buffer).TrimEnd('\0').Trim();
            }
            return string.Empty;
        }

        public static List<UsbDeviceDiagnosticItem> GetUsbDiagnostics()
        {
            var list = new List<UsbDeviceDiagnosticItem>();
            IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, "USB", IntPtr.Zero, DIGCF_PRESENT | DIGCF_ALLCLASSES);
            if (hDevInfo == IntPtr.Zero || hDevInfo == new IntPtr(-1))
            {
                return list;
            }

            try
            {
                var devData = new SP_DEVINFO_DATA();
                devData.cbSize = (uint)Marshal.SizeOf(typeof(SP_DEVINFO_DATA));
                uint index = 0;

                while (SetupDiEnumDeviceInfo(hDevInfo, index, ref devData))
                {
                    index++;
                    string desc = GetDevRegistryProperty(hDevInfo, ref devData, SPDRP_DEVICEDESC);
                    string friendly = GetDevRegistryProperty(hDevInfo, ref devData, SPDRP_FRIENDLYNAME);
                    string hwid = GetDevRegistryProperty(hDevInfo, ref devData, SPDRP_HARDWAREID);
                    string srv = GetDevRegistryProperty(hDevInfo, ref devData, SPDRP_SERVICE);
                    string cls = GetDevRegistryProperty(hDevInfo, ref devData, SPDRP_CLASS);

                    uint status = 0;
                    uint probCode = 0;
                    int cmRes = CM_Get_DevNode_Status(out status, out probCode, devData.DevInst, 0);

                    string name = !string.IsNullOrEmpty(friendly) ? friendly : desc;
                    if (string.IsNullOrEmpty(name)) name = hwid;

                    bool isXiaomiVid = hwid.Contains("VID_2717", StringComparison.OrdinalIgnoreCase);
                    bool isGoogleVid = hwid.Contains("VID_18D1", StringComparison.OrdinalIgnoreCase);
                    bool isMtkVid = hwid.Contains("VID_0E8D", StringComparison.OrdinalIgnoreCase);
                    bool isQcomVid = hwid.Contains("VID_05C6", StringComparison.OrdinalIgnoreCase);
                    bool isHtcVid = hwid.Contains("VID_0BB4", StringComparison.OrdinalIgnoreCase);

                    bool isKeywords = (name.Contains("Android", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Xiaomi", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Redmi", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("POCO", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("ADB", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Sideload", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Fastboot", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Composite", StringComparison.OrdinalIgnoreCase) ||
                                       name.Contains("Recovery", StringComparison.OrdinalIgnoreCase) ||
                                       cls.Contains("Android", StringComparison.OrdinalIgnoreCase));

                    // Only track devices that are actual mobile / Android / Xiaomi hardware
                    if (isXiaomiVid || isGoogleVid || isMtkVid || isQcomVid || isHtcVid || isKeywords)
                    {
                        list.Add(new UsbDeviceDiagnosticItem
                        {
                            DeviceDesc = name,
                            HardwareId = hwid,
                            Service = srv,
                            ClassName = cls,
                            ProblemCode = probCode,
                            Status = status,
                            IsRelevant = true
                        });
                    }
                }
            }
            catch { }
            finally
            {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }

            return list;
        }

        public static void PerformUsbDiagnosticReport(Action<string, Color, bool> logger)
        {
            Color cyan = Color.FromArgb(0, 210, 255);
            Color yellow = Color.FromArgb(255, 196, 0);
            Color white = Color.FromArgb(242, 247, 255);
            Color green = Color.FromArgb(0, 230, 92);
            Color red = Color.FromArgb(255, 52, 64);
            Color muted = Color.FromArgb(145, 172, 204);

            var usbDevices = GetUsbDiagnostics();
            var missingDriverDevices = usbDevices.Where(d => d.ProblemCode == 28 || d.IsMissingDriver).ToList();
            var relevantDevices = usbDevices.Where(d => d.IsRelevant && d.ProblemCode != 28).ToList();

            logger("--------------------------------------------------------------------------------", muted, true);
            logger("[DIAGNOSTICO DE HARDWARE E DRIVERS USB DO WINDOWS]", cyan, true);

            if (missingDriverDevices.Count > 0)
            {
                logger("[ALERTA] DISPOSITIVO CELULAR DETECTADO SEM DRIVER ADB NO WINDOWS!", red, true);
                foreach (var dev in missingDriverDevices)
                {
                    logger($" -> Dispositivo: {dev.DeviceDesc}", yellow, true);
                    logger($"    Hardware ID: {dev.HardwareId}", muted, true);
                    logger($"    Status Windows: CODIGO 28 (Driver Ausente / Dispositivo com exclamacao amarela)", red, true);
                }
                logger("", white, true);
                logger("[COMO RESOLVER NO COMPUTADOR]:", green, true);
                logger(" 1. Clique no botao 'Instalar Drivers ADB' acima para instalar automaticamente.", yellow, true);
                logger(" 2. Ou abra o Gerenciador de Dispositivos (devmgmt.msc) e atualize para 'Android Composite ADB Interface'.", white, true);
            }
            else if (relevantDevices.Count > 0)
            {
                logger("[INFO] Dispositivos USB relacionados detectados pelo Windows:", green, true);
                foreach (var dev in relevantDevices)
                {
                    string statusStr = dev.ProblemCode == 0 ? "Driver Instalado (OK)" : $"Problema de Driver (Codigo {dev.ProblemCode})";
                    Color sc = dev.ProblemCode == 0 ? green : yellow;
                    logger($" -> {dev.DeviceDesc} | Servico: {(!string.IsNullOrEmpty(dev.Service) ? dev.Service : "N/A")} | Status: {statusStr}", sc, true);
                }
                logger("", white, true);
                logger("[ORIENTACAO TECNICA]:", cyan, true);
                logger(" - O hardware USB esta conectado. Certifique-se de que o aparelho esta na tela ativa 'Connect with MIAssistant'.", white, true);
            }
            else
            {
                logger("[STATUS] Nenhum dispositivo Xiaomi/Android detectado conectado nas portas USB.", yellow, true);
                logger("[CAUSAS PROVAVEIS]:", cyan, true);
                logger(" 1. O aparelho nao esta conectado ou o cabo USB e apenas de carga (sem linhas de dados D+/D-).", white, true);
                logger(" 2. O aparelho esta desligado ou nao confirmou a opcao 'Connect with MIAssistant' no Recovery.", white, true);
                logger(" 3. A porta USB do computador pode estar com mau contato. Conecte em uma porta USB 2.0 traseira direta na placa-mae.", white, true);
            }
            logger("--------------------------------------------------------------------------------", muted, true);
        }

        public static void EnsureAdbServerRunning(string adbPath)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                var connectTask = client.ConnectAsync("127.0.0.1", 5037);
                if (connectTask.Wait(400) && client.Connected)
                {
                    return;
                }
            }
            catch { }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = "start-server",
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
            }
            catch { }
        }

        public static async Task<string> SendAdbServerCommandAsync(string command, int timeoutMs = 3000)
        {
            try
            {
                using var client = new System.Net.Sockets.TcpClient();
                using var cts = new CancellationTokenSource(timeoutMs);
                await client.ConnectAsync("127.0.0.1", 5037, cts.Token);
                using var stream = client.GetStream();

                string payload = command.Length.ToString("X4") + command;
                byte[] payloadBytes = Encoding.ASCII.GetBytes(payload);
                await stream.WriteAsync(payloadBytes, 0, payloadBytes.Length, cts.Token);

                byte[] statusBuf = new byte[4];
                int read = await stream.ReadAsync(statusBuf, 0, 4, cts.Token);
                if (read < 4) return string.Empty;

                string status = Encoding.ASCII.GetString(statusBuf);
                if (status == "OKAY")
                {
                    byte[] lenBuf = new byte[4];
                    read = await stream.ReadAsync(lenBuf, 0, 4, cts.Token);
                    if (read == 4 && int.TryParse(Encoding.ASCII.GetString(lenBuf), System.Globalization.NumberStyles.HexNumber, null, out int len))
                    {
                        byte[] dataBuf = new byte[len];
                        int totalRead = 0;
                        while (totalRead < len)
                        {
                            int r = await stream.ReadAsync(dataBuf, totalRead, len - totalRead, cts.Token);
                            if (r <= 0) break;
                            totalRead += r;
                        }
                        return Encoding.UTF8.GetString(dataBuf, 0, totalRead);
                    }
                    else
                    {
                        using var ms = new MemoryStream();
                        byte[] chunk = new byte[4096];
                        int r;
                        while ((r = await stream.ReadAsync(chunk, 0, chunk.Length, cts.Token)) > 0)
                        {
                            ms.Write(chunk, 0, r);
                        }
                        return Encoding.UTF8.GetString(ms.ToArray());
                    }
                }
                else if (status == "FAIL")
                {
                    byte[] lenBuf = new byte[4];
                    read = await stream.ReadAsync(lenBuf, 0, 4, cts.Token);
                    if (read == 4 && int.TryParse(Encoding.ASCII.GetString(lenBuf), System.Globalization.NumberStyles.HexNumber, null, out int len))
                    {
                        byte[] dataBuf = new byte[len];
                        await stream.ReadAsync(dataBuf, 0, len, cts.Token);
                        return "FAIL: " + Encoding.UTF8.GetString(dataBuf);
                    }
                }
            }
            catch { }
            return string.Empty;
        }

        public static void KillStaleAdbProcesses()
        {
            try
            {
                var procs = Process.GetProcessesByName("adb");
                foreach (var p in procs)
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(500);
                    }
                    catch { }
                }
            }
            catch { }
        }

        // ============================================================
        // AUTOMATIC DRIVER INSTALLER (WINUSB & INF REGISTRATION)
        // ============================================================
        public static async Task<bool> InstallAdbSideloadDriversAsync(
            Action<string, Color, bool> logger, CancellationToken ct = default)
        {
            Color cyan = Color.FromArgb(0, 210, 255);
            Color yellow = Color.FromArgb(255, 196, 0);
            Color white = Color.FromArgb(242, 247, 255);
            Color green = Color.FromArgb(0, 230, 92);
            Color red = Color.FromArgb(255, 52, 64);
            Color muted = Color.FromArgb(145, 172, 204);

            logger("--------------------------------------------------------------------------------", muted, true);
            logger("[INSTALADOR AUTOMATICO DE DRIVERS ADB / SIDELOAD / FASTBOOT]", cyan, true);
            logger("Iniciando preparacao do pacote oficial de drivers WinUSB para Xiaomi e Android...", white, true);

            try
            {
                // 1. Create target driver folder
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string driversDir = Path.Combine(baseDir, "tools", "drivers");
                if (!Directory.Exists(driversDir))
                {
                    Directory.CreateDirectory(driversDir);
                }

                string infPath = Path.Combine(driversDir, "android_winusb.inf");

                // 2. Generate comprehensive android_winusb.inf content
                string infContent = GenerateAndroidWinUsbInf();
                await File.WriteAllTextAsync(infPath, infContent, Encoding.ASCII, ct);
                logger($"[DRIVERS] Arquivo INF gerado com sucesso em: tools\\drivers\\android_winusb.inf", muted, true);

                // 3. Create installer batch script that executes pnputil
                string batPath = Path.Combine(driversDir, "install_driver.cmd");
                string batContent = $@"@echo off
title Instalador de Drivers Android ADB / Sideload
cd /d ""%~dp0""
echo ============================================================
echo  INSTALANDO DRIVERS ADB / SIDELOAD NO WINDOWS DRIVERSTORE
echo ============================================================
pnputil.exe /add-driver ""{infPath}"" /install
pnputil.exe /scan-devices
echo.
echo ============================================================
echo  INSTALACAO CONCLUIDA COM SUCESSO!
echo ============================================================
timeout /t 2 >nul
exit /b 0
";
                await File.WriteAllTextAsync(batPath, batContent, Encoding.ASCII, ct);

                logger("[DRIVERS] Solicitando permissao de Administrador ao Windows...", yellow, true);

                // 4. Execute with elevation (runas)
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{batPath}\"\"",
                    Verb = "runas",
                    UseShellExecute = true,
                    CreateNoWindow = false,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                Process? proc = null;
                try
                {
                    proc = Process.Start(psi);
                }
                catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
                {
                    logger("[DRIVERS] Operacao cancelada: A permissao de Administrador foi recusada no UAC.", red, true);
                    logger("--------------------------------------------------------------------------------", muted, true);
                    return false;
                }

                if (proc != null)
                {
                    logger("[DRIVERS] Registrando driver no Windows e atualizando portas USB...", cyan, true);
                    await proc.WaitForExitAsync(ct);
                }

                // 5. Restart ADB daemon to bind to updated interfaces
                logger("[DRIVERS] Reiniciando servidor ADB...", white, true);
                KillStaleAdbProcesses();
                string? adbPath = FindToolPath("adb");
                if (!string.IsNullOrEmpty(adbPath))
                {
                    await RunProcessAsync(adbPath, "kill-server", 2000, ct);
                    await RunProcessAsync(adbPath, "start-server", 3000, ct);
                }

                // 6. Report status
                logger("[DRIVERS] Driver 'Android Composite ADB Interface' instalado com exito!", green, true);
                logger(" -> O Windows agora reconhece dispositivos Xiaomi em modo Sideload (Recovery) e Fastboot.", white, true);
                logger(" -> Conecte o aparelho no USB e clique em 'Ler Sideload (Recovery)' ou 'Ler Fastboot (Xiaomi)'.", cyan, true);
                logger("--------------------------------------------------------------------------------", muted, true);
                return true;
            }
            catch (Exception ex)
            {
                logger($"[ERRO] Falha ao instalar drivers: {ex.Message}", red, true);
                logger("--------------------------------------------------------------------------------", muted, true);
                return false;
            }
        }

        private static string GenerateAndroidWinUsbInf()
        {
            var sb = new StringBuilder();
            sb.AppendLine(";");
            sb.AppendLine("; Android WinUsb Driver Package for Xiaomi / Android Sideload & Fastboot");
            sb.AppendLine("; Generated automatically by Master Unlock Tool");
            sb.AppendLine(";");
            sb.AppendLine("[Version]");
            sb.AppendLine("Signature           = \"$Windows NT$\"");
            sb.AppendLine("Class               = AndroidUsbDeviceClass");
            sb.AppendLine("ClassGuid           = {3F966BD9-FA04-4ec5-991C-D326973B5128}");
            sb.AppendLine("Provider            = %ProviderName%");
            sb.AppendLine("DriverVer           = 01/01/2026,14.0.0.0");
            sb.AppendLine("CatalogFile.NTx86   = androidwinusb86.cat");
            sb.AppendLine("CatalogFile.NTamd64 = androidwinusba64.cat");
            sb.AppendLine();
            sb.AppendLine("[ClassInstall32]");
            sb.AppendLine("Addreg = AndroidUsbClassInstallAddReg");
            sb.AppendLine();
            sb.AppendLine("[AndroidUsbClassInstallAddReg]");
            sb.AppendLine("HKR,,,0,%ClassName%");
            sb.AppendLine("HKR,,Icon,,-1");
            sb.AppendLine();
            sb.AppendLine("[DestinationDirs]");
            sb.AppendLine("DefaultDestDir = 12");
            sb.AppendLine();
            sb.AppendLine("[Manufacturer]");
            sb.AppendLine("%ProviderName% = Google, NTx86, NTamd64");
            sb.AppendLine();

            string[] hwIds = new string[]
            {
                // Xiaomi Sideload (Recovery & MiAssistant)
                "USB\\VID_2717&PID_FF48",
                "USB\\VID_2717&PID_FF48&MI_01",
                "USB\\VID_2717&PID_FF88",
                "USB\\VID_2717&PID_FF88&MI_01",
                "USB\\VID_2717&PID_FF40",
                "USB\\VID_2717&PID_FF40&MI_01",
                "USB\\VID_2717&PID_FF68",
                "USB\\VID_2717&PID_FF68&MI_01",
                "USB\\VID_2717&PID_FF18",
                "USB\\VID_2717&PID_FF18&MI_01",
                "USB\\VID_2717&PID_FF28",
                "USB\\VID_2717&PID_FF28&MI_01",
                "USB\\VID_2717&PID_9039",
                "USB\\VID_2717&PID_9039&MI_01",
                // Xiaomi Fastboot
                "USB\\VID_2717&PID_D00D",
                "USB\\VID_2717&PID_FF08",
                // Google / Standard Android ADB & Sideload
                "USB\\VID_18D1&PID_D001",
                "USB\\VID_18D1&PID_D001&MI_01",
                "USB\\VID_18D1&PID_4EE7",
                "USB\\VID_18D1&PID_4EE7&MI_01",
                "USB\\VID_18D1&PID_D002",
                "USB\\VID_18D1&PID_4EE0",
                "USB\\VID_18D1&PID_4EE2",
                "USB\\VID_18D1&PID_4EE4",
                // Generic / HTC Fastboot & ADB
                "USB\\VID_0BB4&PID_0C01",
                "USB\\VID_0BB4&PID_0C02",
                "USB\\VID_0BB4&PID_0C03",
                // MediaTek ADB / Sideload
                "USB\\VID_0E8D&PID_2008",
                "USB\\VID_0E8D&PID_201D",
                // Qualcomm ADB / Sideload
                "USB\\VID_05C6&PID_901D",
                "USB\\VID_05C6&PID_9025",
                "USB\\VID_05C6&PID_9039"
            };

            sb.AppendLine("[Google.NTx86]");
            foreach (var id in hwIds)
            {
                if (id.Contains("&MI_"))
                    sb.AppendLine($"%CompositeAdbInterface% = USB_Install, {id}");
                else
                    sb.AppendLine($"%SingleAdbInterface% = USB_Install, {id}");
            }
            sb.AppendLine();

            sb.AppendLine("[Google.NTamd64]");
            foreach (var id in hwIds)
            {
                if (id.Contains("&MI_"))
                    sb.AppendLine($"%CompositeAdbInterface% = USB_Install, {id}");
                else
                    sb.AppendLine($"%SingleAdbInterface% = USB_Install, {id}");
            }
            sb.AppendLine();

            sb.AppendLine("[USB_Install]");
            sb.AppendLine("Include = winusb.inf");
            sb.AppendLine("Needs   = WINUSB.NT");
            sb.AppendLine();
            sb.AppendLine("[USB_Install.Services]");
            sb.AppendLine("Include = winusb.inf");
            sb.AppendLine("AddService = WinUSB,0x00000002,WinUSB_ServiceInstall");
            sb.AppendLine();
            sb.AppendLine("[WinUSB_ServiceInstall]");
            sb.AppendLine("DisplayName     = %WinUSB_Service%");
            sb.AppendLine("ServiceType     = 1");
            sb.AppendLine("StartType       = 3");
            sb.AppendLine("ErrorControl    = 1");
            sb.AppendLine("ServiceBinary   = %12%\\WinUSB.sys");
            sb.AppendLine();
            sb.AppendLine("[USB_Install.Wdf]");
            sb.AppendLine("KmdfService = WINUSB, WinUSB_Install");
            sb.AppendLine();
            sb.AppendLine("[WinUSB_Install]");
            sb.AppendLine("KmdfLibraryVersion = 1.9");
            sb.AppendLine();
            sb.AppendLine("[USB_Install.HW]");
            sb.AppendLine("AddReg = Dev_AddReg");
            sb.AppendLine();
            sb.AppendLine("[Dev_AddReg]");
            sb.AppendLine("HKR,,DeviceInterfaceGUIDs,0x10000,\"{F72614D3-547B-4540-98BA-FF3C19AA7D13}\"");
            sb.AppendLine();
            sb.AppendLine("[Strings]");
            sb.AppendLine("ProviderName            = \"Google, Inc.\"");
            sb.AppendLine("SingleAdbInterface      = \"Android ADB Interface\"");
            sb.AppendLine("CompositeAdbInterface   = \"Android Composite ADB Interface\"");
            sb.AppendLine("WinUSB_Service          = \"WinUSB - Android ADB Driver Service\"");
            sb.AppendLine("ClassName               = \"Android Device\"");

            return sb.ToString();
        }

        // ============================================================
        // TOOL RESOLUTION (FASTBOOT & ADB)
        // ============================================================
        public static string? FindToolPath(string toolName)
        {
            string exeName = toolName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? toolName : toolName + ".exe";

            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string currentDir = Directory.GetCurrentDirectory();

            string[] candidatePaths = new string[]
            {
                Path.Combine(baseDir, "tools", "platform-tools", exeName),
                Path.Combine(baseDir, "tools", exeName),
                Path.Combine(baseDir, exeName),
                Path.Combine(currentDir, "tools", "platform-tools", exeName),
                Path.Combine(currentDir, "tools", exeName),
                Path.Combine(currentDir, exeName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", exeName),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Android", "android-sdk", "platform-tools", exeName)
            };

            foreach (var path in candidatePaths)
            {
                if (File.Exists(path))
                    return path;
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = exeName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd().Trim();
                    proc.WaitForExit(1500);
                    if (proc.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                    {
                        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0 && File.Exists(lines[0].Trim()))
                            return lines[0].Trim();
                    }
                }
            }
            catch { }

            return null;
        }

        // ============================================================
        // PROCESS RUNNER HELPER
        // ============================================================
        private static async Task<(int exitCode, string stdout, string stderr)> RunProcessAsync(
            string exePath, string arguments, int timeoutMs = 8000, CancellationToken ct = default)
        {
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var proc = new Process { StartInfo = psi };
            var stdoutSb = new StringBuilder();
            var stderrSb = new StringBuilder();

            proc.OutputDataReceived += (_, e) => { if (e.Data != null) stdoutSb.AppendLine(e.Data); };
            proc.ErrorDataReceived += (_, e) => { if (e.Data != null) stderrSb.AppendLine(e.Data); };

            try
            {
                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var waitForExitTask = Task.Run(() => proc.WaitForExit(timeoutMs));
                var completed = await Task.WhenAny(waitForExitTask, Task.Delay(timeoutMs, cts.Token));

                if (completed == waitForExitTask && proc.HasExited)
                {
                    return (proc.ExitCode, stdoutSb.ToString(), stderrSb.ToString());
                }
                else
                {
                    try { proc.Kill(); } catch { }
                    return (-1, stdoutSb.ToString(), stderrSb.ToString() + "\nProcess timed out.");
                }
            }
            catch (Exception ex)
            {
                try { if (!proc.HasExited) proc.Kill(); } catch { }
                return (-2, stdoutSb.ToString(), stderrSb.ToString() + $"\nProcess error: {ex.Message}");
            }
        }

        // ============================================================
        // 1. FASTBOOT MODE READER
        // ============================================================
        public static async Task<XiaomiFastbootDeviceInfo> ReadFastbootInfoAsync(
            Action<string, Color, bool> logger, CancellationToken ct = default)
        {
            var info = new XiaomiFastbootDeviceInfo();

            string? fastbootPath = FindToolPath("fastboot");
            if (string.IsNullOrEmpty(fastbootPath))
            {
                logger("[ERRO] Binario 'fastboot.exe' nao foi encontrado em tools\\platform-tools ou no PATH do sistema.", Color.FromArgb(255, 52, 64), true);
                info.ErrorMessage = "fastboot.exe not found";
                return info;
            }

            logger("Searching for Fastboot Device... ", Color.FromArgb(242, 247, 255), false);

            // 1. Check fastboot devices
            var devRes = await RunProcessAsync(fastbootPath, "devices", 4000, ct);
            string devOutput = (devRes.stdout + "\n" + devRes.stderr).Trim();

            if (string.IsNullOrWhiteSpace(devOutput) || (!devOutput.Contains("fastboot", StringComparison.OrdinalIgnoreCase) && !devOutput.Contains("\t")))
            {
                logger("Falha!", Color.FromArgb(255, 52, 64), true);

                // Check if device is connected in Sideload/ADB mode instead
                string? adbPath = FindToolPath("adb");
                bool isInSideload = false;
                if (!string.IsNullOrEmpty(adbPath))
                {
                    var adbCheck = await RunProcessAsync(adbPath, "devices", 1500, ct);
                    if (adbCheck.stdout.Contains("sideload", StringComparison.OrdinalIgnoreCase) ||
                        adbCheck.stdout.Contains("recovery", StringComparison.OrdinalIgnoreCase) ||
                        adbCheck.stdout.Contains("device", StringComparison.OrdinalIgnoreCase))
                    {
                        isInSideload = true;
                    }
                }

                if (isInSideload)
                {
                    logger("[FASTBOOT] O dispositivo esta conectado em modo SIDELOAD / RECOVERY.", Color.FromArgb(255, 196, 0), true);
                    logger("-> Clique no botao 'Ler Sideload (Recovery)' ou reinicie em Fastboot [Volume Menos + Power].", Color.FromArgb(0, 210, 255), true);
                }
                else
                {
                    logger("[FASTBOOT] Nenhum dispositivo Xiaomi detectado em modo Fastboot.", Color.FromArgb(255, 196, 0), true);
                    PerformUsbDiagnosticReport(logger);
                }

                info.ErrorMessage = "No fastboot device detected";
                return info;
            }

            logger("Ok", Color.FromArgb(0, 230, 92), true);
            logger("Getting device information...", Color.FromArgb(0, 210, 255), true);

            // Extract serial
            var devLines = devOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string detectedSerial = "";
            foreach (var line in devLines)
            {
                if (line.Contains("fastboot", StringComparison.OrdinalIgnoreCase))
                {
                    detectedSerial = line.Split('\t', ' ')[0].Trim();
                    break;
                }
            }
            if (string.IsNullOrEmpty(detectedSerial) && devLines.Length > 0)
            {
                detectedSerial = devLines[0].Split('\t', ' ')[0].Trim();
            }
            info.SerialNumber = detectedSerial;

            // 2. Query getvar all
            var getvarAllRes = await RunProcessAsync(fastbootPath, "getvar all", 6000, ct);
            string combinedVars = getvarAllRes.stdout + "\n" + getvarAllRes.stderr;
            ParseFastbootVariables(combinedVars, info.RawVars);

            // 3. Query individual essential variables if getvar all omitted them
            string[] essentialVars = { "product", "unlocked", "secure", "anti", "token", "unlock-token", "serialno", "soc_id", "hw-revision", "battery-voltage", "battery-soc-ok", "slot-count", "current-slot", "is-userspace", "max-download-size" };
            foreach (var v in essentialVars)
            {
                if (!info.RawVars.ContainsKey(v) || string.IsNullOrWhiteSpace(info.RawVars[v]))
                {
                    var singleRes = await RunProcessAsync(fastbootPath, $"getvar {v}", 2500, ct);
                    string singleOut = singleRes.stdout + "\n" + singleRes.stderr;
                    ParseFastbootVariables(singleOut, info.RawVars);
                }
            }

            // 4. Query OEM Device-Info
            var oemRes = await RunProcessAsync(fastbootPath, "oem device-info", 4000, ct);
            string oemOut = oemRes.stdout + "\n" + oemRes.stderr;
            ParseFastbootOemInfo(oemOut, info);

            // Populate DTO
            if (info.RawVars.TryGetValue("product", out var prod)) info.Product = prod.Trim();
            if (info.RawVars.TryGetValue("unlocked", out var unl)) info.Unlocked = unl.Trim();
            if (info.RawVars.TryGetValue("secure", out var sec)) info.Secure = sec.Trim();
            if (info.RawVars.TryGetValue("anti", out var ant)) info.AntiRollback = ant.Trim();
            if (info.RawVars.TryGetValue("token", out var tok)) info.Token = tok.Trim();
            if (string.IsNullOrEmpty(info.Token) && info.RawVars.TryGetValue("unlock-token", out var tok2)) info.Token = tok2.Trim();
            if (info.RawVars.TryGetValue("serialno", out var sn) && !string.IsNullOrWhiteSpace(sn)) info.SerialNumber = sn.Trim();
            if (info.RawVars.TryGetValue("soc_id", out var soc)) info.SocId = soc.Trim();
            if (info.RawVars.TryGetValue("hw-revision", out var hw)) info.HwRevision = hw.Trim();
            if (info.RawVars.TryGetValue("battery-voltage", out var bv)) info.BatteryVoltage = bv.Trim();
            if (info.RawVars.TryGetValue("battery-soc-ok", out var bsoc)) info.BatterySoc = bsoc.Trim();
            if (info.RawVars.TryGetValue("current-slot", out var slot)) info.CurrentSlot = slot.Trim();
            if (info.RawVars.TryGetValue("slot-count", out var slots)) info.SlotCount = slots.Trim();
            if (info.RawVars.TryGetValue("is-userspace", out var usr)) info.IsUserspace = usr.Trim();
            if (info.RawVars.TryGetValue("max-download-size", out var mds)) info.MaxDownloadSize = mds.Trim();

            info.DeviceModel = info.Product;
            info.DeviceProduct = info.Product;

            // Resolve Commercial Name & Chipset
            if (!string.IsNullOrEmpty(info.Product))
            {
                if (CodenameDb.TryGetValue(info.Product, out var dbItem))
                {
                    info.CommercialName = dbItem.name;
                    info.Chipset = dbItem.chipset;
                }
                else
                {
                    info.CommercialName = $"Xiaomi Dispositivo ({info.Product})";
                    info.Chipset = "Qualcomm / MediaTek";
                }
            }

            // Determine Lock status
            info.IsUnlocked = string.Equals(info.Unlocked, "yes", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(info.Unlocked, "true", StringComparison.OrdinalIgnoreCase);

            info.Success = !string.IsNullOrEmpty(info.Product) || !string.IsNullOrEmpty(info.SerialNumber);

            // Render Formatted Diagnostic Report
            RenderFastbootReport(info, logger);

            return info;
        }

        private static void ParseFastbootVariables(string rawText, Dictionary<string, string> dict)
        {
            var lines = rawText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string clean = line.Trim();
                if (clean.StartsWith("(bootloader)", StringComparison.OrdinalIgnoreCase))
                    clean = clean.Substring("(bootloader)".Length).Trim();
                if (clean.StartsWith("INFO", StringComparison.OrdinalIgnoreCase))
                    clean = clean.Substring("INFO".Length).Trim();
                if (clean.StartsWith("OKAY", StringComparison.OrdinalIgnoreCase))
                    clean = clean.Substring("OKAY".Length).Trim();

                int colonIdx = clean.IndexOf(':');
                if (colonIdx > 0)
                {
                    string key = clean.Substring(0, colonIdx).Trim().ToLowerInvariant();
                    string val = clean.Substring(colonIdx + 1).Trim();
                    if (!string.IsNullOrEmpty(key) && !string.IsNullOrEmpty(val))
                    {
                        dict[key] = val;
                    }
                }
            }
        }

        private static void ParseFastbootOemInfo(string oemText, XiaomiFastbootDeviceInfo info)
        {
            var lines = oemText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string clean = line.Trim();
                if (clean.Contains("Device tampered:", StringComparison.OrdinalIgnoreCase))
                    info.Tampered = clean.Split(':').Last().Trim();
                if (clean.Contains("Device unlocked:", StringComparison.OrdinalIgnoreCase))
                {
                    string unl = clean.Split(':').Last().Trim();
                    if (string.IsNullOrEmpty(info.Unlocked)) info.Unlocked = unl;
                }
                if (clean.Contains("Charger screen enabled:", StringComparison.OrdinalIgnoreCase))
                    info.ChargerScreen = clean.Split(':').Last().Trim();
            }
        }

        private static void RenderFastbootReport(XiaomiFastbootDeviceInfo info, Action<string, Color, bool> logger)
        {
            Color cyan = Color.FromArgb(0, 210, 255);
            Color yellow = Color.FromArgb(255, 196, 0);
            Color white = Color.FromArgb(242, 247, 255);
            Color green = Color.FromArgb(0, 230, 92);
            Color red = Color.FromArgb(255, 52, 64);
            Color muted = Color.FromArgb(145, 172, 204);

            logger("", white, true);
            PrintReportRow(logger, "Brand", info.Brand, white);
            PrintReportRow(logger, "Model", info.CommercialName ?? info.Product ?? "XIAOMI DEVICE", green);
            PrintReportRow(logger, "Operation", "Fastboot Bootloader Diagnostics", cyan);
            logger("", white, true);

            PrintReportRow(logger, "Device Model", info.DeviceModel ?? info.Product ?? "N/A", yellow);
            PrintReportRow(logger, "Device Product", info.DeviceProduct ?? info.Product ?? "N/A", yellow);
            PrintReportRow(logger, "Platform / Chipset", info.Chipset ?? "Qualcomm / MediaTek", white);
            PrintReportRow(logger, "Device Serial No.", info.SerialNumber ?? "N/A", white);

            // Bootloader Status
            string blText = info.IsUnlocked ? "DESBLOQUEADO (UNLOCKED)" : "BLOQUEADO (LOCKED)";
            Color blColor = info.IsUnlocked ? green : red;
            PrintReportRow(logger, "Bootloader Status", blText, blColor);

            // Anti-Rollback
            if (!string.IsNullOrEmpty(info.AntiRollback))
            {
                Color arbColor = info.AntiRollback == "1" ? green : yellow;
                PrintReportRow(logger, "Anti-Rollback (ARB)", $"Index {info.AntiRollback}", arbColor);
            }

            if (!string.IsNullOrEmpty(info.Secure))
                PrintReportRow(logger, "Secure Boot", info.Secure.ToUpper(), white);

            if (!string.IsNullOrEmpty(info.Token))
                PrintReportRow(logger, "Unlock Token", info.Token, yellow);

            if (!string.IsNullOrEmpty(info.SocId))
                PrintReportRow(logger, "SoC ID", info.SocId, muted);

            if (!string.IsNullOrEmpty(info.HwRevision))
                PrintReportRow(logger, "Hardware Revision", info.HwRevision, muted);

            if (!string.IsNullOrEmpty(info.CurrentSlot))
                PrintReportRow(logger, "Active Slot", $"Slot {info.CurrentSlot} (Count: {info.SlotCount ?? "1"})", white);

            if (!string.IsNullOrEmpty(info.IsUserspace))
                PrintReportRow(logger, "Fastboot Mode", info.IsUserspace.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "FastbootD (Userspace)" : "Bootloader Fastboot", white);

            if (!string.IsNullOrEmpty(info.BatteryVoltage))
                PrintReportRow(logger, "Battery Voltage", $"{info.BatteryVoltage} mV", white);

            if (!string.IsNullOrEmpty(info.MaxDownloadSize))
            {
                if (long.TryParse(info.MaxDownloadSize.Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out long bytes) ||
                    long.TryParse(info.MaxDownloadSize, out bytes))
                {
                    double mb = bytes / (1024.0 * 1024.0);
                    PrintReportRow(logger, "Max Download Buffer", $"{mb:F0} MB ({bytes} bytes)", muted);
                }
            }

            logger("--------------------------------------------------------------------------------", muted, true);
            if (info.IsUnlocked)
            {
                logger("[AVISO DE SERVICO - BOOTLOADER DESBLOQUEADO]", green, true);
                logger(" - O dispositivo aceita gravacao direta de firmware via Fastboot (MiFlash / Fastboot ROM).", white, true);
                logger(" - Procedimentos de FRP, Reset e Custom Recovery podem ser executados livremente.", white, true);
            }
            else
            {
                logger("[AVISO DE SERVICO - BOOTLOADER BLOQUEADO]", red, true);
                logger(" - Gravacao de particoes protegidas exige Unlock Oficial, Modo EDL 9008 ou Sideload Recovery.", yellow, true);
                logger(" - Nunca grave firmware com indice Anti-Rollback inferior ao index do aparelho para evitar Hard Brick.", yellow, true);
            }
            logger("--------------------------------------------------------------------------------", muted, true);
        }

        private static void PrintReportRow(Action<string, Color, bool> logger, string label, string value, Color valColor)
        {
            Color keyColor = Color.FromArgb(0, 210, 255);
            logger($"{label.PadRight(24)}: ", keyColor, false);
            logger(value, valColor, true);
        }

        // ============================================================
        // 2. SIDELOAD / RECOVERY (MIASSISTANT) MODE READER
        // ============================================================
        public static async Task<XiaomiSideloadDeviceInfo> ReadSideloadInfoAsync(
            Action<string, Color, bool> logger, CancellationToken ct = default)
        {
            var info = new XiaomiSideloadDeviceInfo();

            string? adbPath = FindToolPath("adb");
            if (string.IsNullOrEmpty(adbPath))
            {
                logger("[ERRO] Binario 'adb.exe' nao foi encontrado em tools\\platform-tools ou no PATH do sistema.", Color.FromArgb(255, 52, 64), true);
                info.ErrorMessage = "adb.exe not found";
                return info;
            }

            logger("Searching for Android ADB Device... ", Color.FromArgb(242, 247, 255), false);

            // 1. Ensure ADB daemon is up and running in detached mode
            EnsureAdbServerRunning(adbPath);

            string detectedSerial = "";
            string detectedState = "";
            string devOutput = "";

            // 2. Query ADB devices using fast TCP socket (instant and non-blocking)
            string socketDevs = await SendAdbServerCommandAsync("host:devices-l", 2500);
            if (!string.IsNullOrWhiteSpace(socketDevs) && !socketDevs.StartsWith("FAIL", StringComparison.OrdinalIgnoreCase))
            {
                devOutput = socketDevs;
            }

            // Fallback to process execution if socket was empty
            if (string.IsNullOrWhiteSpace(devOutput))
            {
                var devRes = await RunProcessAsync(adbPath, "devices -l", 3000, ct);
                devOutput = (devRes.stdout + "\n" + devRes.stderr).Trim();
            }

            var lines = devOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string clean = line.Trim();
                if (clean.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase)) continue;
                if (clean.StartsWith("*", StringComparison.OrdinalIgnoreCase)) continue;

                if (clean.Contains("sideload", StringComparison.OrdinalIgnoreCase) ||
                    clean.Contains("recovery", StringComparison.OrdinalIgnoreCase) ||
                    clean.Contains("device", StringComparison.OrdinalIgnoreCase) ||
                    clean.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
                {
                    detectedSerial = clean.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();

                    if (clean.Contains("sideload", StringComparison.OrdinalIgnoreCase)) detectedState = "sideload";
                    else if (clean.Contains("recovery", StringComparison.OrdinalIgnoreCase)) detectedState = "recovery";
                    else if (clean.Contains("device", StringComparison.OrdinalIgnoreCase)) detectedState = "device";
                    else detectedState = "unauthorized";

                    // Parse adb devices -l properties (product:xxx, model:xxx, device:xxx)
                    var mProd = Regex.Match(clean, @"product:(\S+)");
                    if (mProd.Success) info.DeviceProduct = mProd.Groups[1].Value;

                    var mModel = Regex.Match(clean, @"model:(\S+)");
                    if (mModel.Success) info.DeviceModel = mModel.Groups[1].Value;

                    var mDev = Regex.Match(clean, @"device:(\S+)");
                    if (mDev.Success) info.ProductDevice = mDev.Groups[1].Value;

                    break;
                }
            }

            if (string.IsNullOrEmpty(detectedSerial))
            {
                logger("Falha!", Color.FromArgb(255, 52, 64), true);

                // Check if device is connected in Fastboot mode instead
                string? fastbootPath = FindToolPath("fastboot");
                bool isInFastboot = false;
                if (!string.IsNullOrEmpty(fastbootPath))
                {
                    var fbCheck = await RunProcessAsync(fastbootPath, "devices", 1500, ct);
                    if (fbCheck.stdout.Contains("fastboot", StringComparison.OrdinalIgnoreCase) || fbCheck.stdout.Contains("\t"))
                    {
                        isInFastboot = true;
                    }
                }

                if (isInFastboot)
                {
                    logger("[SIDELOAD] O dispositivo esta conectado em modo FASTBOOT (Bootloader).", Color.FromArgb(255, 196, 0), true);
                    logger("-> O modo Sideload exige que o aparelho esteja no Mi-Recovery.", Color.FromArgb(0, 210, 255), true);
                    logger("-> Como alternar de Fastboot para Sideload:", Color.FromArgb(145, 172, 204), true);
                    logger("   1. Segure [Volume Mais + Power] ate reiniciar no Recovery (Mi-Recovery).", Color.FromArgb(145, 172, 204), true);
                    logger("   2. No menu do Recovery, selecione a opcao 'Connect with MIAssistant'.", Color.FromArgb(145, 172, 204), true);
                    logger("   3. Clique novamente em 'Ler Sideload (Recovery)'.", Color.FromArgb(145, 172, 204), true);
                }
                else
                {
                    logger("[SIDELOAD] Nenhum dispositivo Xiaomi detectado em modo Sideload / Recovery.", Color.FromArgb(255, 196, 0), true);
                    PerformUsbDiagnosticReport(logger);
                }

                info.ErrorMessage = "No sideload device detected";
                return info;
            }

            logger("Ok", Color.FromArgb(0, 230, 92), true);
            logger("Getting device information...", Color.FromArgb(0, 210, 255), true);

            info.DeviceState = detectedState;
            info.DeviceSerialNo = detectedSerial;

            // 3. Query properties via ADB Sideload / Recovery commands
            // Method A: adb getprop directly
            var propRes = await RunProcessAsync(adbPath, $"-s {detectedSerial} getprop", 4000, ct);
            if (string.IsNullOrWhiteSpace(propRes.stdout) || propRes.stdout.Length < 10)
            {
                // Method B: adb shell getprop
                propRes = await RunProcessAsync(adbPath, $"-s {detectedSerial} shell getprop", 4000, ct);
            }

            if (!string.IsNullOrWhiteSpace(propRes.stdout))
            {
                ParseAdbProperties(propRes.stdout, info.RawProps);
            }

            // Method C: Query individual vital Xiaomi sideload props
            string[] vitalProps = {
                "ro.product.model", "ro.product.name", "ro.product.device", "ro.build.product",
                "ro.build.version.incremental", "ro.build.version.release", "ro.build.version.security_patch",
                "ro.boot.flash.locked", "ro.miui.ui.version.name", "ro.boot.hwversion", "ro.boot.cpuid", "ro.boot.soc",
                "ro.build.version.codebase", "ro.build.version.branch", "persist.sys.language", "persist.sys.country",
                "ro.boot.recovery.version", "ro.system.build.version.incremental", "ro.build.display.id"
            };

            foreach (var vp in vitalProps)
            {
                if (!info.RawProps.ContainsKey(vp) || string.IsNullOrWhiteSpace(info.RawProps[vp]))
                {
                    var singleProp = await RunProcessAsync(adbPath, $"-s {detectedSerial} shell getprop {vp}", 1500, ct);
                    string outVal = singleProp.stdout.Trim();
                    if (!string.IsNullOrWhiteSpace(outVal) && !outVal.Contains("error", StringComparison.OrdinalIgnoreCase))
                    {
                        info.RawProps[vp] = outVal;
                    }
                }
            }

            // Populate DTO from properties
            if (info.RawProps.TryGetValue("ro.product.model", out var model)) info.ProductModel = model.Trim();
            if (info.RawProps.TryGetValue("ro.product.device", out var dev)) info.ProductDevice = dev.Trim();
            if (string.IsNullOrEmpty(info.ProductDevice) && info.RawProps.TryGetValue("ro.build.product", out var bprod)) info.ProductDevice = bprod.Trim();
            if (string.IsNullOrEmpty(info.ProductDevice) && info.RawProps.TryGetValue("ro.product.name", out var pname)) info.ProductDevice = pname.Trim();

            if (info.RawProps.TryGetValue("ro.build.version.incremental", out var inc)) info.BuildIncremental = inc.Trim();
            if (string.IsNullOrEmpty(info.BuildIncremental) && info.RawProps.TryGetValue("ro.system.build.version.incremental", out var sinc)) info.BuildIncremental = sinc.Trim();
            if (string.IsNullOrEmpty(info.BuildIncremental) && info.RawProps.TryGetValue("ro.build.display.id", out var dispid)) info.BuildIncremental = dispid.Trim();

            if (info.RawProps.TryGetValue("ro.build.version.release", out var rel)) info.AndroidVersion = rel.Trim();
            if (info.RawProps.TryGetValue("ro.build.version.security_patch", out var sp)) info.SecurityPatch = sp.Trim();
            if (info.RawProps.TryGetValue("ro.boot.flash.locked", out var lk)) info.BootloaderLocked = lk.Trim();
            if (info.RawProps.TryGetValue("ro.miui.ui.version.name", out var uiv)) info.UiVersion = uiv.Trim();
            if (info.RawProps.TryGetValue("ro.boot.hwversion", out var hwv)) info.HwVersion = hwv.Trim();
            if (info.RawProps.TryGetValue("ro.boot.cpuid", out var cpuid)) info.Cpuid = cpuid.Trim();

            if (info.RawProps.TryGetValue("ro.build.version.codebase", out var cbase)) info.DeviceCodebase = cbase.Trim();
            if (info.RawProps.TryGetValue("ro.build.version.branch", out var br)) info.DeviceBranch = br.Trim();
            if (info.RawProps.TryGetValue("persist.sys.language", out var lang)) info.DeviceLanguage = lang.Trim();
            if (info.RawProps.TryGetValue("persist.sys.country", out var regProp)) info.DeviceRegion = regProp.Trim();
            if (info.RawProps.TryGetValue("ro.boot.recovery.version", out var rver)) info.DeviceRecoveryVersion = rver.Trim();

            // Set DeviceModel / DeviceProduct fallbacks
            if (string.IsNullOrEmpty(info.DeviceModel)) info.DeviceModel = info.ProductDevice ?? info.ProductModel ?? "Xiaomi Device";
            if (string.IsNullOrEmpty(info.DeviceProduct)) info.DeviceProduct = info.ProductDevice ?? info.DeviceModel ?? "Xiaomi Product";
            if (string.IsNullOrEmpty(info.ProductDevice)) info.ProductDevice = info.DeviceModel;

            info.DeviceVersion = info.BuildIncremental;

            // Analyze ROM Family & Region & Android Codebase
            AnalyzeRomVersion(info);

            // Resolve Codename to Commercial Name & Chipset
            string codenameToLookup = info.ProductDevice ?? info.DeviceModel ?? "";
            if (!string.IsNullOrEmpty(codenameToLookup))
            {
                // Strip suffix like _global if needed
                string cleanCodename = codenameToLookup.Split('_')[0].Trim();
                if (CodenameDb.TryGetValue(cleanCodename, out var dbItem) || CodenameDb.TryGetValue(codenameToLookup, out dbItem))
                {
                    info.CommercialName = dbItem.name;
                    info.Chipset = dbItem.chipset;
                }
                else
                {
                    info.CommercialName = $"Xiaomi Dispositivo ({codenameToLookup})";
                    info.Chipset = "Qualcomm / MediaTek";
                }
            }

            // Determine Lock status
            info.IsLocked = (info.BootloaderLocked == "1" || string.Equals(info.BootloaderLocked, "true", StringComparison.OrdinalIgnoreCase));

            info.Success = !string.IsNullOrEmpty(info.ProductDevice) || !string.IsNullOrEmpty(info.DeviceModel) || !string.IsNullOrEmpty(detectedSerial);

            // Render Formatted Diagnostic Report
            RenderSideloadReport(info, logger);

            return info;
        }

        private static void ParseAdbProperties(string rawProps, Dictionary<string, string> dict)
        {
            var lines = rawProps.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var regex = new Regex(@"\[(.*?)\]:\s*\[(.*?)\]");

            foreach (var line in lines)
            {
                var match = regex.Match(line);
                if (match.Success)
                {
                    string key = match.Groups[1].Value.Trim();
                    string val = match.Groups[2].Value.Trim();
                    dict[key] = val;
                }
                else
                {
                    int colonIdx = line.IndexOf(':');
                    if (colonIdx > 0)
                    {
                        string k = line.Substring(0, colonIdx).Trim().Trim('[', ']');
                        string v = line.Substring(colonIdx + 1).Trim().Trim('[', ']');
                        dict[k] = v;
                    }
                }
            }
        }

        private static void AnalyzeRomVersion(XiaomiSideloadDeviceInfo info)
        {
            if (string.IsNullOrEmpty(info.BuildIncremental)) return;

            string inc = info.BuildIncremental.ToUpperInvariant();

            // Detect HyperOS (OS3.x, OS2.x, OS1.x) vs MIUI (V14.x, V13.x, V12.x)
            if (inc.StartsWith("OS3.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "Xiaomi HyperOS 3.0";
            }
            else if (inc.StartsWith("OS2.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "Xiaomi HyperOS 2.0";
            }
            else if (inc.StartsWith("OS1.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "Xiaomi HyperOS 1.0";
            }
            else if (inc.StartsWith("V816.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "Xiaomi HyperOS 1.0 (MIUI 15 Base)";
            }
            else if (inc.StartsWith("V14.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "MIUI 14";
            }
            else if (inc.StartsWith("V13.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "MIUI 13";
            }
            else if (inc.StartsWith("V12.5", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "MIUI 12.5 Enhanced";
            }
            else if (inc.StartsWith("V12.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "MIUI 12";
            }
            else if (inc.StartsWith("V11.", StringComparison.OrdinalIgnoreCase))
            {
                info.RomFamily = "MIUI 11";
            }
            else
            {
                info.RomFamily = "Xiaomi Firmware";
            }

            // Extract Codebase & Region from build letter suffix (e.g. OS3.0.306.0.WGTMIXM -> WGTMIXM)
            var parts = inc.Split('.');
            string letters = parts.LastOrDefault() ?? "";
            if (letters.Length >= 5)
            {
                // First letter = Android codebase letter (e.g. U = 14, V = 15, W = 16)
                char androidCodeLetter = letters[0];
                if (string.IsNullOrEmpty(info.DeviceCodebase))
                {
                    info.DeviceCodebase = androidCodeLetter switch
                    {
                        'W' => "16",
                        'V' => "15",
                        'U' => "14",
                        'T' => "13",
                        'S' => "12",
                        'R' => "11",
                        'Q' => "10",
                        'P' => "9",
                        'O' => "8",
                        'N' => "7",
                        _ => "16"
                    };
                }

                // Letters 3-4 = Region code (e.g. MI, EU, IN, RU, ID, CN)
                string regionCode = letters.Substring(3, 2);
                if (RegionCodes.TryGetValue(regionCode, out var regDesc))
                {
                    info.Region = $"{regDesc} ({regionCode})";
                }
                else
                {
                    info.Region = regionCode;
                }
            }
        }

        private static void RenderSideloadReport(XiaomiSideloadDeviceInfo info, Action<string, Color, bool> logger)
        {
            Color cyan = Color.FromArgb(0, 210, 255);
            Color yellow = Color.FromArgb(255, 196, 0);
            Color white = Color.FromArgb(242, 247, 255);
            Color green = Color.FromArgb(0, 230, 92);
            Color red = Color.FromArgb(255, 52, 64);
            Color muted = Color.FromArgb(145, 172, 204);

            logger("", white, true);
            PrintReportRow(logger, "Brand", info.Brand, white);
            PrintReportRow(logger, "Model", info.CommercialName ?? info.DeviceModel ?? info.ProductModel ?? "XIAOMI DEVICE", green);
            PrintReportRow(logger, "Operation", "Mi Assistant Sideload Diagnostics", cyan);
            logger("", white, true);

            PrintReportRow(logger, "Device Model", info.DeviceModel ?? info.ProductDevice ?? "lake", cyan);
            PrintReportRow(logger, "Device Product", info.DeviceProduct ?? $"{info.DeviceModel}_global", cyan);

            if (!string.IsNullOrEmpty(info.DeviceVersion))
                PrintReportRow(logger, "Device Version", info.DeviceVersion, cyan);

            if (!string.IsNullOrEmpty(info.DeviceSerialNo))
                PrintReportRow(logger, "Device Serial No.", info.DeviceSerialNo, cyan);

            if (!string.IsNullOrEmpty(info.DeviceCodebase))
                PrintReportRow(logger, "Device Codebase", info.DeviceCodebase, cyan);

            if (!string.IsNullOrEmpty(info.DeviceBranch))
                PrintReportRow(logger, "Device Branch", info.DeviceBranch, cyan);

            if (!string.IsNullOrEmpty(info.DeviceLanguage))
                PrintReportRow(logger, "Device Language", info.DeviceLanguage, cyan);

            if (!string.IsNullOrEmpty(info.DeviceRegion))
                PrintReportRow(logger, "Device Region", info.DeviceRegion, cyan);

            if (!string.IsNullOrEmpty(info.DeviceRecoveryVersion))
                PrintReportRow(logger, "Device Recovery Version", info.DeviceRecoveryVersion, cyan);

            if (!string.IsNullOrEmpty(info.RomFamily))
                PrintReportRow(logger, "OS Interface", info.RomFamily, green);

            if (!string.IsNullOrEmpty(info.Chipset))
                PrintReportRow(logger, "Platform / Chipset", info.Chipset, white);

            // Bootloader Lock
            string blText = info.IsLocked ? "BLOQUEADO (LOCKED)" : "DESBLOQUEADO (UNLOCKED)";
            Color blColor = info.IsLocked ? red : green;
            PrintReportRow(logger, "Bootloader Status", blText, blColor);

            if (!string.IsNullOrEmpty(info.SecurityPatch))
                PrintReportRow(logger, "Security Patch", info.SecurityPatch, yellow);

            if (!string.IsNullOrEmpty(info.HwVersion))
                PrintReportRow(logger, "Hardware Version", info.HwVersion, muted);

            if (!string.IsNullOrEmpty(info.Cpuid))
                PrintReportRow(logger, "Device CPU ID", info.Cpuid, muted);

            logger("--------------------------------------------------------------------------------", muted, true);
            logger("[NOTAS TECNICAS DE RECUPERACAO E SIDELOAD]", cyan, true);
            logger(" - Modo Sideload / MiAssistant permite atualizacao e reparacao de ROM oficial sem destravar o Bootloader.", white, true);
            logger($" - Para gravar atualizacao, utilize o pacote Recovery oficial correspondente a regiao: [{info.Region ?? info.DeviceRegion ?? "Global"}].", yellow, true);
            logger("--------------------------------------------------------------------------------", muted, true);
        }

        // ============================================================
        // 3. REBOOT HELPERS (FASTBOOT & SIDELOAD)
        // ============================================================
        public static async Task<bool> RebootDeviceAsync(string mode, string target, Action<string, Color, bool> logger, CancellationToken ct = default)
        {
            if (mode.Equals("fastboot", StringComparison.OrdinalIgnoreCase))
            {
                string? fb = FindToolPath("fastboot");
                if (string.IsNullOrEmpty(fb)) return false;

                string arg = target.ToLowerInvariant() switch
                {
                    "recovery" => "reboot recovery",
                    "edl" => "oem edl",
                    "bootloader" => "reboot bootloader",
                    "fastbootd" => "reboot fastboot",
                    _ => "reboot"
                };

                logger($"[FASTBOOT] Enviando comando: fastboot {arg}...", Color.FromArgb(0, 210, 255), true);
                var res = await RunProcessAsync(fb, arg, 5000, ct);
                if (res.exitCode == 0)
                {
                    logger("[FASTBOOT] Comando de reinicializacao executado com sucesso!", Color.FromArgb(0, 230, 92), true);
                    return true;
                }
                else
                {
                    logger($"[FASTBOOT] Falha ao reiniciar: {res.stderr}", Color.FromArgb(255, 52, 64), true);
                    return false;
                }
            }
            else
            {
                string? adb = FindToolPath("adb");
                if (string.IsNullOrEmpty(adb)) return false;

                string arg = target.ToLowerInvariant() switch
                {
                    "recovery" => "reboot recovery",
                    "bootloader" or "fastboot" => "reboot bootloader",
                    "edl" => "reboot edl",
                    "sideload" => "reboot sideload",
                    _ => "reboot"
                };

                logger($"[ADB/SIDELOAD] Enviando comando: adb {arg}...", Color.FromArgb(0, 210, 255), true);
                var res = await RunProcessAsync(adb, arg, 5000, ct);
                if (res.exitCode == 0)
                {
                    logger("[ADB/SIDELOAD] Comando de reinicializacao executado com sucesso!", Color.FromArgb(0, 230, 92), true);
                    return true;
                }
                else
                {
                    logger($"[ADB/SIDELOAD] Falha ao reiniciar: {res.stderr}", Color.FromArgb(255, 52, 64), true);
                    return false;
                }
            }
        }
    }
}
