using System;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Microsoft.Unity.VisualStudio.Editor;
using UnityEditor;

public static class DocsBuilder
{
    public static void BuildDocs()
    {
        // Make sure the build directory exists (this keeps GameCI happy)
        System.IO.Directory.CreateDirectory("build/StandaloneWindows");
        // Create a dummy file as well
        System.IO.File.WriteAllText("build/StandaloneWindows/dummy.txt", "");
        
        ProjectGeneration projectGeneration = new ProjectGeneration();
        AssetDatabase.Refresh();
        projectGeneration.GenerateAndWriteSolutionAndProjects();
        
        try
        {
            MungeProjectFiles();
            RunProcess("./build-docs.sh");
        }
        catch (Exception e)
        {
            System.Console.Error.WriteLine("Failed to build docs: " + e);
        }
        
        System.Console.Out.WriteLine("### The following output is to make the GameCI builder happy.");
        System.Console.Out.WriteLine("# Build results\n#\nSize:");
    }

    private static void MungeProjectFiles()
    {
        foreach (var file in Directory.EnumerateFiles("."))
        {
            if (file.EndsWith(".csproj"))
            {
                MungeProjectFile(file);
            } 
        }
    }

    private static void MungeProjectFile(string file)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(file);
        
        var root = doc.DocumentElement;
        var assemblyGroup = doc.CreateElement("ItemGroup", root.NamespaceURI);

        foreach (var possibleDll in
                 Directory.EnumerateFiles("/opt/unity/Editor/Data/MonoBleedingEdge/lib/mono/4.7-api"))
        {
            if (possibleDll.EndsWith(".dll"))
            {
                var assembly = possibleDll.Substring(
                    possibleDll.LastIndexOf('/') + 1);
                assembly = assembly.Substring(0, assembly.Length - 4);
                
                var referenceNode = doc.CreateElement("Reference", root.NamespaceURI);
                referenceNode.SetAttribute("Include", assembly);
                
                var hintNode = doc.CreateElement("HintPath", root.NamespaceURI);
                hintNode.InnerText = possibleDll;

                referenceNode.AppendChild(hintNode);
                assemblyGroup.AppendChild(referenceNode);
            }
            
            root.AppendChild(assemblyGroup);
            
            doc.Save(file);
        }
    }

    private static void RunProcess(string command)
    {
        System.Console.Error.WriteLine("=== Running command: " + command + " ===");
        var process = Process.Start("/bin/bash", command);
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new Exception("Process failed: " + command + " - with error code " + process.ExitCode);
        }
    }
}
