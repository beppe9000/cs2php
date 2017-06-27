﻿using Lang.Cs.Compiler;
using Lang.Php;
using Lang.Php.Compiler;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lang.Php.Compiler.Translator;
using Lang.Php.Framework;
using Microsoft.CodeAnalysis;

namespace Lang.Cs2Php
{

    /*
    smartClass
    option NoAdditionalFile
    
    property CsProject string 
    
    property OutDir string 
    
    property Referenced List<string> 
    	init #
    
    property TranlationHelpers List<string> 
    	init #
    
    property ReferencedPhpLibsLocations Dictionary<string,string> Location of referenced libraries in PHP, taken from compiler commandline option
    	init new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
    
    property Configuration string i.e. DEBUG, RELEASE
    	init "RELEASE"
    
    property BinaryOutputDir string 
    smartClassEnd
    */

    public partial class CompilerEngine : MarshalByRefObject, IConfigData
    {
        #region Static Methods

        // Public Methods 

        public static void ExecuteInSeparateAppDomain(Action<CompilerEngine> compilerEngineAction, AppDomain forceDomain = null)
        {

            if (compilerEngineAction == null)
                throw new ArgumentNullException("compilerEngineAction");

            var appDomain = forceDomain;
            if (appDomain == null)
            {
                var domainName = "sandbox" + Guid.NewGuid();
                var domainSetup = new AppDomainSetup
                {
                    ApplicationName = domainName,
                    ApplicationBase = Environment.CurrentDirectory
                };
                appDomain = AppDomain.CreateDomain(domainName, null, domainSetup);
            }
            try
            {
                var wrapperType = typeof(CompilerEngine);
                var compilerEngine = (CompilerEngine)appDomain.CreateInstanceFrom(
                    wrapperType.Assembly.Location,
                    wrapperType.FullName).Unwrap();
                compilerEngineAction(compilerEngine);
            }
            finally
            {
                if (forceDomain == null)
                    AppDomain.Unload(appDomain);
            }
        }
        // Private Methods 

        /// <summary>
        /// Zamienia deklarowane referencje na te, które są w katalogu aplikacji
        /// </summary>
        /// <param name="comp"></param>
        private static void Swap(Cs2PhpCompiler comp)
        {
            // ReSharper disable once CSharpWarnings::CS1030
#warning 'Be careful in LINUX'
            var files = new DirectoryInfo(ExeDir).GetFiles("*.*").ToDictionary(a => a.Name.ToLower(), a => a.FullName);
            var PortableExecutableReferences = comp.CSharpProject.MetadataReferences.OfType<PortableExecutableReference>()
                .Select(
                    reference => new
                    {
                        FileShortName = new FileInfo(reference.FilePath).Name.ToLower(),
                        Reference = reference
                    })
                .ToArray();



            foreach (var fileReference in PortableExecutableReferences)
            {
                string fileFullName;
                if (!files.TryGetValue(fileReference.FileShortName, out fileFullName))
                    continue;

                var remove = fileReference.Reference;
                var add = MetadataReference.CreateFromFile(fileFullName, MetadataReferenceProperties.Assembly);
                if (remove.Display == add.Display)
                    continue;
                comp.RemoveMetadataReferences(remove);
                comp.AddMetadataReferences(add);
                Console.WriteLine("Swap\r\n    {0}\r\n    {1}", remove.Display, add.Display);
            }
        }

        private static void WriteCompileError(Diagnostic diag)
        {
            // var info = diag.;
            switch (diag.Severity)
            {
                case DiagnosticSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.Write("Warning");
                    break;
                case DiagnosticSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write("Error");
                    break;
                default:
                    return;
            }
            Console.ResetColor();
            Console.WriteLine(": {0}{1}", diag.GetMessage(), diag.Location);
        }

        #endregion Static Methods

        #region Methods

        // Public Methods 

        public void Check()
        {
            if (!File.Exists(CsProject))
                throw new Exception(string.Format("File {0} doesn't exist", _csProject));
        }

        public void Compile()
        {
            using (var comp = PreparePhpCompiler())
            {
                var emitResult = comp.Compile2PhpAndEmit(_outDir, DllFilename, _referencedPhpLibsLocations);
                if (emitResult.Success) return;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Compilation errors:");
                Console.ResetColor();
                foreach (var i in emitResult.Diagnostics)
                    WriteCompileError(i);
            }
        }

        /// <summary>
        /// Loads csproj, prepares references and loads assemblies
        /// </summary>
        /// <returns></returns>
        public Cs2PhpCompiler PreparePhpCompiler()
        {
            var comp = new Cs2PhpCompiler
            {
                VerboseToConsole = true,
                ThrowExceptions = true
            };

            // Console.WriteLine("Try to load " + csProject);
            comp.LoadProject(_csProject, _configuration);

            Console.WriteLine("Preparing before compilation");

            #region Remove Lang.Php reference

            {
                // ... will be replaced by reference to dll from compiler base dir
                // I know - compilation libraries should be loaded into separate application domain
                var remove =
                    comp.CSharpProject.MetadataReferences.FirstOrDefault(i => i.Display.EndsWith("Lang.Php.dll"));
                if (remove != null)
                    comp.RemoveMetadataReferences(remove);
            }

            #endregion

            string[] filenames;

            #region We have to remove and add again references - strange

            {
                // in other cases some referenced libraries are ignored
                var refToRemove =
                    comp.CSharpProject.MetadataReferences.OfType<PortableExecutableReference>().ToList();
                foreach (var i in refToRemove)
                    comp.RemoveMetadataReferences(i);
                var ref1 = refToRemove.Select(i => i.FilePath).Union(_referenced).ToList();
                ref1.Add(typeof(PhpDummy).Assembly.GetCodeLocation().FullName);
                ref1.AddRange(Referenced);
                filenames = ref1.Distinct().ToArray();
            }

            #endregion

            foreach (var fileName in filenames)
                comp.AddMetadataReferences(MetadataReference.CreateFromFile(fileName, MetadataReferenceProperties.Assembly));


            Swap(comp);
            comp.ReferencedAssemblies = comp.CSharpProject.MetadataReferences
                .Select(reference => comp.Sandbox.LoadByFullFilename(reference.Display).WrappedAssembly)
                .ToList();
            foreach (var fileName in _tranlationHelpers)
            {
                var assembly = comp.Sandbox.LoadByFullFilename(fileName).WrappedAssembly;
                // ReSharper disable once UnusedVariable
                var an = assembly.GetName();
                Console.WriteLine(" Add translation helper {0}", assembly.FullName);
                comp.TranslationAssemblies.Add(assembly);
            }

            comp.TranslationAssemblies.Add(typeof(Extension).Assembly);
            comp.TranslationAssemblies.Add(typeof(Translator).Assembly);

            return comp;
        }

        public void Set1(string[] referenced, string[] tranlationHelpers, string[] a)
        {
            _referenced = referenced.ToList();
            _tranlationHelpers = tranlationHelpers.ToList();
            _referencedPhpLibsLocations = (from i in a
                                           select i.Split('\n')
                ).ToDictionary(aa => aa[0], aa => aa[1]);
        }

        #endregion Methods

        #region Fields

        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "cs2php" + Guid.NewGuid().ToString("D"));

        #endregion Fields

        #region Static Properties

        private static string ExeDir
        {
            get
            {
                var ea = new Uri(Assembly.GetExecutingAssembly().CodeBase, UriKind.RelativeOrAbsolute);
                var fi = new FileInfo(ea.LocalPath.Replace("/", "\\"));
                return fi.DirectoryName;
            }
        }

        #endregion Static Properties

        #region Properties

        public string DllFilename
        {
            get
            {
                var fi = new FileInfo(_csProject);
                var name = fi.Name;
                name = name.Substring(0, name.Length - fi.Extension.Length);
                name = Path.Combine(!string.IsNullOrEmpty(BinaryOutputDir) ? BinaryOutputDir : _tempDir, name + ".dll");
                return name;
            }
        }

        #endregion Properties
    }
}


// -----:::::##### smartClass embedded code begin #####:::::----- generated 2014-09-05 19:16
// File generated automatically ver 2014-09-01 19:00
// Smartclass.Core, Version=1.0.0.0, Culture=neutral, PublicKeyToken=0c4d5d36fb5eb4ac
namespace Lang.Cs2Php
{
    public partial class CompilerEngine
    {
        /*
        /// <summary>
        /// Tworzy instancję obiektu
        /// </summary>
        public CompilerEngine()
        {
        }
        Przykłady użycia
        implement INotifyPropertyChanged
        implement INotifyPropertyChanged_Passive
        implement ToString ##CsProject## ##OutDir## ##Referenced## ##TranlationHelpers## ##ReferencedPhpLibsLocations## ##Configuration## ##BinaryOutputDir##
        implement ToString CsProject=##CsProject##, OutDir=##OutDir##, Referenced=##Referenced##, TranlationHelpers=##TranlationHelpers##, ReferencedPhpLibsLocations=##ReferencedPhpLibsLocations##, Configuration=##Configuration##, BinaryOutputDir=##BinaryOutputDir##
        implement equals CsProject, OutDir, Referenced, TranlationHelpers, ReferencedPhpLibsLocations, Configuration, BinaryOutputDir
        implement equals *
        implement equals *, ~exclude1, ~exclude2
        */


        #region Constants
        /// <summary>
        /// Nazwa własności CsProject; 
        /// </summary>
        public const string PropertyNameCsProject = "CsProject";
        /// <summary>
        /// Nazwa własności OutDir; 
        /// </summary>
        public const string PropertyNameOutDir = "OutDir";
        /// <summary>
        /// Nazwa własności Referenced; 
        /// </summary>
        public const string PropertyNameReferenced = "Referenced";
        /// <summary>
        /// Nazwa własności TranlationHelpers; 
        /// </summary>
        public const string PropertyNameTranlationHelpers = "TranlationHelpers";
        /// <summary>
        /// Nazwa własności ReferencedPhpLibsLocations; Location of referenced libraries in PHP, taken from compiler commandline option
        /// </summary>
        public const string PropertyNameReferencedPhpLibsLocations = "ReferencedPhpLibsLocations";
        /// <summary>
        /// Nazwa własności Configuration; i.e. DEBUG, RELEASE
        /// </summary>
        public const string PropertyNameConfiguration = "Configuration";
        /// <summary>
        /// Nazwa własności BinaryOutputDir; 
        /// </summary>
        public const string PropertyNameBinaryOutputDir = "BinaryOutputDir";
        #endregion Constants


        #region Methods
        #endregion Methods


        #region Properties
        /// <summary>
        /// 
        /// </summary>
        public string CsProject
        {
            get
            {
                return _csProject;
            }
            set
            {
                value = (value ?? String.Empty).Trim();
                _csProject = value;
            }
        }
        private string _csProject = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public string OutDir
        {
            get
            {
                return _outDir;
            }
            set
            {
                value = (value ?? String.Empty).Trim();
                _outDir = value;
            }
        }
        private string _outDir = string.Empty;
        /// <summary>
        /// 
        /// </summary>
        public List<string> Referenced
        {
            get
            {
                return _referenced;
            }
            set
            {
                _referenced = value;
            }
        }
        private List<string> _referenced = new List<string>();
        /// <summary>
        /// 
        /// </summary>
        public List<string> TranlationHelpers
        {
            get
            {
                return _tranlationHelpers;
            }
            set
            {
                _tranlationHelpers = value;
            }
        }
        private List<string> _tranlationHelpers = new List<string>();
        /// <summary>
        /// Location of referenced libraries in PHP, taken from compiler commandline option
        /// </summary>
        public Dictionary<string, string> ReferencedPhpLibsLocations
        {
            get
            {
                return _referencedPhpLibsLocations;
            }
            set
            {
                _referencedPhpLibsLocations = value;
            }
        }
        private Dictionary<string, string> _referencedPhpLibsLocations = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        /// <summary>
        /// i.e. DEBUG, RELEASE
        /// </summary>
        public string Configuration
        {
            get
            {
                return _configuration;
            }
            set
            {
                value = (value ?? String.Empty).Trim();
                _configuration = value;
            }
        }
        private string _configuration = "RELEASE";
        /// <summary>
        /// 
        /// </summary>
        public string BinaryOutputDir
        {
            get
            {
                return _binaryOutputDir;
            }
            set
            {
                value = (value ?? String.Empty).Trim();
                _binaryOutputDir = value;
            }
        }
        private string _binaryOutputDir = string.Empty;
        #endregion Properties
    }
}
