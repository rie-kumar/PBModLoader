using SharpMonoInjector;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PBModLoader
{
    public class ModRunner
    {
        public static readonly string GameExeName = "PowerBeatsVR.exe";
        public static readonly string ModsPath = "Mods";

        static MonoProcess SelectedProcess = null;

        static void Main(string[] args)
        {
            string curDir = Directory.GetCurrentDirectory();

            string exeFullPath = Path.Combine(curDir, GameExeName);
            string modsFullPath = Path.Combine(curDir, ModsPath);

            var info = Directory.CreateDirectory(modsFullPath);
            var files = info.GetFiles();
            List<ModInfo> mods = new List<ModInfo>();
            Process sp = Process.Start(exeFullPath);
            sp.WaitForExit();

            Thread.Sleep(5000);

            foreach (var file in files)
            {
                string modNamespace = Path.GetFileNameWithoutExtension(file.Name);
                mods.Add(new ModInfo(file.FullName, modNamespace));
            }

            int cp = Process.GetCurrentProcess().Id;

            foreach (Process p in Process.GetProcesses())
            {
                if (p.Id == cp)
                    continue;

                const ProcessAccessRights flags = ProcessAccessRights.PROCESS_QUERY_INFORMATION | ProcessAccessRights.PROCESS_VM_READ;
                IntPtr handle;

                if ((handle = Native.OpenProcess(flags, false, p.Id)) != IntPtr.Zero)
                {
                    if (ProcessUtils.GetMonoModule(handle, out IntPtr mono))
                    {
                        if(p.ProcessName == "PowerBeatsVR")
                        {
                            SelectedProcess = new MonoProcess
                            {
                                MonoModule = mono,
                                Id = p.Id,
                                Name = p.ProcessName
                            };
                        }
                    }

                    Native.CloseHandle(handle);
                }
            }

            foreach (var mod in mods)
            {
                ExecuteInject(mod.AssemblyPath, mod.Namespace);
            }

            Console.WriteLine("Press any key to close");
            Console.ReadLine();
        }

        private static void ExecuteInject(string assemblyPath, string injectNamespace)
        {

            IntPtr handle = Native.OpenProcess(ProcessAccessRights.PROCESS_ALL_ACCESS, false, SelectedProcess.Id);

            if (handle == IntPtr.Zero)
            {
                Console.WriteLine("Failed to find process");
                return;
            }

            byte[] file;

            try
            {
                file = File.ReadAllBytes(assemblyPath);
            }
            catch (IOException)
            {
                Console.WriteLine($"Failed to read {assemblyPath}");
                return;
            }

            Console.WriteLine($"Injecting {assemblyPath}");

            using (Injector injector = new Injector(handle, SelectedProcess.MonoModule))
            {
                try
                {
                    IntPtr asm = injector.Inject(file, injectNamespace, "Loader", "Init");
                    Console.WriteLine("Injection successful!");
                }
                catch (InjectorException ie)
                {
                    Console.WriteLine("Inject failed: " + ie.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Injection failed (unknown error): " + e.Message);
                }
            }
        }

        public class ModInfo
        {
            public string AssemblyPath { get; set; }
            public string Namespace { get; set; }

            public ModInfo(string path, string nspace)
            {
                AssemblyPath = path;
                Namespace = nspace;
            }
        }

        public class MonoProcess
        {
            public IntPtr MonoModule { get; set; }

            public string Name { get; set; }

            public int Id { get; set; }
        }
    }
}
