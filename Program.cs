#define USE_DNLIB

using System;
using System.Linq;
using System.IO;


#if !USE_DNLIB
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.DotNet.Signatures;
#else
using dnlib.DotNet;
using dnlib.DotNet.Emit;
#endif

namespace TablePlusPlus
{

    internal class Program
    {
#if !USE_DNLIB
        private static ModuleDefinition _mod = new("Dummy");
        
        private static TypeDefinition? FindType(ModuleDefinition module, string path)
        {
            var types = module.GetAllTypes();
            return types.FirstOrDefault(t => t.FullName == path);
        }
        private static bool DoPatch()
        {
            var targetClassName = "TablePlus.Source.Service.LicenseService";
            var targetMethodName = "Check";


            var targetClass = FindType(_mod, targetClassName);
            if (targetClass == null) return false;

            var targetMethod = new TargetMethod(targetClass, targetMethodName);
            return targetMethod.ApplyPatch();
        }
#else
        private static ModuleContext? _mainCtx;
        private static ModuleDefMD? _main;

        private static TypeDef? getType(ModuleDefMD? mod, string path)
        {
            if (mod == null) return null;
            return mod.Types.FirstOrDefault(t => t.FullName == path);
        }

#endif


        private static bool DoPatch()
        {
            var targetClassName = "TablePlus.Source.Service.LicenseService";
            var targetMethodName = "Check";
            TypeDef? targetClass = getType(_main, targetClassName);
            if (targetClass == null) return false;
            var targetMethod = new TargetMethod(targetClass, targetMethodName);
            return targetMethod.ApplyPatch();
        }

        static void Main(string[] args)
        {
            var targetDir = args.Length == 1 ? args[0] : Directory.GetCurrentDirectory();

            if (!Directory.Exists(targetDir))
            {
                Console.WriteLine("Target Dir not valid! Please recheck", ConsoleColor.Red); 
                Console.ReadKey();
                Environment.Exit(0);
            }
            string targetFile = targetDir + "\\TablePlus.exe";

            if (!File.Exists(targetFile)) 
            { 
                Console.WriteLine("Target File not found! Need: TablePlus.exe", ConsoleColor.Red);
                return;
            }
            string backupName = targetFile + ".bak";
            if (!File.Exists(backupName))
            {
                Console.WriteLine("Creating Backup...", ConsoleColor.Green);
                File.Copy(targetFile, backupName);
            }

            try
            {
#if !USE_DNLIB 
                _mod = ModuleDefinition.FromFile(targetFile);
#else
                _mainCtx = ModuleDef.CreateModuleContext();
                _main = ModuleDefMD.Load(File.ReadAllBytes(targetFile), _mainCtx);
#endif
                var result = DoPatch();
                if (!result)
                {
                    Console.WriteLine("Something not work!", ConsoleColor.Yellow);
                    Environment.Exit(0);
                }
                File.SetAttributes(targetFile, System.IO.FileAttributes.Normal);
#if !USE_DNLIB
                _mod.Write(targetFile);
#else
                _main.NativeWrite(targetFile);
#endif
                Console.WriteLine("Done!", ConsoleColor.Green);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}", ConsoleColor.Red);
            }
        }

    }

    public class TargetMethod
    {
        // Reserved
        //private TypeDefinition parent;
        //private string name;

#if !USE_DNLIB
        private static MethodDefinition? _methodObj;
        
        public TargetMethod(TypeDefinition type, string name)
        {
            //parent = type ?? throw new ArgumentNullException("type");
            //this.name = name ?? throw new ArgumentNullException("name");
            _methodObj = FindMethod(type, name);
        }
        private static MethodDefinition? FindMethod(TypeDefinition type, string name)
        {
            return type.Methods.FirstOrDefault(method => method.Name == name);
        }

                public bool ApplyPatch()
        {
            if (_methodObj == null) return false;

            try
            {
                CilMethodBody? body = _methodObj.CilMethodBody;
                if (body == null) return false;
                CilMethodBody? newBody = new(_methodObj);
                var found = false;
                for (int i = 0; i < body.Instructions.Count; i++)
                {
                    if (body.Instructions[i].OpCode != CilOpCodes.Ldarg_0) continue;
                    if (body.Instructions[i + 1].OpCode != CilOpCodes.Ldc_I4_1) continue;
                    if (body.Instructions[i + 2].OpCode != CilOpCodes.Stfld) continue;
                    found = true;
                    Console.WriteLine("Found target instructions", ConsoleColor.Cyan);
                    newBody.Instructions.Add(body.Instructions[i]);
                    newBody.Instructions.Add(body.Instructions[i + 1]);
                    newBody.Instructions.Add(body.Instructions[i + 2]);
                    break;

                }
                if (!found) return false;
                Console.WriteLine("Replacing Method body...", ConsoleColor.Cyan);
                newBody.Instructions.Add(new CilInstruction(CilOpCodes.Ret));
                _methodObj.CilMethodBody?.Instructions.Clear();
                _methodObj.CilMethodBody = newBody;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }
#else
        private static MethodDef? _method;


        public TargetMethod(TypeDef type, string name)
        {
            _method = GetMethod(type, name);
        }


        private static MethodDef? GetMethod(TypeDef type, string name)
        {
            return type.Methods.FirstOrDefault(method => method.Name == name);
        }


        public bool ApplyPatch()
        {
            if (_method == null) return false;
            try
            {
                if(!_method.HasBody) return false;
                CilBody? body = _method.Body;
                CilBody? newBody = new CilBody();
                var found = false;
                for (int i = 0; i < body.Instructions.Count; i++)
                {
                    if(body.Instructions[i].OpCode != OpCodes.Ldarg_0) continue;
                    if(body.Instructions[i+1].OpCode != OpCodes.Ldc_I4_1) continue;
                    if(body.Instructions[i+2].OpCode != OpCodes.Stfld) continue;
                    found = true;
                    Console.WriteLine("Found target instructions", ConsoleColor.Cyan);
                    
                    newBody.Instructions.Add(body.Instructions[i]);
                    newBody.Instructions.Add(body.Instructions[i + 1]);
                    newBody.Instructions.Add(body.Instructions[i + 2]);

                    break;
                }
                if (!found) return false;
                Console.WriteLine("Replacing Method body...", ConsoleColor.Cyan);
                newBody.Instructions.Add(new Instruction(OpCodes.Ret));
                _method.Body.Instructions.Clear();
                _method.Body = newBody;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message, ConsoleColor.Red);
                return false;
            }
        }
#endif
    }
}
