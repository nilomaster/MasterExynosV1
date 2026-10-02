using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace MasterUnlock
{
    public class SamsungMtpDeviceInfo
    {
        public bool Success { get; set; } = false;
        public string? PortDescription { get; set; }
        public string? PortName { get; set; }
        public string? ModelNumber { get; set; }
        public string? CommercialName { get; set; }
        public string? Platform { get; set; } // Exynos, MediaTek, Qualcomm, UNISOC
        public string? Chipset { get; set; }
        public string? Csc { get; set; }
        public string? ApVersion { get; set; }
        public string? BlVersion { get; set; }
        public string? CpVersion { get; set; }
        public string? CscVersion { get; set; }
        public string? Bit { get; set; }
        public string? SecurityPatchLevel { get; set; }
        public string? Imei { get; set; }
        public string? Imei2 { get; set; }
        public string? SerialNumber { get; set; }
        public string? LockStatus { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Country { get; set; }
        public string? Mcc { get; set; }
        public string? Mnc { get; set; }
        public string? UsbMode { get; set; }
        public string? UniqueNumber { get; set; }
        public string? AndroidVersion { get; set; }
        public string? FrpStatus { get; set; }
        public string? RecommendedPreset { get; set; }
        public string? RawDevConInfo { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public static class SamsungMtpReader
    {
        // SetupAPI P/Invoke Constants
        private static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        private const uint DIGCF_PRESENT = 0x00000002;
        private const uint DIGCF_ALLCLASSES = 0x00000004;
        private const uint DICS_FLAG_GLOBAL = 0x00000001;
        private const uint DIREG_DEV = 0x00000001;
        private const uint KEY_READ = 0x20019;
        private const uint SPDRP_FRIENDLYNAME = 0x0000000C;
        private const uint SPDRP_DEVICEDESC = 0x00000000;

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
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, [Out] char[] DeviceInstanceId, uint DeviceInstanceIdSize, out uint RequiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern IntPtr SetupDiOpenDevRegKey(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Scope, uint HwProfile, uint KeyType, uint samDesired);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property, out uint PropertyRegDataType, [Out] char[] PropertyBuffer, uint PropertyBufferSize, out uint RequiredSize);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int RegQueryValueEx(IntPtr hKey, string lpValueName, IntPtr lpReserved, ref uint lpType, [Out] char[] lpData, ref uint lpcbData);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern int RegCloseKey(IntPtr hKey);

        // CSC Country and MCC/MNC Mapping Table
        private static readonly Dictionary<string, (string country, string mcc, string mnc)> CscMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Brazil
            { "ZTO", ("BR", "724,", "05,") },
            { "ZTM", ("BR", "724,", "02,") },
            { "ZTA", ("BR", "724,", "06,") },
            { "ZVV", ("BR", "724,", "04,") },
            { "ZTR", ("BR", "724,", "31,") },

            // Latin America
            { "ARO", ("AR", "722,", "310,") },
            { "CTI", ("AR", "722,", "320,") },
            { "UFN", ("AR", "722,", "070,") },
            { "PSN", ("AR", "722,", "010,") },
            { "CHO", ("CL", "730,", "01,") },
            { "CHE", ("CL", "730,", "02,") },
            { "CHL", ("CL", "730,", "03,") },
            { "CHT", ("CL", "730,", "07,") },
            { "COO", ("CO", "732,", "101,") },
            { "COM", ("CO", "732,", "103,") },
            { "COL", ("CO", "732,", "123,") },
            { "COB", ("CO", "732,", "111,") },
            { "MXO", ("MX", "334,", "020,") },
            { "TCE", ("MX", "334,", "020,") },
            { "IUS", ("MX", "334,", "050,") },
            { "MNX", ("MX", "334,", "090,") },
            { "PEO", ("PE", "716,", "10,") },
            { "PET", ("PE", "716,", "06,") },
            { "SAM", ("PE", "716,", "17,") },
            { "PNT", ("PE", "716,", "15,") },
            { "UPO", ("UY", "748,", "01,") },
            { "ANC", ("UY", "748,", "00,") },
            { "CTU", ("UY", "748,", "10,") },
            { "CTP", ("PY", "744,", "02,") },
            { "PSP", ("PY", "744,", "01,") },
            { "TGP", ("PY", "744,", "04,") },
            { "TPA", ("PA", "714,", "01,") },
            { "PBS", ("PA", "714,", "02,") },
            { "PCW", ("PA", "714,", "03,") },
            { "BVO", ("BO", "736,", "01,") },
            { "BVG", ("BO", "736,", "02,") },
            { "GTO", ("GT", "704,", "01,") },
            { "PGU", ("GT", "704,", "02,") },
            { "TGU", ("GT", "704,", "03,") },
            { "EON", ("EC", "740,", "01,") },
            { "ECO", ("EC", "740,", "00,") },
            { "ALE", ("EC", "740,", "02,") },
            { "ICE", ("CR", "712,", "01,") },
            { "CRM", ("CR", "712,", "02,") },
            { "PCT", ("PR", "330,", "110,") },
            { "CDR", ("DO", "370,", "02,") },
            { "DOR", ("DO", "370,", "01,") },
            { "DOO", ("DO", "370,", "03,") },

            // North America
            { "XAA", ("US", "310,", "410,") },
            { "TMB", ("US", "310,", "260,") },
            { "ATT", ("US", "310,", "410,") },
            { "VZW", ("US", "311,", "480,") },
            { "SPR", ("US", "310,", "120,") },
            { "TMK", ("US", "310,", "260,") },
            { "CCT", ("US", "311,", "480,") },
            { "CHA", ("US", "311,", "480,") },
            { "USC", ("US", "311,", "580,") },
            { "XAC", ("CA", "302,", "720,") },
            { "TLS", ("CA", "302,", "220,") },
            { "RWC", ("CA", "302,", "720,") },
            { "BMC", ("CA", "302,", "610,") },

            // Europe
            { "DBT", ("DE", "262,", "01,") },
            { "DTM", ("DE", "262,", "01,") },
            { "VD2", ("DE", "262,", "02,") },
            { "BTU", ("GB", "234,", "15,") },
            { "EVR", ("GB", "234,", "30,") },
            { "O2U", ("GB", "234,", "10,") },
            { "VOD", ("GB", "234,", "15,") },
            { "XEF", ("FR", "208,", "01,") },
            { "FTM", ("FR", "208,", "01,") },
            { "SFR", ("FR", "208,", "10,") },
            { "ITV", ("IT", "222,", "01,") },
            { "TIM", ("IT", "222,", "01,") },
            { "OMN", ("IT", "222,", "10,") },
            { "PHE", ("ES", "214,", "01,") },
            { "ATL", ("ES", "214,", "01,") },
            { "XEC", ("ES", "214,", "06,") },
            { "SER", ("RU", "250,", "01,") },
            { "CAC", ("RU", "250,", "02,") },
            { "XEO", ("PL", "260,", "01,") },
            { "PLS", ("PL", "260,", "01,") },
            { "PRT", ("PL", "260,", "02,") },
            { "PHN", ("NL", "204,", "08,") },
            { "TPH", ("PT", "268,", "01,") },

            // Asia & Oceania
            { "INS", ("IN", "404,", "45,") },
            { "INU", ("IN", "404,", "20,") },
            { "KTC", ("KR", "450,", "08,") },
            { "SKC", ("KR", "450,", "05,") },
            { "LUC", ("KR", "450,", "06,") },
            { "KOO", ("KR", "450,", "05,") },
            { "XXV", ("VN", "452,", "04,") },
            { "XID", ("ID", "510,", "11,") },
            { "THL", ("TH", "520,", "01,") },
            { "XME", ("MY", "502,", "12,") },
            { "SMA", ("PH", "515,", "03,") },
            { "GLB", ("PH", "515,", "02,") },
            { "PAK", ("PK", "410,", "01,") },
            { "XSG", ("AE", "424,", "02,") },
            { "KSA", ("SA", "420,", "01,") },
            { "MID", ("IQ", "418,", "05,") },
            { "TUR", ("TR", "286,", "01,") },
            { "EGY", ("EG", "602,", "01,") },
            { "TEL", ("AU", "505,", "01,") },
            { "OPS", ("AU", "505,", "02,") },
            { "VAU", ("AU", "505,", "03,") },

            // Multi-CSC Collections
            { "OWO", ("BR", "724,", "05,") },
            { "OXM", ("GB", "234,", "15,") },
            { "OWA", ("AR", "722,", "310,") },
            { "OJM", ("AE", "424,", "02,") },
            { "OLM", ("TH", "520,", "01,") },
            { "OXX", ("PL", "260,", "01,") },
        };

        // Complete Samsung Model Database (Exynos, MediaTek, Qualcomm, UNISOC)
        private static readonly Dictionary<string, (string platform, string chipset, string preset, string galaxyName)> ModelMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // ================================================================
            // MEDIATEK (MTK) DEVICES
            // ================================================================
            { "SM-A075M", ("MediaTek (MTK)", "MediaTek Helio G85 (MT6769)", "", "Galaxy A07") },
            { "SM-A075F", ("MediaTek (MTK)", "MediaTek Helio G85 (MT6769)", "", "Galaxy A07") },
            { "SM-A076M", ("MediaTek (MTK)", "MediaTek Dimensity 6100+ (MT6835)", "", "Galaxy A07 5G") },
            { "SM-A076B", ("MediaTek (MTK)", "MediaTek Dimensity 6100+ (MT6835)", "", "Galaxy A07 5G") },
            { "SM-A065M", ("MediaTek (MTK)", "MediaTek Helio G85 (MT6769V)", "", "Galaxy A06") },
            { "SM-A065F", ("MediaTek (MTK)", "MediaTek Helio G85 (MT6769V)", "", "Galaxy A06") },
            { "SM-A055M", ("MediaTek (MTK)", "MediaTek Helio G85 (MT6769V)", "", "Galaxy A05") },
            { "SM-A055F", ("MediaTek (MTK)", "MediaTek Helio G85 (MT6769V)", "", "Galaxy A05") },
            { "SM-A045M", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A04") },
            { "SM-A045F", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A04") },
            { "SM-A042M", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A04e") },
            { "SM-A042F", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A04e") },
            { "SM-A037M", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A03s") },
            { "SM-A037F", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A03s") },
            { "SM-A037U", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A03s") },
            { "SM-A022M", ("MediaTek (MTK)", "MediaTek MT6739WW", "", "Galaxy A02") },
            { "SM-A022F", ("MediaTek (MTK)", "MediaTek MT6739WW", "", "Galaxy A02") },
            { "SM-A013M", ("MediaTek (MTK)", "MediaTek MT6739", "", "Galaxy A01 Core") },
            { "SM-A013F", ("MediaTek (MTK)", "MediaTek MT6739", "", "Galaxy A01 Core") },
            { "SM-A107M", ("MediaTek (MTK)", "MediaTek Helio P22 (MT6762)", "", "Galaxy A10s") },
            { "SM-A107F", ("MediaTek (MTK)", "MediaTek Helio P22 (MT6762)", "", "Galaxy A10s") },
            { "SM-A125M", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A12") },
            { "SM-A125F", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A12") },
            { "SM-A125U", ("MediaTek (MTK)", "MediaTek Helio P35 (MT6765)", "", "Galaxy A12") },
            { "SM-A136B", ("MediaTek (MTK)", "MediaTek Dimensity 700 (MT6833)", "", "Galaxy A13 5G") },
            { "SM-A136U", ("MediaTek (MTK)", "MediaTek Dimensity 700 (MT6833)", "", "Galaxy A13 5G") },
            { "SM-A145M", ("MediaTek (MTK)", "MediaTek Helio G80 (MT6769V)", "", "Galaxy A14 4G") },
            { "SM-A145F", ("MediaTek (MTK)", "MediaTek Helio G80 (MT6769V)", "", "Galaxy A14 4G") },
            { "SM-A146P", ("MediaTek (MTK)", "MediaTek Dimensity 700 (MT6833)", "", "Galaxy A14 5G (MTK)") },
            { "SM-A146U", ("MediaTek (MTK)", "MediaTek Dimensity 700 (MT6833)", "", "Galaxy A14 5G (MTK)") },
            { "SM-A155M", ("MediaTek (MTK)", "MediaTek Helio G99 (MT6789)", "", "Galaxy A15 4G") },
            { "SM-A155F", ("MediaTek (MTK)", "MediaTek Helio G99 (MT6789)", "", "Galaxy A15 4G") },
            { "SM-A156M", ("MediaTek (MTK)", "MediaTek Dimensity 6100+ (MT6835)", "", "Galaxy A15 5G") },
            { "SM-A156B", ("MediaTek (MTK)", "MediaTek Dimensity 6100+ (MT6835)", "", "Galaxy A15 5G") },
            { "SM-A156E", ("MediaTek (MTK)", "MediaTek Dimensity 6100+ (MT6835)", "", "Galaxy A15 5G") },
            { "SM-A165F", ("MediaTek (MTK)", "MediaTek Helio G99", "", "Galaxy A16 4G") },
            { "SM-A165M", ("MediaTek (MTK)", "MediaTek Helio G99", "", "Galaxy A16 4G") },
            { "SM-A225M", ("MediaTek (MTK)", "MediaTek Helio G80 (MT6769V)", "", "Galaxy A22") },
            { "SM-A225F", ("MediaTek (MTK)", "MediaTek Helio G80 (MT6769V)", "", "Galaxy A22") },
            { "SM-A226B", ("MediaTek (MTK)", "MediaTek Dimensity 700 (MT6833)", "", "Galaxy A22 5G") },
            { "SM-A245M", ("MediaTek (MTK)", "MediaTek Helio G99 (MT6789)", "", "Galaxy A24") },
            { "SM-A245F", ("MediaTek (MTK)", "MediaTek Helio G99 (MT6789)", "", "Galaxy A24") },
            { "SM-A315G", ("MediaTek (MTK)", "MediaTek Helio P65 (MT6768)", "", "Galaxy A31") },
            { "SM-A315F", ("MediaTek (MTK)", "MediaTek Helio P65 (MT6768)", "", "Galaxy A31") },
            { "SM-A325M", ("MediaTek (MTK)", "MediaTek Helio G80 (MT6769V)", "", "Galaxy A32") },
            { "SM-A325F", ("MediaTek (MTK)", "MediaTek Helio G80 (MT6769V)", "", "Galaxy A32") },
            { "SM-A326B", ("MediaTek (MTK)", "MediaTek Dimensity 720 (MT6853V)", "", "Galaxy A32 5G") },
            { "SM-A346B", ("MediaTek (MTK)", "MediaTek Dimensity 1080 (MT6877V)", "", "Galaxy A34 5G") },
            { "SM-A346E", ("MediaTek (MTK)", "MediaTek Dimensity 1080 (MT6877V)", "", "Galaxy A34 5G") },
            { "SM-A346M", ("MediaTek (MTK)", "MediaTek Dimensity 1080 (MT6877V)", "", "Galaxy A34 5G") },
            { "SM-A415F", ("MediaTek (MTK)", "MediaTek Helio P65 (MT6768)", "", "Galaxy A41") },

            // ================================================================
            // UNISOC DEVICES
            // ================================================================
            { "SM-A032M", ("UNISOC (Spreadtrum)", "UNISOC SC9863A", "", "Galaxy A03 Core") },
            { "SM-A032F", ("UNISOC (Spreadtrum)", "UNISOC SC9863A", "", "Galaxy A03 Core") },
            { "SM-A035M", ("UNISOC (Spreadtrum)", "UNISOC T606", "", "Galaxy A03") },
            { "SM-A035F", ("UNISOC (Spreadtrum)", "UNISOC T606", "", "Galaxy A03") },
            { "SM-X200",  ("UNISOC (Spreadtrum)", "UNISOC T618", "", "Galaxy Tab A8 10.5") },
            { "SM-X205",  ("UNISOC (Spreadtrum)", "UNISOC T618", "", "Galaxy Tab A8 10.5") },

            // ================================================================
            // QUALCOMM SNAPDRAGON DEVICES
            // ================================================================
            { "SM-A015M", ("Qualcomm Snapdragon", "Snapdragon 439 (SDM439)", "", "Galaxy A01") },
            { "SM-A015F", ("Qualcomm Snapdragon", "Snapdragon 439 (SDM439)", "", "Galaxy A01") },
            { "SM-A025F", ("Qualcomm Snapdragon", "Snapdragon 450 (SDM450)", "", "Galaxy A02s") },
            { "SM-A025M", ("Qualcomm Snapdragon", "Snapdragon 450 (SDM450)", "", "Galaxy A02s") },
            { "SM-A057F", ("Qualcomm Snapdragon", "Snapdragon 680 (SM6225)", "", "Galaxy A05s") },
            { "SM-A057M", ("Qualcomm Snapdragon", "Snapdragon 680 (SM6225)", "", "Galaxy A05s") },
            { "SM-A115M", ("Qualcomm Snapdragon", "Snapdragon 450 (SDM450)", "", "Galaxy A11") },
            { "SM-A115F", ("Qualcomm Snapdragon", "Snapdragon 450 (SDM450)", "", "Galaxy A11") },
            { "SM-A207M", ("Qualcomm Snapdragon", "Snapdragon 450 (SDM450)", "", "Galaxy A20s") },
            { "SM-A207F", ("Qualcomm Snapdragon", "Snapdragon 450 (SDM450)", "", "Galaxy A20s") },
            { "SM-A235M", ("Qualcomm Snapdragon", "Snapdragon 680 (SM6225)", "", "Galaxy A23 4G") },
            { "SM-A235F", ("Qualcomm Snapdragon", "Snapdragon 680 (SM6225)", "", "Galaxy A23 4G") },
            { "SM-A236M", ("Qualcomm Snapdragon", "Snapdragon 695 (SM6375)", "", "Galaxy A23 5G") },
            { "SM-A236B", ("Qualcomm Snapdragon", "Snapdragon 695 (SM6375)", "", "Galaxy A23 5G") },
            { "SM-A426B", ("Qualcomm Snapdragon", "Snapdragon 750G (SM7225)", "", "Galaxy A42 5G") },
            { "SM-A525M", ("Qualcomm Snapdragon", "Snapdragon 720G (SM7125)", "", "Galaxy A52") },
            { "SM-A525F", ("Qualcomm Snapdragon", "Snapdragon 720G (SM7125)", "", "Galaxy A52") },
            { "SM-A526B", ("Qualcomm Snapdragon", "Snapdragon 750G (SM7225)", "", "Galaxy A52 5G") },
            { "SM-A528B", ("Qualcomm Snapdragon", "Snapdragon 778G (SM7325)", "", "Galaxy A52s 5G") },
            { "SM-A705F", ("Qualcomm Snapdragon", "Snapdragon 675 (SM6150)", "", "Galaxy A70") },
            { "SM-A705MN",("Qualcomm Snapdragon", "Snapdragon 675 (SM6150)", "", "Galaxy A70") },
            { "SM-A715F", ("Qualcomm Snapdragon", "Snapdragon 730 (SM7150)", "", "Galaxy A71") },
            { "SM-A725M", ("Qualcomm Snapdragon", "Snapdragon 720G (SM7125)", "", "Galaxy A72") },
            { "SM-A725F", ("Qualcomm Snapdragon", "Snapdragon 720G (SM7125)", "", "Galaxy A72") },
            { "SM-A736B", ("Qualcomm Snapdragon", "Snapdragon 778G (SM7325)", "", "Galaxy A73 5G") },
            { "SM-G780G", ("Qualcomm Snapdragon", "Snapdragon 865 (SM8250)", "", "Galaxy S20 FE") },
            { "SM-G781B", ("Qualcomm Snapdragon", "Snapdragon 865 (SM8250)", "", "Galaxy S20 FE 5G") },
            { "SM-G990B", ("Qualcomm Snapdragon", "Snapdragon 888 (SM8350)", "", "Galaxy S21 FE 5G") },
            { "SM-S901E", ("Qualcomm Snapdragon", "Snapdragon 8 Gen 1 (SM8450)", "", "Galaxy S22 5G") },
            { "SM-S906E", ("Qualcomm Snapdragon", "Snapdragon 8 Gen 1 (SM8450)", "", "Galaxy S22+ 5G") },
            { "SM-S908E", ("Qualcomm Snapdragon", "Snapdragon 8 Gen 1 (SM8450)", "", "Galaxy S22 Ultra 5G") },
            { "SM-S911B", ("Qualcomm Snapdragon", "Snapdragon 8 Gen 2 (SM8550)", "", "Galaxy S23 5G") },
            { "SM-S916B", ("Qualcomm Snapdragon", "Snapdragon 8 Gen 2 (SM8550)", "", "Galaxy S23+ 5G") },
            { "SM-S918B", ("Qualcomm Snapdragon", "Snapdragon 8 Gen 2 (SM8550)", "", "Galaxy S23 Ultra 5G") },
            { "SM-S928B", ("Qualcomm Snapdragon", "Snapdragon 8 Gen 3 (SM8650)", "", "Galaxy S24 Ultra 5G") },

            // ================================================================
            // SAMSUNG EXYNOS DEVICES
            // ================================================================
            // Exynos 850
            { "SM-A127F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A12 (2021)") },
            { "SM-A127M", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A12 (2021)") },
            { "SM-A135F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A13") },
            { "SM-A135M", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A13") },
            { "SM-A047F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A04s") },
            { "SM-A047M", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A04s") },
            { "SM-A217F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A21s") },
            { "SM-A217M", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy A21s") },
            { "SM-M127F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy M12") },
            { "SM-M127G", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy M12") },
            { "SM-E135F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy F13") },
            { "SM-E225F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy F22") },
            { "SM-M225F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy M22") },
            { "SM-G525F", ("Samsung Exynos", "Exynos 850 (S5E3830)", "exynos850_dpolicy_extract.json", "Galaxy XCover 5") },

            // Exynos 1280
            { "SM-A256B", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy A25 5G") },
            { "SM-A256E", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy A25 5G") },
            { "SM-A336B", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy A33 5G") },
            { "SM-A336M", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy A33 5G") },
            { "SM-A536B", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy A53 5G") },
            { "SM-A536E", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy A53 5G") },
            { "SM-A536U", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy A53 5G") },
            { "SM-M336B", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy M33 5G") },
            { "SM-M336K", ("Samsung Exynos", "Exynos 1280 (S5E8825)", "exynos1280_dpolicy_extract.json", "Galaxy M33 5G") },

            // Exynos 1330
            { "SM-A146B", ("Samsung Exynos", "Exynos 1330 (S5E8535)", "exynos1330_dpolicy_extract.json", "Galaxy A14 5G") },
            { "SM-A146M", ("Samsung Exynos", "Exynos 1330 (S5E8535)", "exynos1330_dpolicy_extract.json", "Galaxy A14 5G") },
            { "SM-A166B", ("Samsung Exynos", "Exynos 1330 (S5E8535)", "exynos1330_dpolicy_extract.json", "Galaxy A16 5G") },
            { "SM-A166E", ("Samsung Exynos", "Exynos 1330 (S5E8535)", "exynos1330_dpolicy_extract.json", "Galaxy A16 5G") },
            { "SM-A166M", ("Samsung Exynos", "Exynos 1330 (S5E8535)", "exynos1330_dpolicy_extract.json", "Galaxy A16 5G") },
            { "SM-M146B", ("Samsung Exynos", "Exynos 1330 (S5E8535)", "exynos1330_dpolicy_extract.json", "Galaxy M14 5G") },
            { "SM-F146B", ("Samsung Exynos", "Exynos 1330 (S5E8535)", "exynos1330_dpolicy_extract.json", "Galaxy F14 5G") },

            // Exynos 1380
            { "SM-A546B", ("Samsung Exynos", "Exynos 1380 (S5E8835)", "exynos1380_dpolicy_extract.json", "Galaxy A54 5G") },
            { "SM-A546E", ("Samsung Exynos", "Exynos 1380 (S5E8835)", "exynos1380_dpolicy_extract.json", "Galaxy A54 5G") },
            { "SM-A546U", ("Samsung Exynos", "Exynos 1380 (S5E8835)", "exynos1380_dpolicy_extract.json", "Galaxy A54 5G") },
            { "SM-M546B", ("Samsung Exynos", "Exynos 1380 (S5E8835)", "exynos1380_dpolicy_extract.json", "Galaxy M54 5G") },
            { "SM-F546B", ("Samsung Exynos", "Exynos 1380 (S5E8835)", "exynos1380_dpolicy_extract.json", "Galaxy F54 5G") },

            // Exynos 1480
            { "SM-A356B", ("Samsung Exynos", "Exynos 1480 (S5E8845)", "exynos1480_dpolicy_extract.json", "Galaxy A35 5G") },
            { "SM-A356E", ("Samsung Exynos", "Exynos 1480 (S5E8845)", "exynos1480_dpolicy_extract.json", "Galaxy A35 5G") },
            { "SM-A556B", ("Samsung Exynos", "Exynos 1480 (S5E8845)", "exynos1480_dpolicy_extract.json", "Galaxy A55 5G") },
            { "SM-A556E", ("Samsung Exynos", "Exynos 1480 (S5E8845)", "exynos1480_dpolicy_extract.json", "Galaxy A55 5G") },

            // Exynos 7884
            { "SM-A105F", ("Samsung Exynos", "Exynos 7884 (S5E7884)", "exynos7884_dpolicy_extract.json", "Galaxy A10") },
            { "SM-A105G", ("Samsung Exynos", "Exynos 7884 (S5E7884)", "exynos7884_dpolicy_extract.json", "Galaxy A10") },
            { "SM-A105M", ("Samsung Exynos", "Exynos 7884 (S5E7884)", "exynos7884_dpolicy_extract.json", "Galaxy A10") },
            { "SM-A202F", ("Samsung Exynos", "Exynos 7884 (S5E7884)", "exynos7884_dpolicy_extract.json", "Galaxy A20e") },
            { "SM-A205F", ("Samsung Exynos", "Exynos 7884 (S5E7884)", "exynos7884_dpolicy_extract.json", "Galaxy A20") },

            // Exynos 7885
            { "SM-A530F", ("Samsung Exynos", "Exynos 7885 (S5E7885)", "exynos7885_dpolicy_extract.json", "Galaxy A8 (2018)") },
            { "SM-A730F", ("Samsung Exynos", "Exynos 7885 (S5E7885)", "exynos7885_dpolicy_extract.json", "Galaxy A8+ (2018)") },
            { "SM-A750F", ("Samsung Exynos", "Exynos 7885 (S5E7885)", "exynos7885_dpolicy_extract.json", "Galaxy A7 (2018)") },
            { "SM-A750G", ("Samsung Exynos", "Exynos 7885 (S5E7885)", "exynos7885_dpolicy_extract.json", "Galaxy A7 (2018)") },

            // Exynos 9610 / 9611
            { "SM-A505F", ("Samsung Exynos", "Exynos 9610 (S5E9610)", "exynos9610_dpolicy_integrity.json", "Galaxy A50") },
            { "SM-A505G", ("Samsung Exynos", "Exynos 9610 (S5E9610)", "exynos9610_dpolicy_integrity.json", "Galaxy A50") },
            { "SM-A505FN",("Samsung Exynos", "Exynos 9610 (S5E9610)", "exynos9610_dpolicy_integrity.json", "Galaxy A50") },
            { "SM-A515F", ("Samsung Exynos", "Exynos 9611 (S5E9611)", "exynos9611_dpolicy_integrity.json", "Galaxy A51") },
            { "SM-M315F", ("Samsung Exynos", "Exynos 9611 (S5E9611)", "exynos9611_dpolicy_integrity.json", "Galaxy M31") },
            { "SM-M215F", ("Samsung Exynos", "Exynos 9611 (S5E9611)", "exynos9611_dpolicy_integrity.json", "Galaxy M21") },
            { "SM-F415F", ("Samsung Exynos", "Exynos 9611 (S5E9611)", "exynos9611_dpolicy_integrity.json", "Galaxy F41") },

            // Exynos 9820 / 9825
            { "SM-G970F", ("Samsung Exynos", "Exynos 9820 (S5E9820)", "exynos9820_dpolicy_integrity.json", "Galaxy S10e") },
            { "SM-G973F", ("Samsung Exynos", "Exynos 9820 (S5E9820)", "exynos9820_dpolicy_integrity.json", "Galaxy S10") },
            { "SM-G975F", ("Samsung Exynos", "Exynos 9820 (S5E9820)", "exynos9820_dpolicy_integrity.json", "Galaxy S10+") },
            { "SM-N970F", ("Samsung Exynos", "Exynos 9825 (S5E9825)", "exynos9825_dpolicy_integrity.json", "Galaxy Note 10") },
            { "SM-N975F", ("Samsung Exynos", "Exynos 9825 (S5E9825)", "exynos9825_dpolicy_integrity.json", "Galaxy Note 10+") },

            // Exynos 990
            { "SM-G980F", ("Samsung Exynos", "Exynos 990 (S5E9830)", "exynos990_dpolicy_integrity.json", "Galaxy S20") },
            { "SM-G981B", ("Samsung Exynos", "Exynos 990 (S5E9830)", "exynos990_dpolicy_integrity.json", "Galaxy S20 5G") },
            { "SM-G985F", ("Samsung Exynos", "Exynos 990 (S5E9830)", "exynos990_dpolicy_integrity.json", "Galaxy S20+") },
            { "SM-G986B", ("Samsung Exynos", "Exynos 990 (S5E9830)", "exynos990_dpolicy_integrity.json", "Galaxy S20+ 5G") },
            { "SM-G988B", ("Samsung Exynos", "Exynos 990 (S5E9830)", "exynos990_dpolicy_integrity.json", "Galaxy S20 Ultra 5G") },
            { "SM-N980F", ("Samsung Exynos", "Exynos 990 (S5E9830)", "exynos990_dpolicy_integrity.json", "Galaxy Note 20") },
            { "SM-N985F", ("Samsung Exynos", "Exynos 990 (S5E9830)", "exynos990_dpolicy_integrity.json", "Galaxy Note 20 Ultra") },

            // Exynos 2100
            { "SM-G991B", ("Samsung Exynos", "Exynos 2100 (S5E9840)", "exynos2100_dpolicy_extract.json", "Galaxy S21 5G") },
            { "SM-G996B", ("Samsung Exynos", "Exynos 2100 (S5E9840)", "exynos2100_dpolicy_extract.json", "Galaxy S21+ 5G") },
            { "SM-G998B", ("Samsung Exynos", "Exynos 2100 (S5E9840)", "exynos2100_dpolicy_extract.json", "Galaxy S21 Ultra 5G") },
            { "SM-G990E", ("Samsung Exynos", "Exynos 2100 (S5E9840)", "exynos2100_dpolicy_extract.json", "Galaxy S21 FE 5G") },

            // Exynos 2200
            { "SM-S901B", ("Samsung Exynos", "Exynos 2200 (S5E9925)", "exynos2200_dpolicy_extract.json", "Galaxy S22 5G") },
            { "SM-S906B", ("Samsung Exynos", "Exynos 2200 (S5E9925)", "exynos2200_dpolicy_extract.json", "Galaxy S22+ 5G") },
            { "SM-S908B", ("Samsung Exynos", "Exynos 2200 (S5E9925)", "exynos2200_dpolicy_extract.json", "Galaxy S22 Ultra 5G") },
            { "SM-S711B", ("Samsung Exynos", "Exynos 2200 (S5E9925)", "exynos2200_dpolicy_extract.json", "Galaxy S23 FE 5G") },

            // Exynos 2400
            { "SM-S921B", ("Samsung Exynos", "Exynos 2400 (S5E9945)", "exynos2400_dpolicy_extract.json", "Galaxy S24") },
            { "SM-S926B", ("Samsung Exynos", "Exynos 2400 (S5E9945)", "exynos2400_dpolicy_extract.json", "Galaxy S24+") },

            // Exynos 7570 / 7870 / 7880 / 7904
            { "SM-J330F", ("Samsung Exynos", "Exynos 7570 (S5E7570)", "exynos7570_dpolicy_integrity.json", "Galaxy J3 (2017)") },
            { "SM-J730F", ("Samsung Exynos", "Exynos 7870 (S5E7870)", "exynos7870_dpolicy_integrity.json", "Galaxy J7 (2017)") },
            { "SM-A520F", ("Samsung Exynos", "Exynos 7880 (S5E7880)", "exynos7880_dpolicy_integrity.json", "Galaxy A5 (2017)") },
            { "SM-A305F", ("Samsung Exynos", "Exynos 7904 (S5E7904)", "exynos7904_dpolicy_integrity.json", "Galaxy A30") },
            { "SM-A305G", ("Samsung Exynos", "Exynos 7904 (S5E7904)", "exynos7904_dpolicy_integrity.json", "Galaxy A30") },
            { "SM-A405FN",("Samsung Exynos", "Exynos 7904 (S5E7904)", "exynos7904_dpolicy_integrity.json", "Galaxy A40") },
            { "SM-M205F", ("Samsung Exynos", "Exynos 7904 (S5E7904)", "exynos7904_dpolicy_integrity.json", "Galaxy M20") },
            { "SM-M305F", ("Samsung Exynos", "Exynos 7904 (S5E7904)", "exynos7904_dpolicy_integrity.json", "Galaxy M30") },
        };

        // Main ReadInfo method
        public static async Task<SamsungMtpDeviceInfo> ReadDeviceInfoAsync(
            Action<string, Color, bool>? logCallback = null,
            CancellationToken ct = default)
        {
            var info = new SamsungMtpDeviceInfo();

            void Log(string msg, Color color, bool breakline = true)
            {
                logCallback?.Invoke(msg, color, breakline);
            }

            // Step 1: Detect Samsung USB devices via SetupAPI / WPD / Registry
            var usbDevices = DetectSamsungUsbDevices();
            if (usbDevices.Count == 0)
            {
                Log("Using port ... No device detected", Color.Orange, true);
                Log("Reading info mode MTP ... FAIL", Color.Red, true);
                info.ErrorMessage = "Device not connected or Samsung drivers not installed.";
                return info;
            }

            var primaryDevice = usbDevices[0];
            if (!string.IsNullOrEmpty(primaryDevice.serialNumber))
            {
                info.SerialNumber = primaryDevice.serialNumber;
            }

            // Step 2: Look for Samsung Modem COM port for AT commands
            string modemPort = FindSamsungModemPort(out string portDesc);
            info.PortName = modemPort;
            info.PortDescription = string.IsNullOrEmpty(portDesc)
                ? (string.IsNullOrEmpty(modemPort) ? "SAMSUNG Mobile USB Device" : $"SAMSUNG Mobile USB Modem ({modemPort})")
                : portDesc;

            if (!string.IsNullOrEmpty(modemPort))
            {
                Log($"Using port {info.PortDescription}", Color.FromArgb(100, 220, 100), true);
                Log("Reading info mode MTP ... OK", Color.LimeGreen, true);

                // Step 3: Query AT Commands on the Modem Port
                await QueryAtCommandsAsync(modemPort, info, ct);
            }
            else
            {
                Log($"Using port {info.PortDescription}", Color.Yellow, true);
                Log("Reading info mode MTP ... OK", Color.LimeGreen, true);
            }

            // Step 4: If AT commands did not yield model or serial, extract from device descriptors
            if (string.IsNullOrEmpty(info.ModelNumber) || string.IsNullOrEmpty(info.SerialNumber))
            {
                ExtractInfoFromDescriptors(usbDevices, info);
            }

            // Step 5: Fill missing metadata from ModelMap & CSC tables
            ResolveModelAndCscMetadata(info);

            if (!string.IsNullOrEmpty(info.ModelNumber) || !string.IsNullOrEmpty(info.CommercialName))
            {
                info.Success = true;

                // Print SamFw-style Formatted Table
                PrintSamFwInfoLog(info, Log);
            }
            else
            {
                Log("Failed to parse device information.", Color.Red, true);
                info.ErrorMessage = "Unable to parse device model.";
            }

            return info;
        }

        // Print beautifully aligned and color-coded table matching SamFw Tool
        private static void PrintSamFwInfoLog(SamsungMtpDeviceInfo info, Action<string, Color, bool> Log)
        {
            Color valBlue = Color.FromArgb(65, 155, 255);
            Color valGreen = Color.FromArgb(80, 230, 100);
            Color valMagenta = Color.FromArgb(255, 80, 120);
            Color valGold = Color.Gold;

            void PrintRow(string label, string? value, Color? valueColor = null)
            {
                if (string.IsNullOrEmpty(value)) return;
                string paddedLabel = label.PadRight(22);
                Log($"{paddedLabel}: {value}", valueColor ?? valBlue, true);
            }

            PrintRow("Model", info.ModelNumber, valBlue);
            PrintRow("CSC", info.Csc, valBlue);
            PrintRow("AP version", info.ApVersion, valBlue);
            PrintRow("BL version", info.BlVersion ?? info.ApVersion, valBlue);
            PrintRow("CP version", info.CpVersion ?? info.ApVersion, valBlue);
            PrintRow("CSC version", info.CscVersion, valBlue);
            PrintRow("Bit", info.Bit, valMagenta);
            PrintRow("Security patch level", info.SecurityPatchLevel, valMagenta);
            PrintRow("IMEI", info.Imei, valBlue);
            PrintRow("IMEI2", info.Imei2, valBlue);
            PrintRow("SN", info.SerialNumber, valBlue);
            PrintRow("Lock status", info.LockStatus ?? "NONE", valBlue);
            PrintRow("Phone number", info.PhoneNumber, valBlue);
            PrintRow("Country", info.Country, valBlue);
            PrintRow("MCC", info.Mcc, valBlue);
            PrintRow("MNC", info.Mnc, valBlue);
            PrintRow("USB mode", info.UsbMode ?? "AT,MTP", valBlue);
            PrintRow("Unique number", info.UniqueNumber, valBlue);
            PrintRow("Android version", info.AndroidVersion, valBlue);
            PrintRow("FRP status", info.FrpStatus ?? "UNLOCK", valBlue);

            if (!string.IsNullOrEmpty(info.Platform))
            {
                PrintRow("Platform", info.Platform, valMagenta);
            }

            if (!string.IsNullOrEmpty(info.Chipset))
            {
                PrintRow("Chipset", info.Chipset, valGreen);
            }

            if (!string.IsNullOrEmpty(info.RecommendedPreset))
            {
                PrintRow("Recommended Preset", info.RecommendedPreset, valGold);
            }
            else if (info.Platform != null && !info.Platform.Contains("Exynos", StringComparison.OrdinalIgnoreCase))
            {
                PrintRow("Flash Support", $"{info.Platform} (Not supported by Exynos Odin Flasher)", Color.Orange);
            }

            // Print warning notice if not Exynos
            if (info.Platform != null && !info.Platform.Contains("Exynos", StringComparison.OrdinalIgnoreCase))
            {
                Log("----------------------------------------------------------------------------------------------------", Color.FromArgb(70, 70, 70), true);
                Log($"[AVISO DE COMPATIBILIDADE - PLATAFORMA {info.Platform.ToUpper()}]", Color.Yellow, true);
                Log($"- Dispositivo detectado: {info.CommercialName ?? info.ModelNumber} ({info.ModelNumber})", Color.White, true);
                Log($"- Chipset / Processador: {info.Chipset}", Color.White, true);
                Log("- Status Exynos Flasher : INCOMPATIVEL (Este modulo executa bypass apenas em chips SAMSUNG EXYNOS).", Color.OrangeRed, true);
                Log("- Os recursos de Reset FRP do Master Unlock dependem do exploit Odin/sBoot Exynos.", Color.Orange, true);
                Log("----------------------------------------------------------------------------------------------------", Color.FromArgb(70, 70, 70), true);
            }
        }

        // Query AT Commands via Serial Port
        private static async Task<bool> QueryAtCommandsAsync(
            string portName,
            SamsungMtpDeviceInfo info,
            CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                SerialPort? sp = null;
                bool hadAnyResponse = false;

                try
                {
                    sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        DtrEnable = true,
                        RtsEnable = true,
                        NewLine = "\r\n"
                    };

                    sp.Open();
                    Thread.Sleep(120);

                    // Handshake AT
                    string atResp = SendAtCommand(sp, "AT");
                    if (string.IsNullOrEmpty(atResp))
                    {
                        Thread.Sleep(150);
                        atResp = SendAtCommand(sp, "AT");
                    }
                    if (string.IsNullOrEmpty(atResp))
                    {
                        Thread.Sleep(150);
                        atResp = SendAtCommand(sp, "AT");
                    }

                    if (!string.IsNullOrEmpty(atResp))
                        hadAnyResponse = true;

                    // Disable echo
                    SendAtCommand(sp, "ATE0");

                    // 1. AT+DEVCONINFO
                    string devCon = SendAtCommand(sp, "AT+DEVCONINFO");
                    if (string.IsNullOrEmpty(devCon) || devCon.Contains("ERROR"))
                    {
                        devCon = SendAtCommand(sp, "AT+DEVCONINFO=0");
                    }
                    if (string.IsNullOrEmpty(devCon) || devCon.Contains("ERROR"))
                    {
                        devCon = SendAtCommand(sp, "AT+DEVCONINFO?");
                    }

                    if (!string.IsNullOrEmpty(devCon) && !devCon.Contains("ERROR"))
                    {
                        hadAnyResponse = true;
                        info.RawDevConInfo = devCon;
                        ParseDevConInfo(devCon, info);
                    }

                    // 2. AT+VERSNAME
                    if (string.IsNullOrEmpty(info.ApVersion) || string.IsNullOrEmpty(info.CscVersion) || string.IsNullOrEmpty(info.CpVersion))
                    {
                        string vers = SendAtCommand(sp, "AT+VERSNAME=0");
                        if (string.IsNullOrEmpty(vers) || vers.Contains("ERROR"))
                            vers = SendAtCommand(sp, "AT+VERSNAME=1");
                        if (string.IsNullOrEmpty(vers) || vers.Contains("ERROR"))
                            vers = SendAtCommand(sp, "AT+VERSNAME=2");
                        if (string.IsNullOrEmpty(vers) || vers.Contains("ERROR"))
                            vers = SendAtCommand(sp, "AT+VERSNAME");

                        if (!string.IsNullOrEmpty(vers) && !vers.Contains("ERROR"))
                        {
                            hadAnyResponse = true;
                            ParseVersName(vers, info);
                        }
                    }

                    // 3. AT+SWVER / AT+SWVERSION fallback
                    if (string.IsNullOrEmpty(info.ApVersion))
                    {
                        string swver = SendAtCommand(sp, "AT+SWVER=0");
                        if (string.IsNullOrEmpty(swver) || swver.Contains("ERROR"))
                            swver = SendAtCommand(sp, "AT+SWVERSION=0");
                        if (string.IsNullOrEmpty(swver) || swver.Contains("ERROR"))
                            swver = SendAtCommand(sp, "AT+SWVER");
                        if (string.IsNullOrEmpty(swver) || swver.Contains("ERROR"))
                            swver = SendAtCommand(sp, "AT+SWVERSION");

                        if (!string.IsNullOrEmpty(swver) && !swver.Contains("ERROR"))
                        {
                            hadAnyResponse = true;
                            ParseSwVer(swver, info);
                        }
                    }

                    // 4. AT+FACTORST=0,0
                    if (string.IsNullOrEmpty(info.LockStatus))
                    {
                        string factor = SendAtCommand(sp, "AT+FACTORST=0,0");
                        if (string.IsNullOrEmpty(factor) || factor.Contains("ERROR"))
                            factor = SendAtCommand(sp, "AT+LOCKSTATUS");

                        if (!string.IsNullOrEmpty(factor))
                        {
                            hadAnyResponse = true;
                            if (factor.Contains("OK") || factor.Contains("0") || factor.Contains("NONE"))
                                info.LockStatus = "NONE";
                            else if (factor.Contains("1") || factor.Contains("LOCK"))
                                info.LockStatus = "LOCKED";
                        }
                    }

                    // 5. AT+FRPSTATUS or AT+FRPLOCK
                    string frp = SendAtCommand(sp, "AT+FRPSTATUS");
                    if (string.IsNullOrEmpty(frp) || frp.Contains("ERROR"))
                        frp = SendAtCommand(sp, "AT+FRPLOCK");

                    if (!string.IsNullOrEmpty(frp))
                    {
                        hadAnyResponse = true;
                        if (frp.Contains("TRIGGER") || frp.Contains("LOCK") || frp.Contains("1"))
                            info.FrpStatus = "LOCK";
                        else if (frp.Contains("UNLOCK") || frp.Contains("0") || frp.Contains("OFF"))
                            info.FrpStatus = "UNLOCK";
                    }

                    // 6. IMEI fallback
                    if (string.IsNullOrEmpty(info.Imei))
                    {
                        string gsn = SendAtCommand(sp, "AT+CGSN");
                        if (string.IsNullOrEmpty(gsn) || gsn.Contains("ERROR"))
                            gsn = SendAtCommand(sp, "AT+GSN");
                        if (string.IsNullOrEmpty(gsn) || gsn.Contains("ERROR"))
                            gsn = SendAtCommand(sp, "AT+IMEI");

                        if (!string.IsNullOrEmpty(gsn) && !gsn.Contains("ERROR"))
                        {
                            hadAnyResponse = true;
                            var matches = Regex.Matches(gsn, @"\b(\d{15})\b");
                            if (matches.Count > 0) info.Imei = matches[0].Groups[1].Value;
                            if (matches.Count > 1) info.Imei2 = matches[1].Groups[1].Value;
                        }
                    }

                    // 7. Serial Number fallback
                    if (string.IsNullOrEmpty(info.SerialNumber))
                    {
                        string snResp = SendAtCommand(sp, "AT+SERIALNO");
                        if (string.IsNullOrEmpty(snResp) || snResp.Contains("ERROR"))
                            snResp = SendAtCommand(sp, "AT+SN");

                        if (!string.IsNullOrEmpty(snResp) && !snResp.Contains("ERROR"))
                        {
                            hadAnyResponse = true;
                            var mSn = Regex.Match(snResp, @"\b([A-Za-z0-9]{10,14})\b");
                            if (mSn.Success) info.SerialNumber = mSn.Groups[1].Value;
                        }
                    }

                    // 8. CSC fallback
                    if (string.IsNullOrEmpty(info.Csc))
                    {
                        string cscResp = SendAtCommand(sp, "AT+READCSC");
                        if (string.IsNullOrEmpty(cscResp) || cscResp.Contains("ERROR"))
                            cscResp = SendAtCommand(sp, "AT+OMCCODE");

                        if (!string.IsNullOrEmpty(cscResp) && !cscResp.Contains("ERROR"))
                        {
                            var mCsc = Regex.Match(cscResp, @"\b([A-Za-z]{3})\b");
                            if (mCsc.Success) info.Csc = mCsc.Groups[1].Value.ToUpper();
                        }
                    }

                    // 9. Phone number query
                    if (string.IsNullOrEmpty(info.PhoneNumber))
                    {
                        string cnum = SendAtCommand(sp, "AT+CNUM");
                        if (!string.IsNullOrEmpty(cnum) && !cnum.Contains("ERROR"))
                        {
                            var mNum = Regex.Match(cnum, @"\+?(\d{8,15})");
                            if (mNum.Success) info.PhoneNumber = mNum.Groups[1].Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MTP AT Error] {ex.Message}");
                }
                finally
                {
                    try
                    {
                        if (sp != null && sp.IsOpen)
                            sp.Close();
                        sp?.Dispose();
                    }
                    catch { }
                }

                return hadAnyResponse;
            }, ct);
        }

        // Send AT Command and buffer response until OK/ERROR
        private static string SendAtCommand(SerialPort sp, string command, int timeoutMs = 2500)
        {
            try
            {
                sp.DiscardInBuffer();
                sp.DiscardOutBuffer();
                sp.Write(command + "\r\n");

                var sb = new StringBuilder();
                var sw = Stopwatch.StartNew();

                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    if (sp.BytesToRead > 0)
                    {
                        string chunk = sp.ReadExisting();
                        sb.Append(chunk);
                        string current = sb.ToString();
                        if (current.Contains("OK") || current.Contains("ERROR"))
                        {
                            break;
                        }
                    }
                    Thread.Sleep(15);
                }

                return sb.ToString().Trim();
            }
            catch
            {
                return "";
            }
        }

        // Extract token by multiple alternative keys from DEVCONINFO
        private static string ExtractDevConToken(string response, params string[] keys)
        {
            foreach (var key in keys)
            {
                // 1. KEY(VALUE)
                var m1 = Regex.Match(response, $@"(?:^|[;,\s]){key}\(([^)]*)\)", RegexOptions.IgnoreCase);
                if (m1.Success && !string.IsNullOrWhiteSpace(m1.Groups[1].Value))
                    return m1.Groups[1].Value.Trim();

                // 2. KEY=VALUE or KEY: VALUE
                var m2 = Regex.Match(response, $@"(?:^|[;,\s]){key}[:=]\s*([^;,\r\n\(\)]+)", RegexOptions.IgnoreCase);
                if (m2.Success && !string.IsNullOrWhiteSpace(m2.Groups[1].Value))
                    return m2.Groups[1].Value.Trim();
            }
            return "";
        }

        // Parse AT+DEVCONINFO response
        private static void ParseDevConInfo(string response, SamsungMtpDeviceInfo info)
        {
            try
            {
                // Model Number
                string mn = ExtractDevConToken(response, "MN", "MODEL", "MODEL_NAME", "PRD_NAME");
                if (!string.IsNullOrEmpty(mn)) info.ModelNumber = mn.ToUpper();

                // Software / Build Version (PDA / AP)
                string ver = ExtractDevConToken(response, "VER", "HIDVER", "SW", "SW_VER", "PDA", "AP");
                if (!string.IsNullOrEmpty(ver))
                {
                    if (ver.Contains('/'))
                    {
                        string[] parts = ver.Split('/', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 1)
                        {
                            info.ApVersion = parts[0].Trim();
                            info.BlVersion = parts[0].Trim();
                        }
                        if (parts.Length >= 2)
                        {
                            info.CscVersion = parts[1].Trim();
                        }
                        if (parts.Length >= 3)
                        {
                            info.CpVersion = parts[2].Trim();
                        }
                    }
                    else
                    {
                        info.ApVersion = ver;
                        info.BlVersion = ver;
                    }
                }

                // Baseband / Modem / CP
                string baseband = ExtractDevConToken(response, "BASE", "CP", "PHONE", "MODEM", "BASEBAND");
                if (!string.IsNullOrEmpty(baseband) && !baseband.Equals("UNKNOWN", StringComparison.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(info.CpVersion)) info.CpVersion = baseband;
                }

                // CSC / Product Code
                string prd = ExtractDevConToken(response, "PRD", "OMCCODE", "CC", "CSC", "SALES", "BUYER");
                if (!string.IsNullOrEmpty(prd))
                {
                    if (prd.Length == 3 && Regex.IsMatch(prd, @"^[A-Za-z]{3}$"))
                    {
                        info.Csc = prd.ToUpper();
                    }
                    else if (prd.Length > 3)
                    {
                        string last3 = prd.Substring(prd.Length - 3);
                        if (Regex.IsMatch(last3, @"^[A-Za-z]{3}$"))
                            info.Csc = last3.ToUpper();
                    }
                }

                string omc = ExtractDevConToken(response, "OMCCODE", "OMC");
                if (!string.IsNullOrEmpty(omc) && Regex.IsMatch(omc, @"^[A-Za-z]{3}$"))
                {
                    info.Csc = omc.ToUpper();
                }

                // Serial Number
                string sn = ExtractDevConToken(response, "SN", "SERIAL", "SER", "SN_NUM");
                if (!string.IsNullOrEmpty(sn)) info.SerialNumber = sn;

                // Unique Number (Mainboard serial)
                string un = ExtractDevConToken(response, "UN", "UNIQUE_NO", "MAINBOARD_SN", "CHIP_ID");
                if (!string.IsNullOrEmpty(un)) info.UniqueNumber = un;

                // IMEI
                string imei = ExtractDevConToken(response, "IMEI", "IMEI1", "CGSN");
                if (!string.IsNullOrEmpty(imei) && Regex.IsMatch(imei, @"^\d{15}$"))
                {
                    info.Imei = imei;
                }

                string imei2 = ExtractDevConToken(response, "IMEI2", "IMEI_2");
                if (!string.IsNullOrEmpty(imei2) && Regex.IsMatch(imei2, @"^\d{15}$"))
                {
                    info.Imei2 = imei2;
                }

                // MCC / MNC
                string mcc = ExtractDevConToken(response, "MCC", "CL", "MCC_CODE");
                if (!string.IsNullOrEmpty(mcc)) info.Mcc = mcc.TrimEnd(',') + ",";

                string mnc = ExtractDevConToken(response, "MNC", "MNC_CODE");
                if (!string.IsNullOrEmpty(mnc)) info.Mnc = mnc.TrimEnd(',') + ",";

                // USB Connection Mode
                string con = ExtractDevConToken(response, "CON", "MODE", "USB_MODE");
                if (!string.IsNullOrEmpty(con)) info.UsbMode = con;

                // Lock status
                string lockStatus = ExtractDevConToken(response, "LOCK", "LOCK_STATUS", "SIM_LOCK");
                if (!string.IsNullOrEmpty(lockStatus)) info.LockStatus = lockStatus.ToUpper();

                // Part Number / Product Name
                string pn = ExtractDevConToken(response, "PN", "PROD_NAME", "PART_NO");
                if (!string.IsNullOrEmpty(pn))
                {
                    if (Regex.IsMatch(pn, @"^\+?\d{8,15}$"))
                    {
                        info.PhoneNumber = pn;
                    }
                    else if (string.IsNullOrEmpty(info.ModelNumber))
                    {
                        var mPn = Regex.Match(pn, @"^(SM-[A-Z0-9]+)", RegexOptions.IgnoreCase);
                        if (mPn.Success) info.ModelNumber = mPn.Groups[1].Value.ToUpper();
                    }
                }
            }
            catch { }
        }

        // Parse AT+VERSNAME response
        private static void ParseVersName(string response, SamsungMtpDeviceInfo info)
        {
            try
            {
                var pdaMatch = Regex.Match(response, @"PDA[:\s\(=]+([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
                if (pdaMatch.Success)
                {
                    string pda = pdaMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(info.ApVersion)) info.ApVersion = pda;
                    if (string.IsNullOrEmpty(info.BlVersion)) info.BlVersion = pda;
                }

                var cscMatch = Regex.Match(response, @"CSC[:\s\(=]+([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
                if (cscMatch.Success)
                {
                    string cscStr = cscMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(info.CscVersion)) info.CscVersion = cscStr;
                }

                var phoneMatch = Regex.Match(response, @"(?:PHONE|BASE)[:\s\(=]+([A-Za-z0-9_]+)", RegexOptions.IgnoreCase);
                if (phoneMatch.Success)
                {
                    string phone = phoneMatch.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(info.CpVersion)) info.CpVersion = phone;
                }

                if (string.IsNullOrEmpty(info.ModelNumber) && !string.IsNullOrEmpty(info.ApVersion))
                {
                    var m = Regex.Match(info.ApVersion, @"^(SM-[A-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                        info.ModelNumber = m.Groups[1].Value.ToUpper();
                }
            }
            catch { }
        }

        // Parse AT+SWVER response
        private static void ParseSwVer(string response, SamsungMtpDeviceInfo info)
        {
            try
            {
                var m = Regex.Match(response, @"(?:SWVER|SWVERSION)?[:\s\(=]*([A-Za-z0-9_]{8,15})", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    string val = m.Groups[1].Value.Trim();
                    if (string.IsNullOrEmpty(info.ApVersion)) info.ApVersion = val;
                    if (string.IsNullOrEmpty(info.BlVersion)) info.BlVersion = val;
                }
            }
            catch { }
        }

        // Extract metadata from USB / WPD device descriptors
        private static void ExtractInfoFromDescriptors(
            List<(string deviceId, string description, string friendlyName, string serialNumber)> devices,
            SamsungMtpDeviceInfo info)
        {
            foreach (var dev in devices)
            {
                if (!string.IsNullOrEmpty(dev.friendlyName))
                {
                    var m = Regex.Match(dev.friendlyName, @"(SM-[A-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        if (string.IsNullOrEmpty(info.ModelNumber)) info.ModelNumber = m.Groups[1].Value.ToUpper();
                    }
                    else if (dev.friendlyName.Contains("Galaxy", StringComparison.OrdinalIgnoreCase))
                    {
                        if (string.IsNullOrEmpty(info.CommercialName)) info.CommercialName = dev.friendlyName;
                    }
                }

                if (string.IsNullOrEmpty(info.ModelNumber) && !string.IsNullOrEmpty(dev.description))
                {
                    var m = Regex.Match(dev.description, @"(SM-[A-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                        info.ModelNumber = m.Groups[1].Value.ToUpper();
                }

                if (string.IsNullOrEmpty(info.ModelNumber) && !string.IsNullOrEmpty(dev.deviceId))
                {
                    var m = Regex.Match(dev.deviceId, @"(SM-[A-Z0-9]+)", RegexOptions.IgnoreCase);
                    if (m.Success)
                        info.ModelNumber = m.Groups[1].Value.ToUpper();
                }

                if (string.IsNullOrEmpty(info.SerialNumber) && !string.IsNullOrEmpty(dev.serialNumber))
                {
                    info.SerialNumber = dev.serialNumber;
                }
            }
        }

        // Fill CommercialName, Platform, Chipset, Bit, SecurityPatch, AndroidVersion and CSC info
        private static void ResolveModelAndCscMetadata(SamsungMtpDeviceInfo info)
        {
            if (string.IsNullOrEmpty(info.ModelNumber) && !string.IsNullOrEmpty(info.ApVersion))
            {
                var m = Regex.Match(info.ApVersion, @"^([A-Z0-9]+)");
                if (m.Success)
                {
                    string raw = m.Groups[1].Value;
                    if (!raw.StartsWith("SM-", StringComparison.OrdinalIgnoreCase) && raw.Length >= 5)
                        info.ModelNumber = "SM-" + raw.Substring(0, 5);
                }
            }

            if (!string.IsNullOrEmpty(info.ModelNumber))
            {
                string normalizedModel = info.ModelNumber.ToUpper();

                if (ModelMap.TryGetValue(normalizedModel, out var mapped))
                {
                    info.Platform = mapped.platform;
                    info.Chipset = mapped.chipset;
                    info.RecommendedPreset = mapped.preset;
                    info.CommercialName = mapped.galaxyName;
                }
                else
                {
                    bool found = false;
                    foreach (var kvp in ModelMap)
                    {
                        if (normalizedModel.StartsWith(kvp.Key.Substring(0, Math.Min(kvp.Key.Length, 7)), StringComparison.OrdinalIgnoreCase))
                        {
                            info.Platform = kvp.Value.platform;
                            info.Chipset = kvp.Value.chipset;
                            info.RecommendedPreset = kvp.Value.preset;
                            info.CommercialName = kvp.Value.galaxyName;
                            found = true;
                            break;
                        }
                    }

                    if (!found && string.IsNullOrEmpty(info.Platform))
                    {
                        info.Platform = "Unknown Platform";
                    }
                }
            }

            // Extract CSC from CSC Version if available (e.g. A245MOWO6CXE1 -> OWO -> ZTO fallback if Brazil)
            if (string.IsNullOrEmpty(info.Csc) && !string.IsNullOrEmpty(info.CscVersion) && info.CscVersion.Length >= 8)
            {
                var mCsc = Regex.Match(info.CscVersion, @"^[A-Za-z0-9]{5}([A-Za-z]{3})");
                if (mCsc.Success)
                {
                    string cscFound = mCsc.Groups[1].Value.ToUpper();
                    if (CscMap.ContainsKey(cscFound))
                        info.Csc = cscFound;
                }
            }

            // CSC Sanitization and Country lookup
            if (!string.IsNullOrEmpty(info.Csc))
            {
                if (Regex.IsMatch(info.Csc, @"^[0-9,]+$"))
                {
                    if (string.IsNullOrEmpty(info.Mcc)) info.Mcc = info.Csc;
                    info.Csc = "ZTO";
                }

                if (CscMap.TryGetValue(info.Csc.Trim(), out var cscData))
                {
                    if (string.IsNullOrEmpty(info.Country)) info.Country = cscData.country;
                    if (string.IsNullOrEmpty(info.Mcc)) info.Mcc = cscData.mcc;
                    if (string.IsNullOrEmpty(info.Mnc)) info.Mnc = cscData.mnc;
                }
            }

            // Binary Bit calculation from AP Version (5th char from the right)
            // Example: A245MUBS6CXE1 -> '6'
            if (!string.IsNullOrEmpty(info.ApVersion) && info.ApVersion.Length >= 5)
            {
                if (string.IsNullOrEmpty(info.Bit))
                {
                    char bitChar = info.ApVersion[^5];
                    info.Bit = bitChar.ToString();
                }

                if (string.IsNullOrEmpty(info.SecurityPatchLevel))
                {
                    info.SecurityPatchLevel = CalculateSecurityPatch(info.ApVersion);
                }

                if (string.IsNullOrEmpty(info.AndroidVersion))
                {
                    info.AndroidVersion = CalculateAndroidVersion(info.ApVersion, info.ModelNumber);
                }
            }

            // Default Values
            if (string.IsNullOrEmpty(info.Country) && !string.IsNullOrEmpty(info.PhoneNumber) && info.PhoneNumber.StartsWith("55"))
            {
                info.Country = "BR";
                if (string.IsNullOrEmpty(info.Csc)) info.Csc = "ZTO";
                if (string.IsNullOrEmpty(info.Mcc)) info.Mcc = "724,";
                if (string.IsNullOrEmpty(info.Mnc)) info.Mnc = "05,";
            }

            if (string.IsNullOrEmpty(info.LockStatus)) info.LockStatus = "NONE";
            if (string.IsNullOrEmpty(info.FrpStatus)) info.FrpStatus = "LOCK";
            if (string.IsNullOrEmpty(info.UsbMode)) info.UsbMode = "AT,MTP";
            if (string.IsNullOrEmpty(info.BlVersion) && !string.IsNullOrEmpty(info.ApVersion)) info.BlVersion = info.ApVersion;
            if (string.IsNullOrEmpty(info.CpVersion) && !string.IsNullOrEmpty(info.ApVersion)) info.CpVersion = info.ApVersion;
        }

        // Calculate security patch date from Samsung build code
        // Structure: ...[Bit][OS][Year][Month][Revision]
        // Year: U=2021, V=2022, W=2023, X=2024, Y=2025, Z=2026, A=2027
        // Month: A=Jan, B=Feb, C=Mar, D=Apr, E=May, F=Jun, G=Jul, H=Aug, I=Sep, J=Oct, K=Nov, L=Dec
        private static string CalculateSecurityPatch(string ap)
        {
            if (ap.Length < 4) return "2024-05-01";
            try
            {
                char yChar = char.ToUpper(ap[^3]);
                char mChar = char.ToUpper(ap[^2]);

                int year = yChar switch
                {
                    'S' => 2019,
                    'T' => 2020,
                    'U' => 2021,
                    'V' => 2022,
                    'W' => 2023,
                    'X' => 2024,
                    'Y' => 2025,
                    'Z' => 2026,
                    'A' => 2027,
                    'B' => 2028,
                    _ => 2024
                };

                int month = mChar switch
                {
                    'A' => 1, 'B' => 2, 'C' => 3, 'D' => 4, 'E' => 5, 'F' => 6,
                    'G' => 7, 'H' => 8, 'I' => 9, 'J' => 10, 'K' => 11, 'L' => 12,
                    _ => 5
                };

                return $"{year:D4}-{month:D2}-01";
            }
            catch
            {
                return "2024-05-01";
            }
        }

        // Calculate Android version from Samsung AP build and Model
        private static string CalculateAndroidVersion(string ap, string? model)
        {
            if (ap.Length < 4) return "14";
            try
            {
                char yChar = char.ToUpper(ap[^3]);
                char mChar = char.ToUpper(ap[^2]);

                return yChar switch
                {
                    'Z' => "16",
                    'Y' => "15",
                    'X' => "14",
                    'W' => mChar >= 'I' ? "14" : "13",
                    'V' => mChar >= 'J' ? "13" : "12",
                    'U' => mChar >= 'K' ? "12" : "11",
                    'T' => "11",
                    'S' => "10",
                    _ => "14"
                };
            }
            catch
            {
                return "14";
            }
        }

        // Detect all connected Samsung USB devices using SetupAPI and Registry
        public static List<(string deviceId, string description, string friendlyName, string serialNumber)> DetectSamsungUsbDevices()
        {
            var list = new List<(string, string, string, string)>();

            try
            {
                IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_ALLCLASSES | DIGCF_PRESENT);
                if (hDevInfo == INVALID_HANDLE_VALUE) return list;

                try
                {
                    var devInfoData = new SP_DEVINFO_DATA();
                    devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                    for (uint i = 0; SetupDiEnumDeviceInfo(hDevInfo, i, ref devInfoData); i++)
                    {
                        var idBuf = new char[1024];
                        if (!SetupDiGetDeviceInstanceId(hDevInfo, ref devInfoData, idBuf, 1024, out _))
                            continue;

                        string deviceId = new string(idBuf).TrimEnd('\0');
                        if (!deviceId.Contains("VID_04E8", StringComparison.OrdinalIgnoreCase))
                            continue;

                        string friendlyName = GetDeviceRegistryString(hDevInfo, ref devInfoData, SPDRP_FRIENDLYNAME);
                        string description = GetDeviceRegistryString(hDevInfo, ref devInfoData, SPDRP_DEVICEDESC);

                        string serialNumber = "";
                        string[] parts = deviceId.Split('\\');
                        if (parts.Length >= 3)
                        {
                            string lastPart = parts[^1];
                            if (!lastPart.Contains('&') && lastPart.Length >= 6)
                                serialNumber = lastPart;
                        }

                        list.Add((deviceId, description, friendlyName, serialNumber));
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(hDevInfo);
                }
            }
            catch { }

            return list;
        }

        // Find Samsung Mobile USB Modem COM port with Friendly description
        public static string FindSamsungModemPort(out string portDescription)
        {
            portDescription = "";
            try
            {
                IntPtr hDevInfo = SetupDiGetClassDevs(IntPtr.Zero, null, IntPtr.Zero, DIGCF_ALLCLASSES | DIGCF_PRESENT);
                if (hDevInfo == INVALID_HANDLE_VALUE) return "";

                try
                {
                    var devInfoData = new SP_DEVINFO_DATA();
                    devInfoData.cbSize = (uint)Marshal.SizeOf(devInfoData);

                    for (uint i = 0; SetupDiEnumDeviceInfo(hDevInfo, i, ref devInfoData); i++)
                    {
                        var idBuf = new char[1024];
                        if (!SetupDiGetDeviceInstanceId(hDevInfo, ref devInfoData, idBuf, 1024, out _))
                            continue;

                        string deviceId = new string(idBuf).ToLower().TrimEnd('\0');
                        if (!deviceId.Contains("vid_04e8")) continue;

                        string desc = GetDeviceRegistryString(hDevInfo, ref devInfoData, SPDRP_DEVICEDESC);
                        string fn = GetDeviceRegistryString(hDevInfo, ref devInfoData, SPDRP_FRIENDLYNAME);

                        if (desc.Contains("modem", StringComparison.OrdinalIgnoreCase) ||
                            fn.Contains("modem", StringComparison.OrdinalIgnoreCase) ||
                            desc.Contains("samsung", StringComparison.OrdinalIgnoreCase) ||
                            deviceId.Contains("pid_6860"))
                        {
                            string port = ExtractPort(hDevInfo, ref devInfoData, fn);
                            if (!string.IsNullOrEmpty(port))
                            {
                                portDescription = !string.IsNullOrEmpty(fn) ? fn : (!string.IsNullOrEmpty(desc) ? $"{desc} ({port})" : $"SAMSUNG Mobile USB Modem ({port})");
                                return port;
                            }
                        }
                    }
                }
                finally
                {
                    SetupDiDestroyDeviceInfoList(hDevInfo);
                }
            }
            catch { }

            // Registry fallback
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
                if (key != null)
                {
                    foreach (var val in key.GetValueNames())
                    {
                        if (val.Contains("Samsung", StringComparison.OrdinalIgnoreCase) ||
                            val.Contains("Modem", StringComparison.OrdinalIgnoreCase) ||
                            val.Contains("CdmaModem", StringComparison.OrdinalIgnoreCase))
                        {
                            string? p = key.GetValue(val)?.ToString();
                            if (!string.IsNullOrEmpty(p))
                            {
                                portDescription = $"SAMSUNG Mobile USB Modem ({p})";
                                return p;
                            }
                        }
                    }
                }
            }
            catch { }

            return "";
        }

        private static string ExtractPort(IntPtr hDevInfo, ref SP_DEVINFO_DATA devInfoData, string friendlyName)
        {
            if (!string.IsNullOrEmpty(friendlyName))
            {
                var m = Regex.Match(friendlyName, @"\(COM(\d+)\)", RegexOptions.IgnoreCase);
                if (m.Success) return "COM" + m.Groups[1].Value;
            }

            try
            {
                IntPtr hKey = SetupDiOpenDevRegKey(hDevInfo, ref devInfoData, DICS_FLAG_GLOBAL, 0, DIREG_DEV, KEY_READ);
                if (hKey != INVALID_HANDLE_VALUE)
                {
                    try
                    {
                        var buf = new char[256];
                        uint size = 512;
                        uint type = 0;
                        if (RegQueryValueEx(hKey, "PortName", IntPtr.Zero, ref type, buf, ref size) == 0)
                        {
                            string port = new string(buf).TrimEnd('\0');
                            if (!string.IsNullOrEmpty(port)) return port;
                        }
                    }
                    finally { RegCloseKey(hKey); }
                }
            }
            catch { }

            return "";
        }

        private static string GetDeviceRegistryString(IntPtr hDevInfo, ref SP_DEVINFO_DATA devInfoData, uint property)
        {
            try
            {
                var buf = new char[512];
                if (SetupDiGetDeviceRegistryProperty(hDevInfo, ref devInfoData, property, out _, buf, (uint)(buf.Length * sizeof(char)), out _))
                {
                    return new string(buf).TrimEnd('\0');
                }
            }
            catch { }
            return "";
        }
        // Reboot Samsung device to Download Mode via Modem AT commands or ADB
        public static async Task<bool> RebootToDownloadModeAsync(Action<string, Color, bool> logger, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                logger("[SAMSUNG] Iniciando procedimento para entrar em Modo Download...", Color.FromArgb(0, 210, 255), true);

                // 1. Check Samsung Modem Port
                string portName = FindSamsungModemPort(out string portDesc);
                if (string.IsNullOrEmpty(portName))
                {
                    logger("[SAMSUNG] Nenhuma porta 'SAMSUNG Mobile USB Modem' encontrada automaticamente.", Color.FromArgb(255, 196, 0), true);
                    logger("[SAMSUNG] Tentando via comando ADB reboot download...", Color.FromArgb(145, 172, 204), true);

                    // Fallback via ADB
                    bool adbSuccess = TryAdbReboot("download", logger);
                    if (adbSuccess)
                    {
                        logger("[SAMSUNG] Comando de reinicializacao para Download enviado com sucesso via ADB!", Color.FromArgb(0, 230, 92), true);
                        return true;
                    }

                    logger("[SAMSUNG] Falha: Conecte o aparelho ligado em modo normal com depuracao USB ou com porta Modem ativa.", Color.FromArgb(255, 52, 64), true);
                    return false;
                }

                logger($"[SAMSUNG] Porta Modem detectada: {portName} ({portDesc})", Color.FromArgb(0, 210, 255), true);
                logger("[SAMSUNG] Enviando sequencia de comandos AT para Modo Download...", Color.FromArgb(145, 172, 204), true);

                SerialPort? sp = null;
                try
                {
                    sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 2000,
                        WriteTimeout = 2000,
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    sp.Open();

                    // Handshake
                    SendAtCommand(sp, "AT", 1000);

                    // Primary Samsung Download Mode switch command
                    string r1 = SendAtCommand(sp, "AT+FWSWITCH=255,1", 2000);
                    logger($"[AT+FWSWITCH=255,1] Resposta: {(string.IsNullOrEmpty(r1) ? "OK / Reiniciando aparelho..." : r1)}", Color.FromArgb(145, 172, 204), true);

                    if (string.IsNullOrEmpty(r1) || r1.Contains("OK") || r1.Contains("ERROR"))
                    {
                        string r2 = SendAtCommand(sp, "AT+FWSWITCH=1", 1500);
                        if (!string.IsNullOrEmpty(r2))
                            logger($"[AT+FWSWITCH=1] Resposta: {r2}", Color.FromArgb(145, 172, 204), true);
                    }

                    logger("[SAMSUNG] Comando enviado com sucesso! O aparelho deve reiniciar na tela azul de Download (Odin Mode).", Color.FromArgb(0, 230, 92), true);
                    return true;
                }
                catch (Exception ex)
                {
                    logger($"[SAMSUNG] Erro ao enviar comando para porta {portName}: {ex.Message}", Color.FromArgb(255, 52, 64), true);
                    return false;
                }
                finally
                {
                    try
                    {
                        if (sp != null && sp.IsOpen) sp.Close();
                        sp?.Dispose();
                    }
                    catch { }
                }
            }, ct);
        }

        // Factory Reset Samsung device via Modem AT commands or ADB
        public static async Task<bool> FactoryResetAsync(Action<string, Color, bool> logger, CancellationToken ct)
        {
            return await Task.Run(() =>
            {
                logger("[SAMSUNG] Iniciando procedimento de Resete de Fabrica (Factory Reset / Wipe Data)...", Color.FromArgb(255, 196, 0), true);

                // 1. Check Samsung Modem Port
                string portName = FindSamsungModemPort(out string portDesc);
                if (string.IsNullOrEmpty(portName))
                {
                    logger("[SAMSUNG] Nenhuma porta 'SAMSUNG Mobile USB Modem' encontrada automaticamente.", Color.FromArgb(255, 196, 0), true);
                    logger("[SAMSUNG] Tentando via comando ADB recovery wipe...", Color.FromArgb(145, 172, 204), true);

                    // Fallback via ADB
                    bool adbSuccess = TryAdbReboot("recovery", logger);
                    if (adbSuccess)
                    {
                        logger("[SAMSUNG] Dispositivo reiniciado para Recovery Mode via ADB. Realize o Wipe Data/Factory Reset.", Color.FromArgb(0, 230, 92), true);
                        return true;
                    }

                    logger("[SAMSUNG] Falha: Conecte o aparelho ligado em modo normal com porta Modem Samsung ativa.", Color.FromArgb(255, 52, 64), true);
                    return false;
                }

                logger($"[SAMSUNG] Porta Modem detectada: {portName} ({portDesc})", Color.FromArgb(0, 210, 255), true);
                logger("[SAMSUNG] Enviando comandos AT de Restauracao de Fabrica...", Color.FromArgb(145, 172, 204), true);

                SerialPort? sp = null;
                try
                {
                    sp = new SerialPort(portName, 115200, Parity.None, 8, StopBits.One)
                    {
                        ReadTimeout = 3000,
                        WriteTimeout = 3000,
                        DtrEnable = true,
                        RtsEnable = true
                    };
                    sp.Open();

                    // Handshake
                    SendAtCommand(sp, "AT", 1000);

                    // Try standard Samsung Factory Reset AT commands
                    string r1 = SendAtCommand(sp, "AT+FACTORST=0,0", 2500);
                    logger($"[AT+FACTORST=0,0] Resposta: {(string.IsNullOrEmpty(r1) ? "OK / Processado" : r1)}", Color.FromArgb(145, 172, 204), true);

                    string r2 = SendAtCommand(sp, "AT+FACTORST", 2000);
                    if (!string.IsNullOrEmpty(r2))
                        logger($"[AT+FACTORST] Resposta: {r2}", Color.FromArgb(145, 172, 204), true);

                    string r3 = SendAtCommand(sp, "AT+SWRESET", 2000);
                    if (!string.IsNullOrEmpty(r3))
                        logger($"[AT+SWRESET] Resposta: {r3}", Color.FromArgb(145, 172, 204), true);

                    logger("[SAMSUNG] Comandos de Factory Reset enviados! O aparelho executara a limpeza de dados e reiniciara.", Color.FromArgb(0, 230, 92), true);
                    return true;
                }
                catch (Exception ex)
                {
                    logger($"[SAMSUNG] Erro ao enviar comando de Factory Reset: {ex.Message}", Color.FromArgb(255, 52, 64), true);
                    return false;
                }
                finally
                {
                    try
                    {
                        if (sp != null && sp.IsOpen) sp.Close();
                        sp?.Dispose();
                    }
                    catch { }
                }
            }, ct);
        }

        private static bool TryAdbReboot(string target, Action<string, Color, bool> logger)
        {
            try
            {
                string adbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "adb.exe");
                if (!File.Exists(adbPath)) adbPath = "adb.exe";

                var psi = new ProcessStartInfo
                {
                    FileName = adbPath,
                    Arguments = $"reboot {target}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var p = Process.Start(psi);
                if (p == null) return false;
                p.WaitForExit(4000);
                return p.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }
    }
}
