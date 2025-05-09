using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Memory;

namespace MemoryPatcher
{
    internal class Program
    {
        public static bool attached = false;
        public static int errorcode = 1;
        public static Mem mem = new Mem();

        [DllImport("kernel32.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWow64Process(IntPtr hProcess, out bool wow64Process);

        static bool Is64BitProcess(Process process)
        {
            if (!Environment.Is64BitOperatingSystem)
                return false; // OS自体が32bitならプロセスも32bit

            bool isWow64;
            if (!IsWow64Process(process.Handle, out isWow64))
                throw new System.ComponentModel.Win32Exception();

            return !isWow64; // Wow64なら32bit、そうでなければ64bit
        }

        static bool IsRunAsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
            {
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
        static int Main(string[] args)
        {
            bool isAdminMode = args.Contains("/admin-mode");
            bool Admin = args.Contains("/Admin");
            StreamWriter writer = null;
            const string outputFile = "admin_output.txt"; // 出力ファイル名
            // コマンドライン引数がない場合の処理
            if (args.Length == 0)
            {
                Console.WriteLine("No arguments! /h for help.");
                Thread.Sleep(4000);
                return 1;
            }
            if (args[0] == "/h") 
            {
                Console.WriteLine(@"EasyMemoryPatcher 1.7
This is a console application that allows you to control memory from common executable programs such as the command prompt or bat files.
How to Use
MemoryPatcher WriteMemory 0x21C14E3E byte 0xFF /pid 14052
MemoryPatcher.exe ReadBytes 0x21C14E3E 5 /pid 14052
C:\MemoryPatcher.exe ReadBits 0x21C14E3E /pname pcsx2.exe
MemoryPatcher WriteBits 0x21C14E3E 01111110 /pname pcsx2
MemoryPatcher AoBScan ""F9 11 39 44 B3 ?? 8F 3F C3 11"" /pname pcsx2.exe
MemoryPatcher CheckProcess /pname pcsx2.exe

");
                return 0;
            }

            if (Admin)
            {
                if (!IsRunAsAdministrator())
                {
                    // 管理者権限じゃない → 自分をrunasで起動
                    string arguments = string.Join(" ", args.Select(arg => $"\"{arg}\""));

                    try
                    {
                        var processInfo = new ProcessStartInfo
                        {
                            FileName = Process.GetCurrentProcess().MainModule.FileName,
                            Arguments = arguments.Replace("/Admin", "") + " /admin-mode",
                            UseShellExecute = true,
                            Verb = "runas"
                        };

                        var process = Process.Start(processInfo);
                        process.WaitForExit(); // 昇格したプロセスが終わるのを待つ

                        // 出力ファイルを読む
                        if (File.Exists(outputFile))
                        {
                            string output = File.ReadAllText(outputFile);
                            Console.WriteLine(output);
                            File.Delete(outputFile); // 読み終わったら削除（お好みで）
                        }

                        return process.ExitCode;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine("Elevation failed: " + ex.Message);
                        return -1;
                    }
                }
            }
            if (isAdminMode)
            {
                writer = new StreamWriter(outputFile, append: false, encoding: Encoding.UTF8)
                {
                    AutoFlush = true
                };

                Console.SetOut(writer);
                Console.SetError(writer);
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "/pid")
                {
                    if (args[0] == "CheckProcess")
                    {
                        if (Is64BitProcess(Process.GetProcessById(Convert.ToInt32(args[i + 1]))))
                        {
                            Console.WriteLine("64bit");
                        }
                        else
                        {
                            Console.WriteLine("32bit");
                        }
                        return 0;
                    }
                    if (Environment.Is64BitProcess)
                    {
                        if (Is64BitProcess(Process.GetProcessById(Convert.ToInt32(args[i + 1]))))
                        {
                            if (!mem.OpenProcess(Convert.ToInt32(args[i + 1])))
                            {
                                Console.WriteLine("Attach Failed");
                                return 1;
                            }
                            attached = true;
                        }
                        else
                        {
                            Console.WriteLine("64bit process cannot access 32bit process.");
                            return -1;
                        }
                    }
                    else
                    {
                        if (Is64BitProcess(Process.GetProcessById(Convert.ToInt32(args[i + 1]))))
                        {
                            Console.WriteLine("32bit process cannot access 64bit process.");
                            return -1;
                        }
                        else
                        {
                            if (!mem.OpenProcess(Convert.ToInt32(args[i + 1])))
                            {
                                Console.WriteLine("Process Not Found");
                                return 1;
                            }
                            attached = true;
                        }
                    }
                }
                else if (args[i] == "/pname")
                {
                    string name = "";
                    if (args[i + 1].ToLower().Contains(".exe"))
                    {
                        name = args[i + 1].ToLower().Replace(".exe", "");
                    }
                    if (args[i + 1].ToLower().Contains(".bin"))
                    {
                        name = args[i + 1].ToLower().Replace(".bin", "");
                    }
                    if (Process.GetProcessesByName(name).Length != 0)
                    {
                        if (args[0] == "CheckProcess")
                        {
                            if (Is64BitProcess(Process.GetProcessesByName(name)[0]))
                            {
                                Console.WriteLine("64bit");
                            }
                            else
                            {
                                Console.WriteLine("32bit");
                            }
                            return 0;
                        }
                        if (Environment.Is64BitProcess)
                        {
                            if (Is64BitProcess(Process.GetProcessesByName(name)[0]))
                            {
                                if (!mem.OpenProcess(name))
                                {
                                    Console.WriteLine("Attach Failed");
                                    return 1;
                                }
                                attached = true;
                            }
                            else
                            {
                                if (!mem.OpenProcess(name))
                                {
                                    Console.WriteLine("64bit process cannot access 32bit process.");
                                    return 1;
                                }
                                else
                                {
                                    Console.WriteLine("Warning: 64bit process is accessing 32bit process.");
                                }
                                attached = true;
                            }
                        }
                        else
                        {
                            if (Is64BitProcess(Process.GetProcessesByName(name)[0]))
                            {
                                Console.WriteLine("32bit process cannot access 64bit process.");
                                return 1;
                            }
                            else
                            {
                                if (!mem.OpenProcess(name))
                                {
                                    Console.WriteLine("Process Not Found");
                                    return 1;
                                }
                                attached = true;
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("Process Not Found");
                        return 1;
                    }
                }
            }
            if (!attached)
            {
                Console.WriteLine("/pname or /pid not found!");
                return 1;
            }
            switch (args[0])
            {
                case "ReadBytes":
                    Console.WriteLine(string.Join(" ", mem.ReadBytes(args[1], Convert.ToInt32(args[2])).Select(b => b.ToString("X2"))));
                    errorcode = 0;
                    break;
                case "ReadFloat":
                    Console.WriteLine(mem.ReadFloat(args[1]));
                    errorcode = 0;
                    break;
                case "ReadDouble":
                    Console.WriteLine(mem.ReadDouble(args[1]));
                    errorcode = 0;
                    break;
                case "ReadInt":
                    Console.WriteLine(mem.ReadInt(args[1]));
                    errorcode = 0;
                    break;
                case "ReadLong":
                    Console.WriteLine(mem.ReadLong(args[1]));
                    errorcode = 0;
                    break;
                case "ReadUInt":
                    Console.WriteLine(mem.ReadUInt(args[1]));
                    errorcode = 0;
                    break;
                case "Read2ByteMove":
                    Console.WriteLine(mem.Read2ByteMove(args[1], Convert.ToInt32(args[2])));
                    errorcode = 0;
                    break;
                case "ReadIntMove":
                    Console.WriteLine(mem.ReadIntMove(args[1], Convert.ToInt32(args[2])));
                    errorcode = 0;
                    break;
                case "ReadUIntMove":
                    Console.WriteLine(mem.ReadUIntMove(args[1], Convert.ToInt32(args[2])));
                    errorcode = 0;
                    break;
                case "Read2Byte":
                    Console.WriteLine(mem.Read2Byte(args[1]));
                    errorcode = 0;
                    break;
                case "ReadByte":
                    Console.WriteLine(mem.ReadByte(args[1]));
                    errorcode = 0;
                    break;
                case "ReadBits":
                    Console.WriteLine(string.Concat(mem.ReadBits(args[1]).Select(b => b ? '1' : '0')));
                    errorcode = 0;
                    break;
                case "WriteMemory":
                    if (mem.WriteMemory(args[1], args[2], args[3]))
                        errorcode = 0;
                    break;
                case "WriteMove":
                    if(mem.WriteMove(args[1], args[2], args[3], Convert.ToInt32(args[4])))
                        errorcode = 0;
                    break;
                case "WriteBytes":
                    string input = args[2];
                    var result = new List<byte>();
                    for (int i = 0; i < input.Length; i += 2)
                    {
                        string hex;
                        if (i + 2 <= input.Length)
                            hex = input.Substring(i, 2);
                        else
                            hex = input.Substring(i, 1);

                        result.Add(Convert.ToByte(hex, 16));
                    }
                    mem.WriteBytes(args[1], result.ToArray());
                    errorcode = 0;
                    break;
                case "WriteBits":
                    if (args[2].Length == 8)
                    {
                        mem.WriteBits(args[1], args[2].Select(c => c == '1').ToArray());
                        errorcode = 0;
                    }
                    else
                    {
                        Console.WriteLine("The argument to \"WriteBits\" must be an 8-digit binary number.");
                        errorcode = 1;
                    }
                    break;
                case "AoBScan":
                    Task.Run(async () =>
                    {
                        var results = await mem.AoBScan(args[1], true, true, true);
                        if (!results.Any())
                        {
                            Console.WriteLine("AoB Scan not found.");
                            errorcode = -1;
                        }
                        else
                        {
                            foreach (var res in results)
                            {
                                Console.WriteLine("0x" + res.ToString("X"));
                            }
                            errorcode = 0;
                        }
                    }).Wait();
                    break;
                case "AoBRangeScan":
                    Task.Run(async () =>
                    {
                        var results = await mem.AoBScan(Convert.ToInt64(args[1], 16), Convert.ToInt64(args[2], 16), args[3], true, true, true);
                        Console.WriteLine("This command may not run successfully. Experimental and advanced");
                        if (!results.Any())
                        {
                            Console.WriteLine("AoB Scan not found.");
                            errorcode = -1;
                        }
                        else
                        {
                            foreach (var res in results)
                            {
                                Console.WriteLine("0x" + res.ToString("X"));
                            }
                            errorcode = 0;
                        }
                    }).Wait();
                    break;
                default:
                    Console.WriteLine($"The function {args[0]} was not recognized.");
                    errorcode = 1;
                    break;
            }
            if (isAdminMode)
            {
                writer.Close();
            }
            return errorcode;
        }
    }
}
